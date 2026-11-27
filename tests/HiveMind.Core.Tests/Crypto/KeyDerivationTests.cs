using System.Security.Cryptography;
using HiveMind.Core.Crypto;

namespace HiveMind.Core.Tests.Crypto;

public sealed class KeyDerivationTests
{
    [Fact]
    public void DeriveStoreKey_MatchesHkdfWithPinnedLabel()
    {
        var ikm = Enumerable.Repeat((byte)0x01, 32).ToArray();
        using var key = MasterKey.FromBytes(ikm);
        var expected = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 32, salt: [], info: "hivemind/v1/store/journal"u8.ToArray());
        Assert.Equal(expected, KeyDerivation.DeriveStoreKey(key, StoreKind.Journal));
    }

    [Fact]
    public void DeriveStoreKey_EachStore_IsDistinct()
    {
        using var key = MasterKey.Generate();
        var keys = Enum.GetValues<StoreKind>().Select(s => Convert.ToHexString(KeyDerivation.DeriveStoreKey(key, s)));
        Assert.Equal(6, keys.Distinct().Count());
    }
}
