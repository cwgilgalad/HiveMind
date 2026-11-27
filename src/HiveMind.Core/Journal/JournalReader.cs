using System.Security.Cryptography;
using System.Text.Json;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Journal;

/// <summary>
/// Reads journal segments. The clear header of every record can be read without the journal key;
/// only <see cref="DecryptBody"/> needs it. Files are opened for reading with
/// <see cref="FileShare.ReadWrite"/>, so a segment can be read while a writer has it open.
/// </summary>
public static class JournalReader
{
    /// <summary>Reads a segment's header.</summary>
    /// <exception cref="JournalCorruptException">The header is cut short, or its magic, version or time is wrong.</exception>
    /// <exception cref="IOException">The file can't be opened or read.</exception>
    public static JournalSegmentHeader ReadHeader(string segmentPath)
    {
        using var stream = OpenRead(segmentPath);
        return ReadHeaderOrThrow(stream);
    }

    /// <summary>
    /// Reads a segment's records lazily, in file order. Reading stops silently at the first record that
    /// is cut short or malformed. Hashes and the chain are not checked here: that is
    /// <see cref="JournalVerifier.Verify"/>'s job.
    /// </summary>
    /// <exception cref="JournalCorruptException">The header is bad. Thrown when enumeration starts.</exception>
    /// <exception cref="IOException">The file can't be opened or read.</exception>
    public static IEnumerable<JournalRecord> ReadRecords(string segmentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(segmentPath);
        return Enumerate(segmentPath);

        static IEnumerable<JournalRecord> Enumerate(string segmentPath)
        {
            using var stream = OpenRead(segmentPath);
            ReadHeaderOrThrow(stream);
            var length = stream.Length;
            while (JournalFormat.TryReadRecord(stream, length, out var record) == JournalFormat.RecordStatus.Ok)
            {
                yield return record!.ToJournalRecord();
            }
        }
    }

    /// <summary>
    /// Decrypts a record's body with the journal key, authenticating it against the record's clear
    /// fields, from its sequence number through its previous hash.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="journalKey"/> is not 32 bytes, or the record's SID or previous hash has a length
    /// no record can have.
    /// </exception>
    /// <exception cref="CryptographicException">
    /// The body doesn't authenticate: the key is wrong, or the body or any clear field was changed or
    /// moved from another record.
    /// </exception>
    /// <exception cref="JsonException">The body authenticated but is not a journal body.</exception>
    public static JournalBody DecryptBody(JournalRecord record, ReadOnlySpan<byte> journalKey)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(record.ActorSid, nameof(record));
        ArgumentNullException.ThrowIfNull(record.PreviousHash, nameof(record));
        ArgumentNullException.ThrowIfNull(record.EncryptedBody, nameof(record));

        using var cipher = new RecordCipher(journalKey);
        var actorSid = JournalFormat.StrictUtf8.GetBytes(record.ActorSid);
        if (actorSid.Length > JournalFormat.MaxActorSidBytes || record.PreviousHash.Length != JournalFormat.HashSize)
        {
            throw new ArgumentException("The record's actor SID or previous hash has a length no journal record can have.", nameof(record));
        }

        var associatedData = JournalFormat.BuildAssociatedData(record.Sequence, record.TimestampUtc.UtcTicks, record.Action, record.Elevated, actorSid, record.PreviousHash);
        var plaintext = cipher.Open(record.EncryptedBody, associatedData);
        try
        {
            return JsonSerializer.Deserialize(plaintext, JournalJsonContext.Default.JournalBody)
                ?? throw new JsonException("The journal body is null.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    /// <summary>Opens a segment for reading, sharing it with a writer.</summary>
    internal static FileStream OpenRead(string segmentPath) =>
        new(segmentPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

    private static JournalSegmentHeader ReadHeaderOrThrow(FileStream stream) =>
        JournalFormat.TryReadHeader(stream, stream.Length)
        ?? throw new JournalCorruptException("The journal segment's header is missing, damaged or of an unknown version.", JournalProblem.BadHeader, 0);
}
