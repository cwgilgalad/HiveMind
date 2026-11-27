using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace HiveMind.Core.Crypto;

/// <summary>
/// Keeps the master key in a file, protected by an <see cref="IKeyProtector"/> such as DPAPI (spec
/// section 7.9). The file holds ASCII <c>HMMK</c>, version byte 1, the protector id's length (one
/// byte), the protector id (ASCII), the protected blob's length (int32 little-endian) and the
/// protected blob.
/// </summary>
public static class MasterKeyStore
{
    private const byte FormatVersion = 1;
    private const int FixedHeaderLength = 4 + 1 + 1;
    private const int MaximumFileLength = 64 * 1024;

    private static ReadOnlySpan<byte> Magic => "HMMK"u8;

    /// <summary>
    /// Loads the master key from <paramref name="path"/>. Only when there is no file there does it
    /// create a new key, protect it with <paramref name="protector"/> and save it, creating the
    /// folders it needs.
    /// </summary>
    /// <exception cref="MasterKeyUnavailableException">
    /// The file exists but can't be used: it can't be opened or read, it is damaged, it was saved with
    /// a different protector, or <see cref="IKeyProtector.Unprotect"/> failed. The file is left exactly
    /// as it was and no new key is made. Also thrown when a new key can't be saved.
    /// </exception>
    public static MasterKey LoadOrCreate(string path, IKeyProtector protector)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(protector);

        var id = protector.Id;
        if (string.IsNullOrEmpty(id) || id.Length > byte.MaxValue || !Ascii.IsValid(id))
        {
            throw new ArgumentException("The protector's id must be 1 to 255 ASCII characters.", nameof(protector));
        }

        var protectorId = Encoding.ASCII.GetBytes(id);
        var fullPath = Path.GetFullPath(path);
        return TryLoad(fullPath, protector, protectorId) ?? Create(fullPath, protector, protectorId);
    }

    // Returns null only when there is no key file; see OpenExisting.
    private static MasterKey? TryLoad(string path, IKeyProtector protector, byte[] protectorId)
    {
        byte[] contents;
        using (var stream = OpenExisting(path))
        {
            if (stream is null)
            {
                return null;
            }

            contents = ReadAll(stream, path);
        }

        return Decode(contents, path, protector, protectorId);
    }

    // Returns null only when opening the file reports that it, or its folder, doesn't exist. Every
    // other failure throws, so a key that can't be read is never mistaken for a missing one.
    // File.Exists must not decide this: it returns false on access denied.
    private static FileStream? OpenExisting(string path)
    {
        try
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            throw Unavailable(path, "could not be opened", ex);
        }
    }

    private static byte[] ReadAll(FileStream stream, string path)
    {
        try
        {
            if (stream.Length > MaximumFileLength)
            {
                throw Damaged(path, "it is far too large");
            }

            var contents = new byte[stream.Length];
            stream.ReadExactly(contents);
            return contents;
        }
        catch (Exception ex) when (ex is not MasterKeyUnavailableException)
        {
            throw Unavailable(path, "could not be read", ex);
        }
    }

    private static MasterKey Decode(ReadOnlySpan<byte> data, string path, IKeyProtector protector, byte[] protectorId)
    {
        if (data.Length < FixedHeaderLength || !data[..4].SequenceEqual(Magic))
        {
            throw Damaged(path, "it doesn't start with a master key header");
        }

        if (data[4] != FormatVersion)
        {
            throw Unavailable(path, $"uses format version {data[4]}, which this version of HiveMind can't read");
        }

        int idLength = data[5];
        var blobLengthOffset = FixedHeaderLength + idLength;
        if (data.Length < blobLengthOffset + sizeof(int))
        {
            throw Damaged(path, "it is cut short");
        }

        var storedId = data.Slice(FixedHeaderLength, idLength);
        if (!storedId.SequenceEqual(protectorId))
        {
            throw Unavailable(path, $"was protected with '{Encoding.ASCII.GetString(storedId)}', not '{protector.Id}'");
        }

        var blobLength = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(blobLengthOffset, sizeof(int)));
        var blob = data[(blobLengthOffset + sizeof(int))..];
        if (blobLength != blob.Length)
        {
            throw Damaged(path, $"it says the protected key is {blobLength} bytes, but {blob.Length} bytes follow");
        }

        byte[] keyBytes;
        try
        {
            keyBytes = protector.Unprotect(blob);
        }
        catch (Exception ex)
        {
            throw Unavailable(path, "could not be unprotected, for example after a Windows profile reset", ex);
        }

        try
        {
            if (keyBytes.Length != MasterKey.SizeInBytes)
            {
                throw Damaged(path, $"the unprotected key is {keyBytes.Length} bytes, not {MasterKey.SizeInBytes}");
            }

            return MasterKey.FromBytes(keyBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    private static MasterKey Create(string path, IKeyProtector protector, byte[] protectorId)
    {
        bool saved;
        var key = MasterKey.Generate();
        try
        {
            saved = WriteAtomically(path, Encode(protectorId, protector.Protect(key.Bytes)), overwrite: false);
        }
        catch (Exception ex)
        {
            key.Dispose();
            throw new MasterKeyUnavailableException($"A new master key could not be saved to '{path}'.", ex);
        }

        if (saved)
        {
            return key;
        }

        // Another process created the key first, and the move refused to replace it. Use that key,
        // through the same checks as any other load.
        key.Dispose();
        return TryLoad(path, protector, protectorId)
            ?? throw new MasterKeyUnavailableException($"The master key file '{path}' appeared and vanished again while a new key was being saved.");
    }

    private static byte[] Encode(byte[] protectorId, byte[] blob)
    {
        var contents = new byte[FixedHeaderLength + protectorId.Length + sizeof(int) + blob.Length];
        var span = contents.AsSpan();
        Magic.CopyTo(span);
        span[4] = FormatVersion;
        span[5] = (byte)protectorId.Length;
        protectorId.CopyTo(span[FixedHeaderLength..]);
        var blobLengthOffset = FixedHeaderLength + protectorId.Length;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(blobLengthOffset, sizeof(int)), blob.Length);
        blob.CopyTo(span[(blobLengthOffset + sizeof(int))..]);
        return contents;
    }

    // Writes contents to a uniquely named temp file in the same folder, flushes it to disk and moves
    // it into place, so the key file is never left half written. With overwrite false an existing
    // file is never replaced: the method returns false and leaves it alone. The temp file is deleted
    // on any failure.
    private static bool WriteAtomically(string path, ReadOnlySpan<byte> contents, bool overwrite)
    {
        var folder = Path.GetDirectoryName(path)
            ?? throw new ArgumentException("The key file's path has no folder.", nameof(path));
        Directory.CreateDirectory(folder);
        var tempPath = Path.Combine(folder, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(contents);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite);
            return true;
        }
        catch (IOException) when (!overwrite && File.Exists(path))
        {
            DeleteQuietly(tempPath);
            return false;
        }
        catch
        {
            DeleteQuietly(tempPath);
            throw;
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Leaving a stray temp file behind is better than hiding the error that brought us here.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }

    private static MasterKeyUnavailableException Unavailable(string path, string problem, Exception? innerException = null) =>
        new($"The master key file '{path}' {problem}. It was left unchanged.", innerException);

    private static MasterKeyUnavailableException Damaged(string path, string problem) =>
        Unavailable(path, $"is damaged: {problem}");
}
