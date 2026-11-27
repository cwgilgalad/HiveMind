namespace HiveMind.Core.Journal;

/// <summary>
/// The kind of action a journal record logs (spec section 7.10). Stored in the clear as a uint16, so
/// it can be filtered and verified without the journal key.
/// </summary>
public enum JournalAction : ushort
{
    /// <summary>A registry write: create, set, rename, delete, or an ACL or owner change.</summary>
    RegistryWrite = 1,

    /// <summary>A use of privilege: the broker starting or stopping, a privilege enabled or disabled.</summary>
    Privilege = 2,

    /// <summary>Data leaving HiveMind: an export, a clipboard copy, a snapshot or a capture.</summary>
    DataMovement = 3,

    /// <summary>A boundary crossed: an offline hive, a remote machine, a plugin or pack, or the network.</summary>
    Boundary = 4,

    /// <summary>A settings or policy change, a key rotation or a data erase.</summary>
    Configuration = 5,

    /// <summary>Navigation, logged only in Verbose audit mode.</summary>
    Navigation = 100,

    /// <summary>A search, logged only in Verbose audit mode.</summary>
    Search = 101,
}
