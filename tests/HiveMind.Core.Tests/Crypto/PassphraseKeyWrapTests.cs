using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class PassphraseKeyWrapTests
{
    private const string Passphrase = "correct horse battery staple";

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
}
