using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>
/// The random 256-bit key at the top of the key hierarchy (spec section 7.9). Every store key is
/// derived from it with <see cref="KeyDerivation"/>. The bytes live in a pinned array, so the GC
/// never leaves copies behind, and <see cref="Dispose"/> zeroes them.
/// </summary>
public sealed class MasterKey : IDisposable
{
    /// <summary>The size of a master key: 32 bytes (256 bits).</summary>
    public const int SizeInBytes = 32;

    private readonly byte[] _bytes = GC.AllocateArray<byte>(SizeInBytes, pinned: true);
    private bool _disposed;

    private MasterKey()
    {
    }

    /// <summary>Creates a new key from the system's cryptographic random number generator.</summary>
    public static MasterKey Generate()
    {
        var key = new MasterKey();
        RandomNumberGenerator.Fill(key._bytes);
        return key;
    }

    /// <summary>Creates a key from exactly <see cref="SizeInBytes"/> bytes, which it copies.</summary>
    /// <exception cref="ArgumentException"><paramref name="keyBytes"/> is not 32 bytes long.</exception>
    public static MasterKey FromBytes(ReadOnlySpan<byte> keyBytes)
    {
        if (keyBytes.Length != SizeInBytes)
        {
            throw new ArgumentException($"A master key is exactly {SizeInBytes} bytes, not {keyBytes.Length}.", nameof(keyBytes));
        }

        var key = new MasterKey();
        keyBytes.CopyTo(key._bytes);
        return key;
    }

    /// <summary>The key bytes, for the crypto types in this assembly only. Never copy them out.</summary>
    /// <exception cref="ObjectDisposedException">The key has been disposed.</exception>
    internal ReadOnlySpan<byte> Bytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _bytes;
        }
    }

    /// <summary>Zeroes the key bytes. Any later use throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            CryptographicOperations.ZeroMemory(_bytes);
            _disposed = true;
        }
    }
}
