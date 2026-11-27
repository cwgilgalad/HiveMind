using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class MasterKeyTests
{
    private static byte[] Fingerprint(MasterKey key) => KeyDerivation.DeriveStoreKey(key, StoreKind.Journal);

    [Fact]
    public void Generate_TwoKeys_Differ()
    {
        using var first = MasterKey.Generate();
        using var second = MasterKey.Generate();
        Assert.NotEqual(Fingerprint(first), Fingerprint(second));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(33)]
    public void FromBytes_WrongLength_Throws(int length) =>
        Assert.Throws<ArgumentException>(() => MasterKey.FromBytes(new byte[length]));

    [Fact]
    public void UseAfterDispose_ThrowsObjectDisposed()
    {
        var key = MasterKey.Generate();
        key.Dispose();
        Assert.Throws<ObjectDisposedException>(() => KeyDerivation.DeriveStoreKey(key, StoreKind.Journal));
    }
}
