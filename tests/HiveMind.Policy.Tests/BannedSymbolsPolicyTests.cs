using HiveMind.Testing;

namespace HiveMind.Policy.Tests;

public sealed class BannedSymbolsPolicyTests
{
    [Theory]
    [InlineData("M:System.IO.Pipes.NamedPipeServerStream.RunAsClient(System.IO.Pipes.PipeStreamImpersonationWorker)")]
    [InlineData("M:System.Security.Principal.WindowsIdentity.RunImpersonated(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle,System.Action)")]
    [InlineData("M:System.Diagnostics.Process.Start(System.Diagnostics.ProcessStartInfo)")]
    [InlineData("M:System.Diagnostics.Process.Start(System.String,System.String,System.Security.SecureString,System.String)")]
    [InlineData("T:Microsoft.Win32.Registry")]
    [InlineData("T:Microsoft.Win32.RegistryKey")]
    [InlineData("M:System.Runtime.InteropServices.NativeLibrary.GetExport(System.IntPtr,System.String)")]
    public void BannedSymbols_ContainsRequiredEntry(string docId) =>
        Assert.Contains(docId, RepoFiles.ReadLines("eng/BannedSymbols.txt").Select(l => l.Split(';')[0].Trim()));

    [Fact]
    public void BuildProps_WireBannedSymbolsIntoProductProjects() =>
        Assert.Contains("eng/BannedSymbols.txt", File.ReadAllText(RepoFiles.Path("Directory.Build.props")).Replace('\\', '/'));
}
