using System.Buffers.Binary;
using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class StreamCipherTests
{
    private const int Chunk = 1024, Header = 41, Stored = Chunk + 16;
    private static byte[] Enc(byte[] data, byte[] key) { using var o = new MemoryStream(); StreamCipher.Encrypt(new MemoryStream(data), o, key, Chunk); return o.ToArray(); }
    private static byte[] Dec(byte[] data, byte[] key) { using var o = new MemoryStream(); StreamCipher.Decrypt(new MemoryStream(data), o, key); return o.ToArray(); }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(1023)] [InlineData(1024)] [InlineData(1025)] [InlineData(3072)]
    public void EncryptThenDecrypt_RoundTrips(int length)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var data = RandomNumberGenerator.GetBytes(length);
        Assert.Equal(data, Dec(Enc(data, key), key));
    }

    [Fact]
    public void Decrypt_TruncatedAtChunkBoundary_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        Assert.ThrowsAny<CryptographicException>(() => Dec(ct[..(Header + 2 * Stored)], key));
    }

    [Fact]
    public void Decrypt_SwappedChunks_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        var swapped = ct[..Header].Concat(ct[(Header + Stored)..(Header + 2 * Stored)]).Concat(ct[Header..(Header + Stored)]).Concat(ct[(Header + 2 * Stored)..]).ToArray();
        Assert.ThrowsAny<CryptographicException>(() => Dec(swapped, key));
    }

    [Fact]
    public void Decrypt_FlippedBit_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        ct[Header + Stored + 7] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => Dec(ct, key));
    }

    [Fact]
    public void Decrypt_TrailingGarbage_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        Assert.ThrowsAny<CryptographicException>(() => Dec([.. ct, 0x00, 0x01, 0x02], key));
    }

    [Fact]
    public void Decrypt_TamperedHeaderChunkSize_Throws()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), key);
        BinaryPrimitives.WriteInt32LittleEndian(ct.AsSpan(5), 2 * Chunk);
        Assert.ThrowsAny<CryptographicException>(() => Dec(ct, key));
    }

    [Theory]
    [InlineData("cut short")]
    [InlineData("wrong magic")]
    [InlineData("unknown version")]
    [InlineData("chunk size too small")]
    [InlineData("chunk size too large")]
    public void Decrypt_MalformedHeader_Throws(string damage)
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var ct = Enc([], key);
        switch (damage)
        {
            case "cut short": ct = ct[..(Header - 1)]; break;
            case "wrong magic": ct[3] = (byte)'X'; break;
            case "unknown version": ct[4] = 2; break;
            case "chunk size too small": BinaryPrimitives.WriteInt32LittleEndian(ct.AsSpan(5), 1023); break;
            case "chunk size too large": BinaryPrimitives.WriteInt32LittleEndian(ct.AsSpan(5), 16_777_217); break;
        }

        Assert.ThrowsAny<CryptographicException>(() => Dec(ct, key));
    }

    [Fact]
    public void Decrypt_WrongKey_Throws()
    {
        var ct = Enc(RandomNumberGenerator.GetBytes(3072), RandomNumberGenerator.GetBytes(32));
        Assert.ThrowsAny<CryptographicException>(() => Dec(ct, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void Encrypt_SameInputTwice_DiffersBecauseOfSalt()
    {
        var key = RandomNumberGenerator.GetBytes(32);
        var data = RandomNumberGenerator.GetBytes(3072);
        var first = Enc(data, key);
        var second = Enc(data, key);
        Assert.NotEqual(first[9..Header], second[9..Header]);
        Assert.NotEqual(first[Header..], second[Header..]);
    }

    [Theory]
    [InlineData(1023)]
    [InlineData(16_777_217)]
    public void Encrypt_ChunkSizeOutOfRange_Throws(int chunkSize)
    {
        using var output = new MemoryStream();
        using var input = new MemoryStream([1, 2, 3]);
        Assert.Throws<ArgumentOutOfRangeException>(() => StreamCipher.Encrypt(input, output, new byte[32], chunkSize));
    }
}
