using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>
/// Encrypts small records, such as journal bodies, with AES-256-GCM under one store key (spec section
/// 7.9). Each sealed record is a random 12-byte nonce, the ciphertext and a 16-byte tag. An instance
/// isn't safe to use from several threads at once.
/// </summary>
/// <remarks>
/// Because the nonces are random, seal at most 2^32 records under one key. Past that, the chance of
/// two records sharing a nonce, which breaks AES-GCM, grows beyond the accepted bound.
/// </remarks>
public sealed class RecordCipher : IDisposable
{
    /// <summary>The bytes a sealed record adds to its plaintext: a 12-byte nonce and a 16-byte tag.</summary>
    public const int Overhead = NonceSize + TagSize;

    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly AesGcm _aes;
    private bool _disposed;

    /// <summary>Creates a cipher for a 32-byte store key, which it copies.</summary>
    /// <exception cref="ArgumentException"><paramref name="key"/> is not 32 bytes long.</exception>
    public RecordCipher(ReadOnlySpan<byte> key)
    {
        if (key.Length != KeySize)
        {
            throw new ArgumentException($"A record key is exactly {KeySize} bytes, not {key.Length}.", nameof(key));
        }

        _aes = new AesGcm(key, TagSize);
    }

    /// <summary>
    /// Encrypts <paramref name="plaintext"/> under a fresh random nonce and authenticates it together
    /// with <paramref name="associatedData"/>, which is not stored.
    /// </summary>
    /// <returns>The nonce, the ciphertext and the tag: <see cref="Overhead"/> bytes more than the plaintext.</returns>
    /// <exception cref="ObjectDisposedException">The cipher has been disposed.</exception>
    public byte[] Seal(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var output = new byte[Overhead + plaintext.Length];
        var span = output.AsSpan();
        var nonce = span[..NonceSize];
        RandomNumberGenerator.Fill(nonce);
        _aes.Encrypt(nonce, plaintext, span[NonceSize..^TagSize], span[^TagSize..], associatedData);
        return output;
    }

    /// <summary>
    /// Decrypts a record that <see cref="Seal"/> made, checking it against the same
    /// <paramref name="associatedData"/>.
    /// </summary>
    /// <exception cref="CryptographicException">
    /// The record is shorter than <see cref="Overhead"/>, or it doesn't authenticate: it was changed,
    /// the associated data differs, or the key is wrong.
    /// </exception>
    /// <exception cref="ObjectDisposedException">The cipher has been disposed.</exception>
    public byte[] Open(ReadOnlySpan<byte> sealedRecord, ReadOnlySpan<byte> associatedData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (sealedRecord.Length < Overhead)
        {
            throw new CryptographicException($"The sealed record is {sealedRecord.Length} bytes, shorter than the {Overhead} bytes of nonce and tag.");
        }

        var plaintext = new byte[sealedRecord.Length - Overhead];
        _aes.Decrypt(sealedRecord[..NonceSize], sealedRecord[NonceSize..^TagSize], sealedRecord[^TagSize..], plaintext, associatedData);
        return plaintext;
    }

    /// <summary>Releases the key. Any later <see cref="Seal"/> or <see cref="Open"/> throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _aes.Dispose();
            _disposed = true;
        }
    }
}
