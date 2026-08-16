using System.Reflection;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.Abstractions.Tests;

/// <summary>
/// Covers provider-neutral web abstraction contracts.
/// </summary>
public sealed class WebAbstractionTests
{
    /// <summary>
    /// Verifies public abstractions do not expose Playwright types.
    /// </summary>
    [Fact]
    public void PublicContractsDoNotExposePlaywrightTypes()
    {
        Assembly assembly = typeof(IWebPageAdapter).Assembly;
        IEnumerable<Type> exposedTypes = assembly.GetExportedTypes()
            .SelectMany(static type => type.GetMethods().Select(static method => method.ReturnType)
                .Concat(type.GetMethods().SelectMany(static method => method.GetParameters().Select(static parameter => parameter.ParameterType)))
                .Append(type));

        Assert.DoesNotContain(exposedTypes, static type => type.FullName?.Contains("Microsoft.Playwright", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Verifies default navigation policy rejects local files and JavaScript URLs.
    /// </summary>
    [Fact]
    public void DefaultNavigationPolicyRejectsUnsafeSchemes()
    {
        DefaultWebNavigationPolicy policy = new();

        Assert.Null(policy.ValidateNavigation("https://example.test/"));
        Assert.NotNull(policy.ValidateNavigation("javascript:alert(1)"));
        Assert.NotNull(policy.ValidateNavigation("file:///C:/secret.txt"));
    }

    /// <summary>
    /// Verifies screenshot bytes are defensively owned.
    /// </summary>
    [Fact]
    public void ScreenshotResultDefensivelyCopiesBytes()
    {
        byte[] bytes = [1, 2, 3];
        WebScreenshotResult result = new("image/png", bytes);

        bytes[0] = 9;

        Assert.Equal([1, 2, 3], result.Bytes);
    }
}
