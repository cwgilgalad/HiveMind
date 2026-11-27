using System.Security.Cryptography;

namespace HiveMind.Core.Crypto;

/// <summary>Derives the per-store keys from the master key with HKDF-SHA256 (spec section 7.9).</summary>
public static class KeyDerivation
{
    private const int StoreKeySizeInBytes = 32;

    /// <summary>
    /// Derives the 32-byte key for <paramref name="store"/>: HKDF-SHA256 over the master key, with an
    /// empty salt and the info <c>hivemind/v1/store/&lt;store in lowercase&gt;</c>.
    /// </summary>
    /// <exception cref="ObjectDisposedException"><paramref name="masterKey"/> has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="store"/> is not a defined store.</exception>
    public static byte[] DeriveStoreKey(MasterKey masterKey, StoreKind store)
    {
        ArgumentNullException.ThrowIfNull(masterKey);
        var info = Info(store);
        var storeKey = new byte[StoreKeySizeInBytes];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, masterKey.Bytes, storeKey, salt: [], info);
        return storeKey;
    }

    // The labels are spelled out rather than taken from the enum names, so renaming a StoreKind
    // member can never silently change a store's key.
    private static ReadOnlySpan<byte> Info(StoreKind store) => store switch
    {
        StoreKind.Journal => "hivemind/v1/store/journal"u8,
        StoreKind.Index => "hivemind/v1/store/index"u8,
        StoreKind.Snapshots => "hivemind/v1/store/snapshots"u8,
        StoreKind.Captures => "hivemind/v1/store/captures"u8,
        StoreKind.Notes => "hivemind/v1/store/notes"u8,
        StoreKind.Drafts => "hivemind/v1/store/drafts"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(store), store, "There is no such store."),
    };
}
