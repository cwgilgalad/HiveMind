namespace HiveMind.Testing;

/// <summary>
/// A new, uniquely named folder under the user's temp folder. Disposing it deletes the folder and
/// everything in it.
/// </summary>
public sealed class TempDirectory : IDisposable
{
    /// <summary>Creates the folder.</summary>
    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"HiveMind-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path);
    }

    /// <summary>The full path of the folder.</summary>
    public string Path { get; }

    /// <summary>Deletes the folder and everything in it.</summary>
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
