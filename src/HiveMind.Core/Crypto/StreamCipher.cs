using System.Buffers.Binary;
using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>
/// Encrypts streams too large for one record, such as snapshots and captures, in AES-256-GCM chunks
/// (spec section 7.9). Each chunk's index and a final-chunk flag are authenticated, so truncation,
/// reordering and trailing bytes are all detected.
/// </summary>
/// <remarks>
/// <para>
/// The output starts with a 41-byte header: ASCII <c>HMS1</c>, version byte 1, the chunk size (int32
/// little-endian, 1,024 to 16,777,216) and 32 random salt bytes. The stream key is HKDF-SHA256 over the
/// store key with that salt and the info <c>hivemind/v1/stream</c>, so every stream has its own key.
/// </para>
/// <para>
/// Chunk <c>i</c>, counting from 0, uses the nonce of 4 zero bytes then <c>i</c> as a uint64
/// big-endian. Its associated data is the header, <c>i</c> as a uint64 big-endian, and a flag byte that
/// is 1 for the final chunk and 0 otherwise. It is stored as the ciphertext followed by a 16-byte tag.
/// Every chunk but the final one holds exactly the chunk size in plaintext; when the plaintext length
/// is a multiple of the chunk size, including 0, the final chunk is empty.
/// </para>
/// <para>Neither method closes, disposes or seeks the streams it is given, and both work on streams that can't seek.</para>
/// </remarks>
public static class StreamCipher
{
    /// <summary>The chunk size <see cref="Encrypt"/> uses unless told otherwise: 64 KiB.</summary>
    public const int DefaultChunkSize = 65_536;

    private const int MinimumChunkSize = 1_024;
    private const int MaximumChunkSize = 16_777_216;
    private const byte FormatVersion = 1;
    private const int ChunkSizeOffset = 4 + 1;
    private const int SaltOffset = ChunkSizeOffset + sizeof(int);
    private const int SaltSize = 32;
    private const int HeaderSize = SaltOffset + SaltSize;
    private const int KeySize = 32;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int AssociatedDataSize = HeaderSize + sizeof(ulong) + 1;

    private static ReadOnlySpan<byte> Magic => "HMS1"u8;

    private static ReadOnlySpan<byte> StreamKeyInfo => "hivemind/v1/stream"u8;

    /// <summary>
    /// Reads <paramref name="plaintext"/> to its end and writes it, encrypted under a fresh key derived
    /// from <paramref name="storeKey"/>, to <paramref name="ciphertext"/>.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="storeKey"/> is not 32 bytes long.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="chunkSize"/> is outside 1,024 to 16,777,216.</exception>
    public static void Encrypt(Stream plaintext, Stream ciphertext, ReadOnlySpan<byte> storeKey, int chunkSize = DefaultChunkSize)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(ciphertext);
        CheckStoreKey(storeKey);
        ArgumentOutOfRangeException.ThrowIfLessThan(chunkSize, MinimumChunkSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(chunkSize, MaximumChunkSize);

        Span<byte> associatedData = stackalloc byte[AssociatedDataSize];
        var header = associatedData[..HeaderSize];
        Magic.CopyTo(header);
        header[4] = FormatVersion;
        BinaryPrimitives.WriteInt32LittleEndian(header.Slice(ChunkSizeOffset, sizeof(int)), chunkSize);
        RandomNumberGenerator.Fill(header.Slice(SaltOffset, SaltSize));
        ciphertext.Write(header);

        Span<byte> nonce = stackalloc byte[NonceSize];
        Span<byte> streamKey = stackalloc byte[KeySize];
        var buffer = new byte[chunkSize];
        var output = new byte[chunkSize + TagSize];
        try
        {
            DeriveStreamKey(storeKey, header, streamKey);
            using var aes = new AesGcm(streamKey, TagSize);
            for (ulong index = 0; ; index++)
            {
                // A short read, including an empty one, means the plaintext has ended: that chunk is final.
                var length = plaintext.ReadAtLeast(buffer, chunkSize, throwOnEndOfStream: false);
                var isFinal = length < chunkSize;
                PrepareChunk(index, isFinal, nonce, associatedData);
                aes.Encrypt(nonce, buffer.AsSpan(0, length), output.AsSpan(0, length), output.AsSpan(length, TagSize), associatedData);
                ciphertext.Write(output, 0, length + TagSize);
                if (isFinal)
                {
                    return;
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(streamKey);
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    /// <summary>
    /// Reads a stream that <see cref="Encrypt"/> wrote from <paramref name="ciphertext"/> to its end, and
    /// writes the decrypted plaintext to <paramref name="plaintext"/>.
    /// </summary>
    /// <remarks>
    /// Chunks are written as they authenticate, so if this method throws, any plaintext already written
    /// to <paramref name="plaintext"/> is unauthenticated and must be discarded: the stream may have been
    /// truncated or tampered with after that point. Decrypt to a temporary destination and keep it only
    /// once this method returns.
    /// </remarks>
    /// <exception cref="ArgumentException"><paramref name="storeKey"/> is not 32 bytes long.</exception>
    /// <exception cref="CryptographicException">
    /// The header is missing, damaged or of an unknown version, the stream is truncated, chunks were
    /// reordered or changed, bytes follow the final chunk, or the key is wrong.
    /// </exception>
    public static void Decrypt(Stream ciphertext, Stream plaintext, ReadOnlySpan<byte> storeKey)
    {
        ArgumentNullException.ThrowIfNull(ciphertext);
        ArgumentNullException.ThrowIfNull(plaintext);
        CheckStoreKey(storeKey);

        Span<byte> associatedData = stackalloc byte[AssociatedDataSize];
        var header = associatedData[..HeaderSize];
        var chunkSize = ReadHeader(ciphertext, header);

        Span<byte> nonce = stackalloc byte[NonceSize];
        Span<byte> streamKey = stackalloc byte[KeySize];
        var stored = new byte[chunkSize + TagSize];
        var buffer = new byte[chunkSize];
        try
        {
            DeriveStreamKey(storeKey, header, streamKey);
            using var aes = new AesGcm(streamKey, TagSize);
            for (ulong index = 0; ; index++)
            {
                // A full stored chunk is never final in this format; a short read is the final chunk.
                // A stream cut at a chunk boundary ends in a full chunk and then nothing, which fails here.
                var read = ciphertext.ReadAtLeast(stored, stored.Length, throwOnEndOfStream: false);
                if (read < TagSize)
                {
                    throw new CryptographicException("The encrypted stream is truncated.");
                }

                var isFinal = read < stored.Length;
                var length = read - TagSize;
                PrepareChunk(index, isFinal, nonce, associatedData);
                aes.Decrypt(nonce, stored.AsSpan(0, length), stored.AsSpan(length, TagSize), buffer.AsSpan(0, length), associatedData);
                plaintext.Write(buffer, 0, length);
                if (isFinal)
                {
                    return;
                }
            }
        }
        catch (AuthenticationTagMismatchException ex)
        {
            throw new CryptographicException("The encrypted stream is damaged, truncated or reordered, or the key is wrong.", ex);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(streamKey);
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    // Reads and checks the header into the given span and returns the chunk size. A chunk size that
    // was changed but is still in range passes here and fails authentication, since every chunk's
    // associated data includes the header.
    private static int ReadHeader(Stream ciphertext, Span<byte> header)
    {
        if (ciphertext.ReadAtLeast(header, HeaderSize, throwOnEndOfStream: false) < HeaderSize)
        {
            throw new CryptographicException("The encrypted stream is truncated: its header is incomplete.");
        }

        if (!header[..4].SequenceEqual(Magic))
        {
            throw new CryptographicException("The data is not a HiveMind encrypted stream.");
        }

        if (header[4] != FormatVersion)
        {
            throw new CryptographicException($"The encrypted stream uses format version {header[4]}, which this version of HiveMind can't read.");
        }

        var chunkSize = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(ChunkSizeOffset, sizeof(int)));
        if (chunkSize is < MinimumChunkSize or > MaximumChunkSize)
        {
            throw new CryptographicException($"The encrypted stream's header gives a chunk size of {chunkSize}, outside {MinimumChunkSize} to {MaximumChunkSize}.");
        }

        return chunkSize;
    }

    private static void CheckStoreKey(ReadOnlySpan<byte> storeKey)
    {
        if (storeKey.Length != KeySize)
        {
            throw new ArgumentException($"A store key is exactly {KeySize} bytes, not {storeKey.Length}.", nameof(storeKey));
        }
    }

    private static void DeriveStreamKey(ReadOnlySpan<byte> storeKey, ReadOnlySpan<byte> header, Span<byte> streamKey) =>
        HKDF.DeriveKey(HashAlgorithmName.SHA256, storeKey, streamKey, header.Slice(SaltOffset, SaltSize), StreamKeyInfo);

    // Sets the nonce (4 zero bytes, then the index) and the index and final flag that follow the
    // header in the associated data.
    private static void PrepareChunk(ulong index, bool isFinal, Span<byte> nonce, Span<byte> associatedData)
    {
        nonce[..4].Clear();
        BinaryPrimitives.WriteUInt64BigEndian(nonce[4..], index);
        BinaryPrimitives.WriteUInt64BigEndian(associatedData.Slice(HeaderSize, sizeof(ulong)), index);
        associatedData[^1] = isFinal ? (byte)1 : (byte)0;
    }
}
