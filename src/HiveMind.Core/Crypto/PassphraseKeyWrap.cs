using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>
/// Encrypts a secret under a passphrase: PBKDF2-HMAC-SHA256 with at least 600,000 iterations, then
/// AES-256-GCM (spec section 7.9). The output holds ASCII <c>HMPW</c>, version byte 1, the
/// iterations (int32 little-endian), a 16-byte salt, a 12-byte nonce, the ciphertext and a 16-byte
/// tag. Every byte before the nonce is authenticated as associated data.
/// </summary>
public static class PassphraseKeyWrap
{
    /// <summary>The fewest PBKDF2 iterations HiveMind accepts, for wrapping and for unwrapping.</summary>
    public const int MinimumIterations = 600_000;

    private const byte FormatVersion = 1;
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int WrapKeySize = 32;
    private const int IterationsOffset = 4 + 1;
    private const int SaltOffset = IterationsOffset + sizeof(int);
    private const int HeaderSize = SaltOffset + SaltSize;
    private const int MinimumLength = HeaderSize + NonceSize + TagSize;

    private static ReadOnlySpan<byte> Magic => "HMPW"u8;

    /// <summary>Encrypts <paramref name="secret"/> under <paramref name="passphrase"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="iterations"/> is below <see cref="MinimumIterations"/>.</exception>
    public static byte[] Wrap(ReadOnlySpan<byte> secret, string passphrase, int iterations = MinimumIterations)
    {
        ArgumentNullException.ThrowIfNull(passphrase);
        ArgumentOutOfRangeException.ThrowIfLessThan(iterations, MinimumIterations);

        var wrapped = new byte[MinimumLength + secret.Length];
        var span = wrapped.AsSpan();
        Magic.CopyTo(span);
        span[4] = FormatVersion;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(IterationsOffset, sizeof(int)), iterations);
        var salt = span.Slice(SaltOffset, SaltSize);
        RandomNumberGenerator.Fill(salt);
        var nonce = span.Slice(HeaderSize, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        var ciphertext = span.Slice(HeaderSize + NonceSize, secret.Length);
        var tag = span[^TagSize..];

        Span<byte> wrapKey = stackalloc byte[WrapKeySize];
        try
        {
            // This overload of Pbkdf2 encodes the passphrase as UTF-8.
            Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, wrapKey, iterations, HashAlgorithmName.SHA256);
            using var aes = new AesGcm(wrapKey, TagSize);
            aes.Encrypt(nonce, secret, ciphertext, tag, span[..HeaderSize]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }

        return wrapped;
    }

    /// <summary>Recovers the secret that <see cref="Wrap"/> encrypted under <paramref name="passphrase"/>.</summary>
    /// <exception cref="CryptographicException">
    /// The data isn't passphrase-wrapped, it asks for fewer than <see cref="MinimumIterations"/>
    /// iterations, or the passphrase is wrong or the data damaged (the tag doesn't verify).
    /// </exception>
    public static byte[] Unwrap(ReadOnlySpan<byte> wrapped, string passphrase)
    {
        ArgumentNullException.ThrowIfNull(passphrase);

        if (wrapped.Length < MinimumLength || !wrapped[..4].SequenceEqual(Magic))
        {
            throw new CryptographicException("The data is not a HiveMind passphrase-wrapped secret.");
        }

        if (wrapped[4] != FormatVersion)
        {
            throw new CryptographicException($"The passphrase-wrapped secret uses format version {wrapped[4]}, which this version of HiveMind can't read.");
        }

        var iterations = BinaryPrimitives.ReadInt32LittleEndian(wrapped.Slice(IterationsOffset, sizeof(int)));
        if (iterations < MinimumIterations)
        {
            throw new CryptographicException($"The passphrase-wrapped secret asks for {iterations} PBKDF2 iterations; at least {MinimumIterations} are required.");
        }

        var salt = wrapped.Slice(SaltOffset, SaltSize);
        var nonce = wrapped.Slice(HeaderSize, NonceSize);
        var ciphertext = wrapped[(HeaderSize + NonceSize)..^TagSize];
        var tag = wrapped[^TagSize..];
        var secret = new byte[ciphertext.Length];

        Span<byte> wrapKey = stackalloc byte[WrapKeySize];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(passphrase, salt, wrapKey, iterations, HashAlgorithmName.SHA256);
            using var aes = new AesGcm(wrapKey, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, secret, wrapped[..HeaderSize]);
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException("The passphrase is wrong, or the passphrase-wrapped secret is damaged.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrapKey);
        }

        return secret;
    }
}
