using System.Buffers.Binary;
using System.Security.Cryptography;
using HiveMind.Core.Journal;
using HiveMind.Testing;

namespace HiveMind.Core.Tests.Journal;

public sealed class JournalVerifierTests
{
    private const string Sid = "S-1-5-21-1000-2000-3000-1001";

    private static byte[] Key { get; } = RandomNumberGenerator.GetBytes(32);

    // Writes a 3-record segment and returns its path and records.
    private static (string Path, List<JournalRecord> Records) WriteSegment(TempDirectory dir)
    {
        var path = Path.Combine(dir.Path, "segment-0001.hmj");
        using (var w = JournalWriter.Create(path, Key))
        {
            w.Append(new(JournalAction.RegistryWrite, Sid, false, new("registry.set", @"HKCU\Software\Contoso", "one")));
            w.Append(new(JournalAction.RegistryWrite, Sid, false, new("registry.set", @"HKCU\Software\Contoso", "two")));
            w.Append(new(JournalAction.RegistryWrite, Sid, false, new("registry.set", @"HKCU\Software\Contoso", "three")));
        }

        return (path, JournalReader.ReadRecords(path).ToList());
    }

    private static byte[] Bytes(byte[] file, JournalRecord record) => file[(int)record.FileOffset..((int)record.FileOffset + record.TotalLength)];

    [Fact]
    public void Verify_Intact_ReturnsCountAndLastHash()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var result = JournalVerifier.Verify(path);
        Assert.True(result.IsIntact);
        Assert.Equal(3UL, result.RecordCount);
        Assert.Null(result.FirstBadSequence);
        Assert.Equal(JournalProblem.None, result.Problem);
        Assert.Equal(records[2].Hash, result.LastGoodHash);
    }

    [Fact]
    public void Verify_FlippedBodyByte_ReportsHashMismatchAtSequence2()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        file[records[1].FileOffset + records[1].TotalLength - 32 - 1] ^= 0x01;
        File.WriteAllBytes(path, file);

        var result = JournalVerifier.Verify(path);
        Assert.False(result.IsIntact);
        Assert.Equal(JournalProblem.HashMismatch, result.Problem);
        Assert.Equal(1UL, result.RecordCount);
        Assert.Equal(2UL, result.FirstBadSequence);
        Assert.Equal(records[0].Hash, result.LastGoodHash);
    }

    [Fact]
    public void Verify_DeletedMiddleRecord_ReportsSequenceGapAt2()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        File.WriteAllBytes(path, [.. file[..(int)records[1].FileOffset], .. Bytes(file, records[2])]);

        var result = JournalVerifier.Verify(path);
        Assert.Equal(JournalProblem.SequenceGap, result.Problem);
        Assert.Equal(1UL, result.RecordCount);
        Assert.Equal(2UL, result.FirstBadSequence);
    }

    [Fact]
    public void Verify_SwappedRecords_ReportsSequenceGap()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        File.WriteAllBytes(path, [.. file[..(int)records[1].FileOffset], .. Bytes(file, records[2]), .. Bytes(file, records[1])]);

        var result = JournalVerifier.Verify(path);
        Assert.Equal(JournalProblem.SequenceGap, result.Problem);
        Assert.Equal(1UL, result.RecordCount);
        Assert.Equal(2UL, result.FirstBadSequence);
    }

    [Fact]
    public void Verify_EditedPreviousHash_ReportsBrokenChain()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        var previousHashAt = (int)records[1].FileOffset + file.AsSpan((int)records[1].FileOffset).IndexOf(records[0].Hash);
        file[previousHashAt] ^= 0x01;
        File.WriteAllBytes(path, file);

        var result = JournalVerifier.Verify(path);
        Assert.Equal(JournalProblem.BrokenChain, result.Problem);
        Assert.Equal(1UL, result.RecordCount);
        Assert.Equal(2UL, result.FirstBadSequence);
    }

    [Fact]
    public void Verify_BadMagic_ReportsBadHeader()
    {
        using var dir = new TempDirectory();
        var (path, _) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        file[0] = (byte)'X';
        File.WriteAllBytes(path, file);

        var result = JournalVerifier.Verify(path);
        Assert.False(result.IsIntact);
        Assert.Equal(JournalProblem.BadHeader, result.Problem);
        Assert.Equal(0UL, result.RecordCount);
        Assert.Equal(1UL, result.FirstBadSequence);
    }

    [Fact]
    public void Verify_UnknownVersion_ReportsBadHeader()
    {
        using var dir = new TempDirectory();
        var (path, _) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(4), 2);
        File.WriteAllBytes(path, file);

        Assert.Equal(JournalProblem.BadHeader, JournalVerifier.Verify(path).Problem);
    }

    [Fact]
    public void Verify_HeaderOnly_IsIntactWithZeroRecords()
    {
        using var dir = new TempDirectory();
        var path = Path.Combine(dir.Path, "s.hmj");
        var previous = RandomNumberGenerator.GetBytes(32);
        using (JournalWriter.Create(path, Key, previous)) { }

        var result = JournalVerifier.Verify(path);
        Assert.True(result.IsIntact);
        Assert.Equal(0UL, result.RecordCount);
        Assert.Equal(previous, result.LastGoodHash);
    }

    [Fact]
    public void Verify_RecordLengthInconsistent_ReportsHashMismatch()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        var lengthField = file.AsSpan((int)records[2].FileOffset, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(lengthField, BinaryPrimitives.ReadUInt32LittleEndian(lengthField) + 1);
        File.WriteAllBytes(path, [.. file, 0x00]);

        var result = JournalVerifier.Verify(path);
        Assert.Equal(JournalProblem.HashMismatch, result.Problem);
        Assert.Equal(2UL, result.RecordCount);
        Assert.Equal(3UL, result.FirstBadSequence);
    }

    [Fact]
    public void Verify_TruncatedTail_ReportsLastGoodRecord()
    {
        using var dir = new TempDirectory();
        var (path, records) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        File.WriteAllBytes(path, file[..^10]);

        var result = JournalVerifier.Verify(path);
        Assert.False(result.IsIntact);
        Assert.Equal(JournalProblem.TruncatedTail, result.Problem);
        Assert.Equal(2UL, result.RecordCount);
        Assert.Equal(3UL, result.FirstBadSequence);
        Assert.Equal(records[1].Hash, result.LastGoodHash);
    }

    [Fact]
    public void OpenForAppend_TruncatedTail_Throws()
    {
        using var dir = new TempDirectory();
        var (path, _) = WriteSegment(dir);
        var file = File.ReadAllBytes(path);
        File.WriteAllBytes(path, file[..^10]);

        var ex = Assert.Throws<JournalCorruptException>(() => JournalWriter.OpenForAppend(path, Key));
        Assert.Equal(JournalProblem.TruncatedTail, ex.Problem);
        Assert.Equal(2UL, ex.LastGoodSequence);
        Assert.Equal(file.Length - 10, new FileInfo(path).Length);
    }
}
