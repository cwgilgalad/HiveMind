using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HiveMind.Core.Journal;

/// <summary>
/// The byte layout of a journal segment, shared by the writer, the reader and the verifier so it is
/// defined in one place. All integers are little-endian.
/// </summary>
/// <remarks>
/// <para>
/// The 62-byte header is ASCII <c>HMJ1</c>, the format version (uint16, 1), the segment id (16 bytes,
/// <see cref="Guid.ToByteArray()"/>), the previous segment's last hash (32 bytes) and the creation
/// time (int64 UTC ticks).
/// </para>
/// <para>
/// Each record is, in order and counting from the record's start: <c>recordLength</c> (uint32, the
/// bytes after this field) at 0, <c>sequence</c> (uint64) at 4, UTC ticks (int64) at 12, the action
/// (uint16) at 20, flags (uint8, bit 0 = elevated) at 22, the actor-SID length <c>n</c> (uint16, at
/// most 256) at 23, the SID in UTF-8 at 25, the previous hash (32 bytes) at 25 + n, the body length
/// <c>m</c> (uint32, at most 16 MiB) at 57 + n, the body at 61 + n, and the hash (32 bytes) at
/// 61 + n + m. The hash is SHA-256 over the bytes from <c>sequence</c> through the end of the body;
/// the body's associated data is the bytes from <c>sequence</c> through the previous hash.
/// </para>
/// </remarks>
internal static class JournalFormat
{
    /// <summary>The size of the segment header.</summary>
    internal const int HeaderSize = 62;

    /// <summary>The size of a SHA-256 hash.</summary>
    internal const int HashSize = 32;

    /// <summary>The longest actor SID, in UTF-8 bytes.</summary>
    internal const int MaxActorSidBytes = 256;

    /// <summary>The longest sealed body: 16 MiB.</summary>
    internal const int MaxBodyBytes = 16 * 1024 * 1024;

    /// <summary>The size of the <c>recordLength</c> field that starts each record.</summary>
    internal const int LengthFieldSize = sizeof(uint);

    private const ushort FormatVersion = 1;
    private const int VersionOffset = 4;
    private const int SegmentIdOffset = VersionOffset + sizeof(ushort);
    private const int PreviousSegmentHashOffset = SegmentIdOffset + 16;
    private const int CreatedOffset = PreviousSegmentHashOffset + HashSize;

    // Offsets within a record's content, which starts just after recordLength.
    private const int TicksOffset = sizeof(ulong);
    private const int ActionOffset = TicksOffset + sizeof(long);
    private const int FlagsOffset = ActionOffset + sizeof(ushort);
    private const int SidLengthOffset = FlagsOffset + 1;
    private const int SidOffset = SidLengthOffset + sizeof(ushort);

    // Everything in a record's content except the SID and the body.
    private const int FixedContentSize = SidOffset + HashSize + sizeof(uint) + HashSize;

    private const byte ElevatedFlag = 0x01;

    /// <summary>UTF-8 that throws on unpaired surrogates, so a SID always round-trips byte for byte.</summary>
    internal static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static ReadOnlySpan<byte> Magic => "HMJ1"u8;

    /// <summary>Builds a segment header.</summary>
    internal static byte[] EncodeHeader(Guid segmentId, ReadOnlySpan<byte> previousSegmentHash, long createdTicks)
    {
        var header = new byte[HeaderSize];
        var span = header.AsSpan();
        Magic.CopyTo(span);
        BinaryPrimitives.WriteUInt16LittleEndian(span[VersionOffset..], FormatVersion);
        segmentId.TryWriteBytes(span.Slice(SegmentIdOffset, 16));
        previousSegmentHash.CopyTo(span.Slice(PreviousSegmentHashOffset, HashSize));
        BinaryPrimitives.WriteInt64LittleEndian(span[CreatedOffset..], createdTicks);
        return header;
    }

    /// <summary>
    /// Reads the header from the start of <paramref name="stream"/>, leaving the stream just past it.
    /// Returns null if the file is shorter than a header, or the magic, version or time is wrong.
    /// </summary>
    internal static JournalSegmentHeader? TryReadHeader(Stream stream, long fileLength)
    {
        if (fileLength < HeaderSize)
        {
            return null;
        }

        Span<byte> header = stackalloc byte[HeaderSize];
        stream.Position = 0;
        stream.ReadExactly(header);
        var ticks = BinaryPrimitives.ReadInt64LittleEndian(header[CreatedOffset..]);
        if (!header[..4].SequenceEqual(Magic)
            || BinaryPrimitives.ReadUInt16LittleEndian(header[VersionOffset..]) != FormatVersion
            || !IsValidTicks(ticks))
        {
            return null;
        }

        return new JournalSegmentHeader(
            new Guid(header.Slice(SegmentIdOffset, 16)),
            header.Slice(PreviousSegmentHashOffset, HashSize).ToArray(),
            new DateTimeOffset(ticks, TimeSpan.Zero));
    }

    /// <summary>
    /// Builds a record's associated data: its bytes from <c>sequence</c> through the previous hash.
    /// </summary>
    internal static byte[] BuildAssociatedData(ulong sequence, long ticks, JournalAction action, bool elevated, ReadOnlySpan<byte> actorSid, ReadOnlySpan<byte> previousHash)
    {
        var data = new byte[SidOffset + actorSid.Length + HashSize];
        var span = data.AsSpan();
        BinaryPrimitives.WriteUInt64LittleEndian(span, sequence);
        BinaryPrimitives.WriteInt64LittleEndian(span[TicksOffset..], ticks);
        BinaryPrimitives.WriteUInt16LittleEndian(span[ActionOffset..], (ushort)action);
        span[FlagsOffset] = elevated ? ElevatedFlag : (byte)0;
        BinaryPrimitives.WriteUInt16LittleEndian(span[SidLengthOffset..], (ushort)actorSid.Length);
        actorSid.CopyTo(span[SidOffset..]);
        previousHash.CopyTo(span[(SidOffset + actorSid.Length)..]);
        return data;
    }

    /// <summary>
    /// Builds a whole record, from its length field through its hash, from the associated data that
    /// <see cref="BuildAssociatedData"/> made and the sealed body.
    /// </summary>
    internal static byte[] BuildRecord(ReadOnlySpan<byte> associatedData, ReadOnlySpan<byte> body, out byte[] hash)
    {
        var contentLength = associatedData.Length + sizeof(uint) + body.Length + HashSize;
        var record = new byte[LengthFieldSize + contentLength];
        var span = record.AsSpan();
        BinaryPrimitives.WriteUInt32LittleEndian(span, (uint)contentLength);
        var content = span[LengthFieldSize..];
        associatedData.CopyTo(content);
        BinaryPrimitives.WriteUInt32LittleEndian(content[associatedData.Length..], (uint)body.Length);
        body.CopyTo(content[(associatedData.Length + sizeof(uint))..]);
        hash = SHA256.HashData(content[..^HashSize]);
        hash.CopyTo(content[^HashSize..]);
        return record;
    }

    /// <summary>
    /// Reads the record at the stream's position. Every length is checked against its limit and
    /// against the bytes left in the file before anything is allocated for it.
    /// </summary>
    /// <param name="stream">A seekable stream positioned at a record, or at the end.</param>
    /// <param name="fileLength">The length of the file, taken when it was opened.</param>
    /// <param name="record">The record, when the result is <see cref="RecordStatus.Ok"/>.</param>
    internal static RecordStatus TryReadRecord(Stream stream, long fileLength, out RawRecord? record)
    {
        record = null;
        var start = stream.Position;
        var remaining = fileLength - start;
        if (remaining == 0)
        {
            return RecordStatus.EndOfFile;
        }

        try
        {
            Span<byte> head = stackalloc byte[LengthFieldSize + SidOffset];
            if (remaining < LengthFieldSize)
            {
                return RecordStatus.Truncated;
            }

            stream.ReadExactly(head[..LengthFieldSize]);
            var recordLength = BinaryPrimitives.ReadUInt32LittleEndian(head);
            if (recordLength > remaining - LengthFieldSize || remaining < head.Length)
            {
                return RecordStatus.Truncated;
            }

            stream.ReadExactly(head[LengthFieldSize..]);
            int sidLength = BinaryPrimitives.ReadUInt16LittleEndian(head[(LengthFieldSize + SidLengthOffset)..]);
            if (sidLength > MaxActorSidBytes)
            {
                return RecordStatus.Malformed;
            }

            var bodyLengthOffset = LengthFieldSize + SidOffset + sidLength + HashSize;
            if (bodyLengthOffset + sizeof(uint) > remaining)
            {
                return RecordStatus.Truncated;
            }

            Span<byte> bodyLengthField = stackalloc byte[sizeof(uint)];
            stream.Position = start + bodyLengthOffset;
            stream.ReadExactly(bodyLengthField);
            var bodyLength = BinaryPrimitives.ReadUInt32LittleEndian(bodyLengthField);
            if (bodyLength > MaxBodyBytes)
            {
                return RecordStatus.Malformed;
            }

            var contentLength = (long)FixedContentSize + sidLength + bodyLength;
            if (LengthFieldSize + contentLength > remaining)
            {
                return RecordStatus.Truncated;
            }

            if (recordLength != contentLength)
            {
                return RecordStatus.Malformed;
            }

            // recordLength now equals a sum of checked lengths, so it is at most
            // FixedContentSize + MaxActorSidBytes + MaxBodyBytes (89 + 256 + 16 MiB).
            var content = new byte[recordLength];
            stream.Position = start + LengthFieldSize;
            stream.ReadExactly(content);
            if (!IsValidTicks(BinaryPrimitives.ReadInt64LittleEndian(content.AsSpan(TicksOffset))))
            {
                return RecordStatus.Malformed;
            }

            record = new RawRecord(start, content, sidLength, (int)bodyLength);
            return RecordStatus.Ok;
        }
        catch (EndOfStreamException)
        {
            // The file shrank after it was opened.
            return RecordStatus.Truncated;
        }
    }

    // Ticks a DateTimeOffset at offset zero can hold.
    private static bool IsValidTicks(long ticks) => ticks >= 0 && ticks <= DateTime.MaxValue.Ticks;

    /// <summary>The outcome of <see cref="TryReadRecord"/>.</summary>
    internal enum RecordStatus
    {
        /// <summary>A structurally sound record was read.</summary>
        Ok,

        /// <summary>The stream is exactly at the end of the file: there are no more records.</summary>
        EndOfFile,

        /// <summary>The record needs bytes past the end of the file.</summary>
        Truncated,

        /// <summary>The bytes are not a record any writer produced: a length is over its limit or inconsistent.</summary>
        Malformed,
    }

    /// <summary>A structurally sound record, not yet checked against its hash or the chain.</summary>
    internal sealed class RawRecord
    {
        private readonly byte[] _content;
        private readonly int _sidLength;
        private readonly int _bodyLength;

        internal RawRecord(long fileOffset, byte[] content, int sidLength, int bodyLength)
        {
            FileOffset = fileOffset;
            _content = content;
            _sidLength = sidLength;
            _bodyLength = bodyLength;
        }

        /// <summary>Where the record's length field is in the file.</summary>
        internal long FileOffset { get; }

        internal ulong Sequence => BinaryPrimitives.ReadUInt64LittleEndian(_content);

        internal ReadOnlySpan<byte> PreviousHash => _content.AsSpan(SidOffset + _sidLength, HashSize);

        internal ReadOnlySpan<byte> StoredHash => _content.AsSpan(_content.Length - HashSize);

        /// <summary>Recomputes SHA-256 over the bytes from <c>sequence</c> through the end of the body.</summary>
        internal byte[] ComputeHash() => SHA256.HashData(_content.AsSpan(0, _content.Length - HashSize));

        /// <summary>Converts to the public record. The reader keeps only bit 0 of the flags.</summary>
        internal JournalRecord ToJournalRecord()
        {
            var span = _content.AsSpan();
            return new JournalRecord(
                Sequence,
                new DateTimeOffset(BinaryPrimitives.ReadInt64LittleEndian(span[TicksOffset..]), TimeSpan.Zero),
                (JournalAction)BinaryPrimitives.ReadUInt16LittleEndian(span[ActionOffset..]),
                Encoding.UTF8.GetString(span.Slice(SidOffset, _sidLength)),
                (span[FlagsOffset] & ElevatedFlag) != 0,
                PreviousHash.ToArray(),
                span.Slice(SidOffset + _sidLength + HashSize + sizeof(uint), _bodyLength).ToArray(),
                StoredHash.ToArray(),
                FileOffset,
                LengthFieldSize + _content.Length);
        }
    }
}
