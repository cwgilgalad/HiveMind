namespace HiveMind.Core.Crypto;

/// <summary>The encrypted stores that each get their own key (spec section 7.9).</summary>
public enum StoreKind
{
    /// <summary>The audit journal.</summary>
    Journal,

    /// <summary>The search index.</summary>
    Index,

    /// <summary>Registry snapshots.</summary>
    Snapshots,

    /// <summary>Change captures.</summary>
    Captures,

    /// <summary>The user's notes.</summary>
    Notes,

    /// <summary>Unsaved drafts.</summary>
    Drafts,
}
