namespace HiveMind.Core.Journal;

/// <summary>
/// The encrypted part of a journal record: what happened, to what, and why. It is stored as camelCase
/// UTF-8 JSON, without null members, sealed with the journal key.
/// </summary>
/// <param name="Kind">What happened, such as <c>registry.set</c> or <c>broker.start</c>.</param>
/// <param name="Target">What it happened to, such as a key path.</param>
/// <param name="Note">The user's note or ticket reference.</param>
/// <param name="Details">Any further facts, as name-value pairs.</param>
public sealed record JournalBody(string Kind, string? Target = null, string? Note = null, IReadOnlyDictionary<string, string>? Details = null);

/// <summary>An action to append to the journal.</summary>
/// <param name="Action">The kind of action, stored in the clear.</param>
/// <param name="ActorSid">The SID of the user who acted, stored in the clear as 1 to 256 bytes of UTF-8.</param>
/// <param name="Elevated">Whether the action ran elevated, stored in the clear.</param>
/// <param name="Body">The details, stored encrypted.</param>
public sealed record JournalEntry(JournalAction Action, string ActorSid, bool Elevated, JournalBody Body);
