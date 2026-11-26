namespace HiveMind.Core.Tests;

public sealed class BuildInfoTests
{
    [Fact]
    public void ProductName_IsHiveMind() => Assert.Equal("HiveMind", BuildInfo.ProductName);

    [Fact]
    public void InformationalVersion_StartsWithDevVersion() =>
        Assert.StartsWith("0.0.0-dev", BuildInfo.InformationalVersion);
}
