namespace HiveMind.Core.Journal;

/// <summary>The first problem <see cref="JournalVerifier.Verify"/> found in a segment.</summary>
public enum JournalProblem
{
    /// <summary>No problem: the segment is intact.</summary>
    None,

    /// <summary>The header is cut short, or its magic, version or time is wrong.</summary>
    BadHeader,

    /// <summary>The last record is cut short, as a crash in the middle of an append leaves it.</summary>
    TruncatedTail,

    /// <summary>A record's sequence number isn't the next one: a record was removed or moved.</summary>
    SequenceGap,

    /// <summary>A record's previous hash doesn't match the hash before it.</summary>
    BrokenChain,

    /// <summary>A record's bytes don't match its hash, or aren't a record any writer produced.</summary>
    HashMismatch,
}

/// <summary>The result of verifying a journal segment.</summary>
/// <param name="IsIntact">Whether the segment has no problem.</param>
/// <param name="RecordCount">The number of valid records before the first problem.</param>
/// <param name="FirstBadSequence">The sequence number expected where the problem is (<c>RecordCount + 1</c>), or null if intact.</param>
/// <param name="Problem">The first problem found.</param>
/// <param name="LastGoodHash">
/// The hash of the last valid record; the header's previous-segment hash if there is none; or an empty
/// array if the header itself is bad.
/// </param>
public sealed record JournalVerification(bool IsIntact, ulong RecordCount, ulong? FirstBadSequence, JournalProblem Problem, byte[] LastGoodHash);

/// <summary>
/// Checks a journal segment without the journal key: the header, then for each record its structure,
/// its sequence number, its link to the hash before it, and its own hash. It stops at the first problem.
/// </summary>
public static class JournalVerifier
{
    /// <summary>Verifies a segment and reports the first problem, if any.</summary>
    /// <exception cref="IOException">The file can't be opened or read.</exception>
    public static JournalVerification Verify(string segmentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(segmentPath);
        using var stream = JournalReader.OpenRead(segmentPath);
        var length = stream.Length;
        var header = JournalFormat.TryReadHeader(stream, length);
        if (header is null)
        {
            return new JournalVerification(false, 0, 1, JournalProblem.BadHeader, []);
        }

        ulong count = 0;
        var lastHash = header.PreviousSegmentHash;
        while (true)
        {
            var status = JournalFormat.TryReadRecord(stream, length, out var record);
            if (status == JournalFormat.RecordStatus.EndOfFile)
            {
                return new JournalVerification(true, count, null, JournalProblem.None, lastHash);
            }

            var problem = status switch
            {
                JournalFormat.RecordStatus.Truncated => JournalProblem.TruncatedTail,
                JournalFormat.RecordStatus.Malformed => JournalProblem.HashMismatch,
                _ => Check(record!, count + 1, lastHash),
            };

            if (problem != JournalProblem.None)
            {
                return new JournalVerification(false, count, count + 1, problem, lastHash);
            }

            count++;
            lastHash = record!.StoredHash.ToArray();
        }
    }

    // Checks a structurally sound record, in the order that names the most specific problem.
    private static JournalProblem Check(JournalFormat.RawRecord record, ulong expectedSequence, byte[] previousHash)
    {
        if (record.Sequence != expectedSequence)
        {
            return JournalProblem.SequenceGap;
        }

        if (!record.PreviousHash.SequenceEqual(previousHash))
        {
            return JournalProblem.BrokenChain;
        }

        return record.StoredHash.SequenceEqual(record.ComputeHash()) ? JournalProblem.None : JournalProblem.HashMismatch;
    }
}
