namespace HiveMind.Core.Journal;

/// <summary>
/// A journal segment failed its checks: its header is damaged, or a record is cut short, missing,
/// out of order, unlinked or altered. HiveMind never appends to such a segment, because new records
/// would hide where the damage starts.
/// </summary>
public sealed class JournalCorruptException : Exception
{
    /// <summary>Creates the exception.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="problem">The first problem found.</param>
    /// <param name="lastGoodSequence">The sequence number of the last valid record, or 0 if there is none.</param>
    /// <param name="innerException">The error that caused this one, if any.</param>
    public JournalCorruptException(string message, JournalProblem problem, ulong lastGoodSequence, Exception? innerException = null)
        : base(message, innerException)
    {
        Problem = problem;
        LastGoodSequence = lastGoodSequence;
    }

    /// <summary>The first problem found.</summary>
    public JournalProblem Problem { get; }

    /// <summary>The sequence number of the last valid record, or 0 if there is none.</summary>
    public ulong LastGoodSequence { get; }
}
