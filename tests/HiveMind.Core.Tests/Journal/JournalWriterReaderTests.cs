using System.Security.Cryptography;
using HiveMind.Core.Journal;
using HiveMind.Testing;
using Microsoft.Extensions.Time.Testing;

namespace HiveMind.Core.Tests.Journal;

public sealed class JournalWriterReaderTests
{
    private const string Sid = "S-1-5-21-1000-2000-3000-1001";

    private static byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Append_ThenRead_ChainsHashesAndEncryptsBody()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "segment-0001.hmj");
        using (var writer = JournalWriter.Create(path, Key))
        {
            writer.Append(new JournalEntry(JournalAction.RegistryWrite, Sid, false, new JournalBody("registry.set", @"HKCU\Software\Contoso", "CHG-1042")));
            writer.Append(new JournalEntry(JournalAction.Privilege, Sid, true, new JournalBody("broker.start")));
        }
        var records = JournalReader.ReadRecords(path).ToList();
        Assert.Equal(new ulong[] { 1, 2 }, records.Select(r => r.Sequence));
        Assert.Equal(new byte[32], records[0].PreviousHash);
        Assert.Equal(records[0].Hash, records[1].PreviousHash);
        Assert.True(records[1].Elevated);
        Assert.Equal(JournalAction.Privilege, records[1].Action);
        Assert.Equal(-1, File.ReadAllBytes(path).AsSpan().IndexOf("CHG-1042"u8));
        Assert.Equal("CHG-1042", JournalReader.DecryptBody(records[0], Key).Note);
    }

    [Fact]
    public void DecryptBody_MovedToAnotherRecord_Throws()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using (var w = JournalWriter.Create(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); w.Append(new(JournalAction.Configuration, Sid, false, new("b"))); }
        var r = JournalReader.ReadRecords(path).ToList();
        Assert.ThrowsAny<CryptographicException>(() => JournalReader.DecryptBody(r[1] with { EncryptedBody = r[0].EncryptedBody }, Key));
    }

    [Fact]
    public void Append_StoresUtcTimestamp()
    {
        using var dir = new TempDirectory();
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 15, 30, 0, TimeSpan.FromHours(-5)));
        using var w = JournalWriter.Create(Path.Combine(dir.Path, "s.hmj"), Key, timeProvider: clock);
        var record = w.Append(new(JournalAction.Configuration, Sid, false, new("settings.change")));
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 20, 30, 0, TimeSpan.Zero), record.TimestampUtc);
    }

    [Fact]
    public void DecryptBody_WrongKey_Throws()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using (var w = JournalWriter.Create(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); }
        var record = Assert.Single(JournalReader.ReadRecords(path));
        Assert.ThrowsAny<CryptographicException>(() => JournalReader.DecryptBody(record, RandomNumberGenerator.GetBytes(32)));
    }

    [Fact]
    public void Header_ActionSidAndTimeReadableWithoutKey()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 24, 20, 30, 0, TimeSpan.Zero));
        using (var w = JournalWriter.Create(path, Key, timeProvider: clock))
        {
            clock.Advance(TimeSpan.FromMinutes(5));
            w.Append(new(JournalAction.DataMovement, Sid, true, new("export.reg", @"HKLM\Software\Contoso")));
        }

        var header = JournalReader.ReadHeader(path);
        Assert.NotEqual(Guid.Empty, header.SegmentId);
        Assert.Equal(new byte[32], header.PreviousSegmentHash);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 20, 30, 0, TimeSpan.Zero), header.CreatedUtc);

        var record = Assert.Single(JournalReader.ReadRecords(path));
        Assert.Equal(JournalAction.DataMovement, record.Action);
        Assert.Equal(Sid, record.ActorSid);
        Assert.True(record.Elevated);
        Assert.Equal(new DateTimeOffset(2026, 11, 24, 20, 35, 0, TimeSpan.Zero), record.TimestampUtc);
    }

    [Fact]
    public void Create_WithPreviousSegmentHash_FirstRecordLinksToIt()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "segment-0002.hmj");
        var previous = RandomNumberGenerator.GetBytes(32);
        using (var w = JournalWriter.Create(path, Key, previous)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); }

        Assert.Equal(previous, JournalReader.ReadHeader(path).PreviousSegmentHash);
        Assert.Equal(previous, Assert.Single(JournalReader.ReadRecords(path)).PreviousHash);
        Assert.True(JournalVerifier.Verify(path).IsIntact);
    }

    [Fact]
    public void OpenForAppend_ContinuesSequenceAndChain()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using (var w = JournalWriter.Create(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); w.Append(new(JournalAction.Configuration, Sid, false, new("b"))); }

        JournalRecord third;
        using (var w = JournalWriter.OpenForAppend(path, Key)) { third = w.Append(new(JournalAction.Configuration, Sid, false, new("c"))); }

        var records = JournalReader.ReadRecords(path).ToList();
        Assert.Equal(3UL, third.Sequence);
        Assert.Equal(records[1].Hash, third.PreviousHash);
        Assert.Equal(third.Hash, records[2].Hash);
        Assert.Equal("c", JournalReader.DecryptBody(records[2], Key).Kind);
        var verification = JournalVerifier.Verify(path);
        Assert.True(verification.IsIntact);
        Assert.Equal(3UL, verification.RecordCount);
    }

    [Fact]
    public void Append_BodyOver16MiB_ThrowsArgumentException()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using var w = JournalWriter.Create(path, Key);
        var huge = new JournalBody("registry.set", Note: new string('a', 16 * 1024 * 1024));
        Assert.Throws<ArgumentException>(() => w.Append(new(JournalAction.RegistryWrite, Sid, false, huge)));
        Assert.Equal(62, new FileInfo(path).Length);
    }

    [Fact]
    public void Append_ActorSidOver256Bytes_ThrowsArgumentException()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        using var w = JournalWriter.Create(path, Key);
        Assert.Throws<ArgumentException>(() => w.Append(new(JournalAction.Configuration, new string('S', 257), false, new("a"))));
        Assert.Equal(62, new FileInfo(path).Length);
    }

    [Fact]
    public void Journal_LongUnicodePath_Works()
    {
        using var dir = new TempDirectory();
        var folder = Path.Combine(dir.Path, "José Müller");
        while (folder.Length < 300) folder = Path.Combine(folder, "OneDrive - Contoso Ü");
        var path = Path.Combine(folder, "segment-0001.hmj");

        using (var w = JournalWriter.Create(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("a"))); }
        using (var w = JournalWriter.OpenForAppend(path, Key)) { w.Append(new(JournalAction.Configuration, Sid, false, new("b"))); }

        Assert.True(path.Length > 300);
        Assert.Equal(2, JournalReader.ReadRecords(path).Count());
        Assert.Equal(2UL, JournalVerifier.Verify(path).RecordCount);
    }
}
