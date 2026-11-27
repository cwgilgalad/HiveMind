using HiveMind.Testing;

namespace HiveMind.Policy.Tests;

public sealed class GovernancePolicyTests
{
    [Fact] public void CodeSigningPolicy_ContainsExactAttributionLine() =>
        Assert.Contains("Free code signing provided by SignPath.io, certificate by SignPath Foundation.", File.ReadAllText(RepoFiles.Path("CODE_SIGNING_POLICY.md")));

    [Fact] public void CodeSigningPolicy_ContainsPrivacyStatement() =>
        Assert.Contains("will not transfer any information to other networked systems unless specifically requested by the user or the person installing or operating it", File.ReadAllText(RepoFiles.Path("CODE_SIGNING_POLICY.md")));

    [Theory]
    [InlineData("CONTRIBUTING.md")] [InlineData("SECURITY.md")] [InlineData("CODE_SIGNING_POLICY.md")] [InlineData("docs/development.md")]
    public void Readme_LinksGovernanceDocument(string target) => Assert.Contains($"]({target})", File.ReadAllText(RepoFiles.Path("README.md")));
}
