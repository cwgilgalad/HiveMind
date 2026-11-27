using HiveMind.Core.Crypto;
using HiveMind.Testing;

namespace HiveMind.Core.Tests.Crypto;

public sealed class MasterKeyStoreTests
{
    private static byte[] Fingerprint(MasterKey key) => KeyDerivation.DeriveStoreKey(key, StoreKind.Journal);

    [Fact]
    public void LoadOrCreate_MissingFile_CreatesKeyThatReloadsIdentically()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "keys", "masterkey.bin");
        using var created = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        using var reloaded = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        Assert.Equal(Fingerprint(created), Fingerprint(reloaded));
        Assert.Equal("HMMK"u8.ToArray(), File.ReadAllBytes(path)[..4]);
    }

    [Fact]
    public void LoadOrCreate_CorruptFile_ThrowsAndLeavesFileUntouched()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        File.WriteAllBytes(path, [0x48, 0x4D, 0x00]);
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()));
        Assert.Equal(new byte[] { 0x48, 0x4D, 0x00 }, File.ReadAllBytes(path));
    }

    [Fact]
    public void LoadOrCreate_UnprotectFails_ThrowsMasterKeyUnavailable()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()).Dispose();
        var before = File.ReadAllBytes(path);
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector(failUnprotect: true)));
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void LoadOrCreate_DifferentProtectorId_Throws()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector("dpapi-user")).Dispose();
        Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector("dpapi-machine")));
    }

    [Fact]
    public void LoadOrCreate_FileLockedByAnotherHandle_ThrowsAndLeavesFileUntouched()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "masterkey.bin");
        MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()).Dispose();
        var before = File.ReadAllBytes(path);
        using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.Throws<MasterKeyUnavailableException>(() => MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector()));
        }

        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void LoadOrCreate_UnicodePathWithSpaces_Works()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "José Müller", "key store", "masterkey.bin");
        using var key = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
        Assert.True(File.Exists(path));
    }

    [Fact]
    public void LoadOrCreate_LongPath_Works()
    {
        using var dir = new TempDirectory();
        var folder = dir.Path;
        while (folder.Length < 300) folder = Path.Combine(folder, "nested-folder-segment");
        using var key = MasterKeyStore.LoadOrCreate(Path.Combine(folder, "masterkey.bin"), new FakeKeyProtector());
        Assert.True(File.Exists(Path.Combine(folder, "masterkey.bin")));
    }

    [Fact]
    public void LoadOrCreate_KeyFileCreatedWhileSaving_LoadsTheOtherKey()
    {
        using var dir = new TempDirectory();
        var folder = Path.Combine(dir.Path, "keys");
        var path = Path.Combine(folder, "masterkey.bin");
        var racer = new RacingKeyProtector(path);

        using var loaded = MasterKeyStore.LoadOrCreate(path, racer);
        using var reloaded = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());

        Assert.NotNull(racer.OtherFingerprint);
        Assert.Equal(racer.OtherFingerprint, Fingerprint(loaded));
        Assert.Equal(racer.OtherFingerprint, Fingerprint(reloaded));
        Assert.Equal(["masterkey.bin"], Directory.GetFiles(folder).Select(Path.GetFileName));
    }

    // Behaves like FakeKeyProtector, but its first Protect call stands in for another process that
    // creates the key file at the same path while the outer LoadOrCreate is still saving its own.
    private sealed class RacingKeyProtector(string path) : IKeyProtector
    {
        private readonly FakeKeyProtector _inner = new();
        private bool _raced;

        public string Id => "fake";

        public byte[]? OtherFingerprint { get; private set; }

        public byte[] Protect(ReadOnlySpan<byte> secret)
        {
            if (!_raced)
            {
                _raced = true;
                using var other = MasterKeyStore.LoadOrCreate(path, new FakeKeyProtector());
                OtherFingerprint = Fingerprint(other);
            }

            return _inner.Protect(secret);
        }

        public byte[] Unprotect(ReadOnlySpan<byte> protectedData) => _inner.Unprotect(protectedData);
    }
}
