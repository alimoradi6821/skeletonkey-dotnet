namespace SkeletonKey.Core.Tests;

/// <summary>
/// Verifies that the xUnit infrastructure is available for the core test project.
/// </summary>
public sealed class InfrastructureTests
{
    /// <summary>
    /// Confirms that the test runner can discover and execute tests.
    /// </summary>
    [Fact]
    public void TestInfrastructure_IsOperational()
    {
        Assert.True(true);
    }
}
