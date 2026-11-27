using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>
/// The optional, user-initiated master-key backup, protected by a recovery passphrase (spec section
/// 7.9). Without one, resetting the Windows profile loses history. The backup is
/// <see cref="PassphraseKeyWrap"/> over the 32 key bytes.
/// </summary>
public static class MasterKeyBackup
{
    /// <summary>Encrypts <paramref name="masterKey"/> under <paramref name="recoveryPassphrase"/>.</summary>
    /// <exception cref="ObjectDisposedException"><paramref name="masterKey"/> has been disposed.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="recoveryPassphrase"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="recoveryPassphrase"/> is empty.</exception>
    public static byte[] Export(MasterKey masterKey, string recoveryPassphrase)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        return PassphraseKeyWrap.Wrap(masterKey.Bytes, recoveryPassphrase);
    }

    /// <summary>Restores the master key from a backup that <see cref="Export"/> made.</summary>
    /// <exception cref="CryptographicException">
    /// The backup is damaged or isn't a master-key backup, or the passphrase is wrong.
    /// </exception>
    public static MasterKey Import(ReadOnlySpan<byte> backup, string recoveryPassphrase)
    {
        var keyBytes = PassphraseKeyWrap.Unwrap(backup, recoveryPassphrase);
        try
        {
            if (keyBytes.Length != MasterKey.SizeInBytes)
            {
                throw new CryptographicException($"The backup holds {keyBytes.Length} bytes, not a {MasterKey.SizeInBytes}-byte master key.");
            }

            return MasterKey.FromBytes(keyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }
}
