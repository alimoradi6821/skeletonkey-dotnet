using System.Text.Json.Nodes;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Web.Abstractions;
using SkeletonKey.Web.Playwright;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Web.Integration.Tests;

/// <summary>Exercises the Phase 2 host-lifetime contract with a real Playwright persistent profile.</summary>
public sealed class HostLifetimePlaywrightSmokeTests
{
    /// <summary>Verifies healthy reuse and replacement after the persistent browser resource is deliberately closed.</summary>
    [Fact]
    public async Task PersistentPlaywrightResourceReusesAndRecoversWhenEnabled()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("SKELETONKEY_PLAYWRIGHT_SMOKE"), "1", StringComparison.Ordinal))
        {
            return;
        }

        string profile = Path.Combine(Path.GetTempPath(), "skeletonkey-phase2-playwright", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(profile);
        try
        {
            PlaywrightPageResourceProvider provider = new();
            await using WorkflowRuntimeHostResourceRegistry registry = new();
            WorkflowResourceDefinition definition = new(
                StandardWorkflowResourceKinds.WebPage,
                WorkflowResourceLifetime.Host,
                WorkflowResourceAccessMode.Exclusive,
                capabilities: [StandardWorkflowResourceCapabilities.WebNavigation],
                constraints: new JsonObject
                {
                    ["engine"] = "chromium",
                    ["visibility"] = "headless",
                    ["profile"] = "persistent",
                    ["userDataDirectory"] = profile,
                    ["defaultTimeoutMilliseconds"] = 10000,
                });

            WorkflowRuntimeResourceRequest firstRequest = new("execution-a", "invocation-a", "phase2-playwright", "page", definition);
            IWorkflowRuntimeResourceInstance first = await registry.GetOrCreateAsync(firstRequest, provider);
            WorkflowRuntimeResourceHealthState firstHealth = await Assert.IsAssignableFrom<IWorkflowRuntimeResourceHealthParticipant>(first).GetHealthAsync();

            WorkflowRuntimeResourceRequest secondRequest = new("execution-b", "invocation-b", "phase2-playwright", "page", definition);
            IWorkflowRuntimeResourceInstance reused = await registry.GetOrCreateAsync(secondRequest, provider);

            Assert.Equal(WorkflowRuntimeResourceHealthState.Healthy, firstHealth);
            Assert.Same(first, reused);

            await first.DisposeAsync();

            WorkflowRuntimeResourceRequest thirdRequest = new("execution-c", "invocation-c", "phase2-playwright", "page", definition);
            IWorkflowRuntimeResourceInstance replacement = await registry.GetOrCreateAsync(thirdRequest, provider);
            WorkflowRuntimeResourceHealthState replacementHealth = await Assert.IsAssignableFrom<IWorkflowRuntimeResourceHealthParticipant>(replacement).GetHealthAsync();

            Assert.NotSame(first, replacement);
            Assert.Equal(WorkflowRuntimeResourceHealthState.Healthy, replacementHealth);
        }
        finally
        {
            if (Directory.Exists(profile))
            {
                Directory.Delete(profile, recursive: true);
            }
        }
    }
}
