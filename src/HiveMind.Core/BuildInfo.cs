using System.Reflection;

namespace HiveMind.Core;

/// <summary>The product name and the version of this build.</summary>
public static class BuildInfo
{
    /// <summary>The product name.</summary>
    public const string ProductName = "HiveMind";

    /// <summary>
    /// The informational version of this build, such as <c>0.0.0-dev+&lt;commit&gt;</c>, read from the
    /// assembly's <see cref="AssemblyInformationalVersionAttribute"/>. It is empty if the assembly has
    /// no such attribute.
    /// </summary>
    public static string InformationalVersion { get; } =
        typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? string.Empty;
}
