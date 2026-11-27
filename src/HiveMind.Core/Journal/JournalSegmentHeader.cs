namespace HiveMind.Core.Journal;

/// <summary>The header at the start of every journal segment file.</summary>
/// <param name="SegmentId">A random identifier for the segment.</param>
/// <param name="PreviousSegmentHash">The last hash of the segment before this one, or 32 zero bytes if there was none.</param>
/// <param name="CreatedUtc">When the segment was created, in UTC.</param>
public sealed record JournalSegmentHeader(Guid SegmentId, byte[] PreviousSegmentHash, DateTimeOffset CreatedUtc);
