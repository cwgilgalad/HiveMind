using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class RecordCipherTests
{
    private static ReadOnlySpan<byte> AssociatedData => "record #7"u8;

    [Fact]
    public void SealThenOpen_RoundTrips()
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        var plaintext = RandomNumberGenerator.GetBytes(100);
        Assert.Equal(plaintext, cipher.Open(cipher.Seal(plaintext, AssociatedData), AssociatedData));
    }

    [Fact]
    public void Open_WrongAssociatedData_Throws()
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        var sealedRecord = cipher.Seal(RandomNumberGenerator.GetBytes(100), AssociatedData);
        Assert.ThrowsAny<CryptographicException>(() => cipher.Open(sealedRecord, "record #8"u8));
    }

    [Fact]
    public void Open_FlippedByte_Throws()
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        var sealedRecord = cipher.Seal(RandomNumberGenerator.GetBytes(100), AssociatedData);
        sealedRecord[20] ^= 0x01;
        Assert.ThrowsAny<CryptographicException>(() => cipher.Open(sealedRecord, AssociatedData));
    }

    [Fact]
    public void Seal_SameInputTwice_UsesDifferentNonces()
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        var plaintext = RandomNumberGenerator.GetBytes(100);
        var first = cipher.Seal(plaintext, AssociatedData);
        var second = cipher.Seal(plaintext, AssociatedData);
        Assert.NotEqual(first[..12], second[..12]);
        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    public void Constructor_KeyNot32Bytes_Throws(int length) =>
        Assert.Throws<ArgumentException>(() => new RecordCipher(new byte[length]));

    [Fact]
    public void Open_ShorterThanOverhead_Throws()
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        Assert.ThrowsAny<CryptographicException>(() => cipher.Open(new byte[RecordCipher.Overhead - 1], AssociatedData));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(1000)]
    public void SealedLength_IsPlaintextPlus28(int length)
    {
        using var cipher = new RecordCipher(RandomNumberGenerator.GetBytes(32));
        Assert.Equal(length + 28, cipher.Seal(new byte[length], AssociatedData).Length);
    }
}
