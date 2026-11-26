namespace HiveMind.Testing;

/// <summary>Finds files in the repository from a test.</summary>
public static class RepoFiles
{
    private const string SolutionFileName = "HiveMind.slnx";

    private static readonly Lazy<string> _root = new(FindRoot);

    /// <summary>
    /// The repository root: the nearest folder at or above <see cref="AppContext.BaseDirectory"/>
    /// that contains <c>HiveMind.slnx</c>.
    /// </summary>
    public static string Root => _root.Value;

    /// <summary>Returns the full path of a file or folder given relative to the repository root.</summary>
    /// <param name="relative">A path relative to the root, such as <c>eng/Setup-DevMachine.ps1</c>.</param>
    public static string Path(string relative) =>
        System.IO.Path.GetFullPath(System.IO.Path.Combine(Root, relative));

    /// <summary>Reads every line of a file given relative to the repository root.</summary>
    /// <param name="relative">A path relative to the root.</param>
    public static string[] ReadLines(string relative) => File.ReadAllLines(Path(relative));

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(System.IO.Path.Combine(directory.FullName, SolutionFileName)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException($"No folder at or above '{AppContext.BaseDirectory}' contains {SolutionFileName}.");
    }
}
