using System.Security.Cryptography;
using System.Text.Json;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Journal;

/// <summary>
/// Appends records to one journal segment (spec section 7.10). The writer holds the file open with
/// <see cref="FileShare.Read"/>: there is one writer, and readers may look at the file meanwhile.
/// <see cref="Append"/> is safe to call from several threads; calls are serialised.
/// </summary>
public sealed class JournalWriter : IDisposable
{
    private readonly Lock _gate = new();
    private readonly FileStream _stream;
    private readonly RecordCipher _cipher;
    private readonly TimeProvider _timeProvider;
    private ulong _lastSequence;
    private byte[] _lastHash;
    private bool _faulted;
    private bool _disposed;

    private JournalWriter(FileStream stream, RecordCipher cipher, TimeProvider timeProvider, ulong lastSequence, byte[] lastHash)
    {
        _stream = stream;
        _cipher = cipher;
        _timeProvider = timeProvider;
        _lastSequence = lastSequence;
        _lastHash = lastHash;
    }

    /// <summary>
    /// Creates a new segment, creating its folder if needed, and writes its header to disk before
    /// returning. The first record will be sequence 1. An existing file is never overwritten.
    /// </summary>
    /// <param name="segmentPath">The new segment's path.</param>
    /// <param name="journalKey">The 32-byte journal store key, which the writer copies.</param>
    /// <param name="previousSegmentHash">The previous segment's last hash, or null for 32 zero bytes.</param>
    /// <param name="timeProvider">The clock for timestamps; null means <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="journalKey"/> is not 32 bytes, or <paramref name="previousSegmentHash"/> is not
    /// null and not 32 bytes.
    /// </exception>
    /// <exception cref="IOException">The file already exists, or can't be created or written.</exception>
    public static JournalWriter Create(string segmentPath, ReadOnlySpan<byte> journalKey, byte[]? previousSegmentHash = null, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(segmentPath);
        if (previousSegmentHash is not null && previousSegmentHash.Length != JournalFormat.HashSize)
        {
            throw new ArgumentException($"A previous-segment hash is exactly {JournalFormat.HashSize} bytes, not {previousSegmentHash.Length}.", nameof(previousSegmentHash));
        }

        var previous = previousSegmentHash is null ? new byte[JournalFormat.HashSize] : (byte[])previousSegmentHash.Clone();
        var clock = timeProvider ?? TimeProvider.System;
        var cipher = new RecordCipher(journalKey);
        FileStream? stream = null;
        try
        {
            var fullPath = Path.GetFullPath(segmentPath);
            var folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            // CreateNew: overwriting a segment would destroy audit history.
            stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read, bufferSize: 0);
            stream.Write(JournalFormat.EncodeHeader(Guid.NewGuid(), previous, clock.GetUtcNow().UtcTicks));
            stream.Flush(flushToDisk: true);
            return new JournalWriter(stream, cipher, clock, 0, previous);
        }
        catch
        {
            stream?.Dispose();
            cipher.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Opens an existing segment to append to it. The write handle is opened first, so no other writer
    /// can change the file, and then the segment is verified. Appending continues from the last record.
    /// </summary>
    /// <param name="segmentPath">The segment's path.</param>
    /// <param name="journalKey">The 32-byte journal store key, which the writer copies.</param>
    /// <param name="timeProvider">The clock for timestamps; null means <see cref="TimeProvider.System"/>.</param>
    /// <exception cref="ArgumentException"><paramref name="journalKey"/> is not 32 bytes.</exception>
    /// <exception cref="JournalCorruptException">
    /// The segment fails verification, for example because a crash cut its last record short. The file
    /// is left as it is, and <see cref="JournalCorruptException.LastGoodSequence"/> names the last valid record.
    /// </exception>
    /// <exception cref="IOException">The file doesn't exist, or can't be opened or read.</exception>
    public static JournalWriter OpenForAppend(string segmentPath, ReadOnlySpan<byte> journalKey, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(segmentPath);
        var clock = timeProvider ?? TimeProvider.System;
        var cipher = new RecordCipher(journalKey);
        FileStream? stream = null;
        try
        {
            stream = new FileStream(segmentPath, FileMode.Open, FileAccess.Write, FileShare.Read, bufferSize: 0);
            var verification = JournalVerifier.Verify(segmentPath);
            if (!verification.IsIntact)
            {
                throw new JournalCorruptException(
                    $"The journal segment failed verification ({verification.Problem}) after record {verification.RecordCount}; HiveMind won't append to it.",
                    verification.Problem,
                    verification.RecordCount);
            }

            stream.Seek(0, SeekOrigin.End);
            return new JournalWriter(stream, cipher, clock, verification.RecordCount, verification.LastGoodHash);
        }
        catch
        {
            stream?.Dispose();
            cipher.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Appends one record: it seals the body, links the record to the one before it, writes it in a
    /// single write call and flushes it to disk before returning.
    /// </summary>
    /// <exception cref="ArgumentNullException">The entry, its body or its actor SID is null.</exception>
    /// <exception cref="ArgumentException">
    /// The actor SID is empty, longer than 256 bytes of UTF-8 or not valid Unicode, or the sealed body
    /// would be longer than 16 MiB. Nothing is written.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The writer has been disposed.</exception>
    /// <exception cref="InvalidOperationException">An earlier append failed while writing, so the file's end is unknown.</exception>
    /// <exception cref="IOException">The record can't be written. Later appends then throw <see cref="InvalidOperationException"/>.</exception>
    public JournalRecord Append(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Body, nameof(entry));
        ArgumentNullException.ThrowIfNull(entry.ActorSid, nameof(entry));

        var actorSid = JournalFormat.StrictUtf8.GetBytes(entry.ActorSid);
        if (actorSid.Length is 0 or > JournalFormat.MaxActorSidBytes)
        {
            throw new ArgumentException($"An actor SID is 1 to {JournalFormat.MaxActorSidBytes} bytes of UTF-8, not {actorSid.Length}.", nameof(entry));
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(entry.Body, JournalJsonContext.Default.JournalBody);
        try
        {
            if (json.Length > JournalFormat.MaxBodyBytes - RecordCipher.Overhead)
            {
                throw new ArgumentException($"The sealed body would be {json.Length + (long)RecordCipher.Overhead} bytes, over the {JournalFormat.MaxBodyBytes}-byte limit.", nameof(entry));
            }

            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                if (_faulted)
                {
                    throw new InvalidOperationException("An earlier append failed while writing. Reopen the segment with OpenForAppend, which verifies it first.");
                }

                var sequence = _lastSequence + 1;
                var timestamp = new DateTimeOffset(_timeProvider.GetUtcNow().UtcTicks, TimeSpan.Zero);
                var associatedData = JournalFormat.BuildAssociatedData(sequence, timestamp.UtcTicks, entry.Action, entry.Elevated, actorSid, _lastHash);
                var body = _cipher.Seal(json, associatedData);
                var bytes = JournalFormat.BuildRecord(associatedData, body, out var hash);
                var offset = _stream.Position;
                try
                {
                    _stream.Write(bytes);
                    _stream.Flush(flushToDisk: true);
                }
                catch
                {
                    _faulted = true;
                    throw;
                }

                // The record gets its own copies, so a caller can't change the writer's chain.
                var record = new JournalRecord(sequence, timestamp, entry.Action, entry.ActorSid, entry.Elevated, (byte[])_lastHash.Clone(), body, (byte[])hash.Clone(), offset, bytes.Length);
                _lastSequence = sequence;
                _lastHash = hash;
                return record;
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(json);
        }
    }

    /// <summary>Closes the segment and releases the key. Any later <see cref="Append"/> throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cipher.Dispose();
            _stream.Dispose();
        }
    }
}
