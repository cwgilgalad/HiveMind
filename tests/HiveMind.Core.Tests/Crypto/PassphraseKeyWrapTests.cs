using System.Buffers.Binary;
using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class PassphraseKeyWrapTests
{
    private const string Passphrase = "correct horse battery staple";

    // The wrapped layout: HMPW, version, int32 LE iterations, 16 salt, 12 nonce, ciphertext, 16 tag.
    private const int IterationsOffset = 5;
    private const int CiphertextOffset = IterationsOffset + 4 + 16 + 12;
    private const int MinimumLength = CiphertextOffset + 16;

    // A well-formed header over a zero-filled body, for the checks that run before PBKDF2.
    private static byte[] Synthetic(int length = MinimumLength + 32, int iterations = PassphraseKeyWrap.MinimumIterations)
    {
        var data = new byte[length];
        "HMPW"u8.CopyTo(data);
        data[4] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(IterationsOffset), iterations);
        return data;
    }

    [Fact]
    public void Wrap_ThenUnwrap_RoundTrips()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var wrapped = PassphraseKeyWrap.Wrap(secret, Passphrase);
        Assert.Equal(secret, PassphraseKeyWrap.Unwrap(wrapped, Passphrase));
    }

    [Fact]
    public void Unwrap_WrongPassphrase_ThrowsCryptographicException()
    {
        var wrapped = PassphraseKeyWrap.Wrap(RandomNumberGenerator.GetBytes(32), Passphrase);
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(wrapped, "wrong horse battery staple"));
    }

    [Fact]
    public void Wrap_IterationsBelowMinimum_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PassphraseKeyWrap.Wrap(new byte[32], Passphrase, iterations: 599_999));

    [Fact]
    public void Wrapped_StartsWithMagicAndVersion()
    {
        var wrapped = PassphraseKeyWrap.Wrap(new byte[32], Passphrase);
        Assert.Equal("HMPW"u8.ToArray(), wrapped[..4]);
        Assert.Equal(1, wrapped[4]);
    }

    [Fact]
    public void MasterKeyBackup_ExportThenImport_RestoresSameKey()
    {
        using var key = MasterKey.Generate();
        var backup = MasterKeyBackup.Export(key, Passphrase);
        using var restored = MasterKeyBackup.Import(backup, Passphrase);
        Assert.Equal(KeyDerivation.DeriveStoreKey(key, StoreKind.Journal), KeyDerivation.DeriveStoreKey(restored, StoreKind.Journal));
    }

    [Fact]
    public void Unwrap_TooShort_Throws() =>
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(Synthetic(length: MinimumLength - 1), Passphrase));

    [Fact]
    public void Unwrap_WrongMagic_Throws()
    {
        var data = Synthetic();
        data[3] = (byte)'X';
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(data, Passphrase));
    }

    [Fact]
    public void Unwrap_UnknownVersion_Throws()
    {
        var data = Synthetic();
        data[4] = 2;
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(data, Passphrase));
    }

    [Fact]
    public void Unwrap_IterationsBelowMinimum_Throws() =>
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(Synthetic(iterations: 599_999), Passphrase));

    [Fact]
    public void Unwrap_IterationsAboveMaximum_Throws() =>
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(Synthetic(iterations: 10_000_001), Passphrase));

    [Fact]
    public void Unwrap_TamperedCiphertext_Throws()
    {
        var wrapped = PassphraseKeyWrap.Wrap(RandomNumberGenerator.GetBytes(32), Passphrase);
        wrapped[CiphertextOffset] ^= 0x01;
        Assert.Throws<CryptographicException>(() => PassphraseKeyWrap.Unwrap(wrapped, Passphrase));
    }

    [Fact]
    public void Wrap_IterationsAboveMaximum_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PassphraseKeyWrap.Wrap(new byte[32], Passphrase, iterations: 10_000_001));

    [Fact]
    public void Wrap_EmptyPassphrase_Throws() =>
        Assert.Throws<ArgumentException>(() => PassphraseKeyWrap.Wrap(new byte[32], string.Empty));

    [Fact]
    public void MasterKeyBackup_Import_NotABackup_Throws() =>
        Assert.Throws<CryptographicException>(() => MasterKeyBackup.Import(new byte[MinimumLength + 32], Passphrase));

    [Fact]
    public void MasterKeyBackup_Import_WrappedSecretNot32Bytes_Throws()
    {
        var wrapped = PassphraseKeyWrap.Wrap(new byte[31], Passphrase);
        Assert.Throws<CryptographicException>(() => MasterKeyBackup.Import(wrapped, Passphrase));
    }

    [Fact]
    public void MasterKeyBackup_Export_EmptyPassphrase_Throws()
    {
        using var key = MasterKey.Generate();
        Assert.Throws<ArgumentException>(() => MasterKeyBackup.Export(key, string.Empty));
    }
}
