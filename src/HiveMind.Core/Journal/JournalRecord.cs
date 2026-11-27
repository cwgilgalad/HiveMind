namespace HiveMind.Core.Journal;

/// <summary>
/// One record of a journal segment as stored: the clear header, the sealed body and the hash that
/// links it into the chain.
/// </summary>
/// <param name="Sequence">The record's number in its segment, counting from 1.</param>
/// <param name="TimestampUtc">When the record was appended, in UTC.</param>
/// <param name="Action">The kind of action.</param>
/// <param name="ActorSid">The SID of the user who acted.</param>
/// <param name="Elevated">Whether the action ran elevated.</param>
/// <param name="PreviousHash">The previous record's hash, or the segment header's previous-segment hash for record 1.</param>
/// <param name="EncryptedBody">The <see cref="JournalBody"/>, sealed with the journal key.</param>
/// <param name="Hash">SHA-256 over the record from its sequence number through the end of its body.</param>
/// <param name="FileOffset">Where the record starts in the segment file: the offset of its length field.</param>
/// <param name="TotalLength">The record's size in the file, including its length field.</param>
public sealed record JournalRecord(
    ulong Sequence,
    DateTimeOffset TimestampUtc,
    JournalAction Action,
    string ActorSid,
    bool Elevated,
    byte[] PreviousHash,
    byte[] EncryptedBody,
    byte[] Hash,
    long FileOffset,
    int TotalLength);
