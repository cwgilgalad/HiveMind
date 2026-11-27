namespace HiveMind.Core.Crypto;

/// <summary>
/// The master key exists but can't be used: the file is damaged or unreadable, or it can't be
/// unprotected, for example after a Windows profile reset. HiveMind never replaces the key in that
/// case, because a new key would orphan all encrypted history; the way back is recovery.
/// </summary>
public sealed class MasterKeyUnavailableException : Exception
{
    /// <summary>Creates the exception with a message and, optionally, the error that caused it.</summary>
    public MasterKeyUnavailableException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
