namespace HiveMind.Core.Crypto;

/// <summary>
/// Protects a secret at rest, as DPAPI does for the master key (spec section 7.9).
/// </summary>
public interface IKeyProtector
{
    /// <summary>
    /// A short ASCII name for the protection, such as <c>dpapi-user</c>. It is stored with the
    /// protected key, so a key protected one way is never handed to a different protector.
    /// </summary>
    string Id { get; }

    /// <summary>Protects <paramref name="secret"/> and returns the protected blob.</summary>
    byte[] Protect(ReadOnlySpan<byte> secret);

    /// <summary>Recovers the secret from a blob that <see cref="Protect"/> returned.</summary>
    byte[] Unprotect(ReadOnlySpan<byte> protectedData);
}
