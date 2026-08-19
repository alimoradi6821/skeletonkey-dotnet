using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Desktop.FlaUI;

/// <summary>Creates FlaUI UIA3-backed resources for <c>desktop.application</c>.</summary>
public sealed class FlaUiApplicationResourceProvider : IWorkflowRuntimeResourceProvider
{
    /// <inheritdoc />
    public string Kind => StandardWorkflowResourceKinds.DesktopApplication;

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities { get; } = Array.AsReadOnly(
    [
        StandardWorkflowResourceCapabilities.DesktopApplicationLifecycle,
        StandardWorkflowResourceCapabilities.DesktopLocators,
        StandardWorkflowResourceCapabilities.DesktopActions,
        StandardWorkflowResourceCapabilities.DesktopForms,
        StandardWorkflowResourceCapabilities.DesktopText,
    ]);

    /// <inheritdoc />
    public async ValueTask<IWorkflowRuntimeResourceInstance> CreateAsync(WorkflowRuntimeResourceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Definition.Kind, Kind, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("FlaUI application provider only supports desktop.application resources.");
        }

        if (!OperatingSystem.IsWindows())
        {
            throw Failure(DesktopAutomationErrorCodes.PlatformNotSupported, "Desktop automation requires Windows.", "create");
        }

        var constraints = FlaUiApplicationConstraints.Parse(request.Definition.Constraints);
        HashSet<int> existingProcessIds = constraints.Mode == "launch"
            ? CaptureProcessIds(constraints.Executable!)
            : [];
        Application? application = null;
        UIA3Automation? automation = null;
        try
        {
            application = CreateApplication(constraints);
            automation = new UIA3Automation();
            ApplicationWindowResult resolved = await WaitForMainWindowAsync(
                application,
                automation,
                constraints,
                existingProcessIds,
                cancellationToken).ConfigureAwait(false);
            application = resolved.Application;
            Window? window = resolved.Window;
            if (window is null)
            {
                throw Failure(DesktopAutomationErrorCodes.WindowUnavailable, "The application main window did not become available.", "create");
            }

            return new FlaUiApplicationResource(
                request.ResourceName,
                request.Definition.Access,
                application,
                automation,
                window,
                constraints,
                Capabilities);
        }
        catch (DesktopAutomationException)
        {
            CleanupFailedCreation(application, automation, constraints.Mode == "launch");
            throw;
        }
        catch (OperationCanceledException)
        {
            CleanupFailedCreation(application, automation, constraints.Mode == "launch");
            throw;
        }
        catch (Exception exception)
        {
            CleanupFailedCreation(application, automation, constraints.Mode == "launch");
            throw Failure(DesktopAutomationErrorCodes.ApplicationStartFailed, "Application launch or attachment failed.", "create", exception);
        }
    }

    private static void CleanupFailedCreation(Application? application, UIA3Automation? automation, bool closeApplication)
    {
        try
        {
            automation?.Dispose();
        }
        catch
        {
            // Preserve the primary creation failure.
        }

        if (application is null)
        {
            return;
        }

        try
        {
            if (closeApplication)
            {
                application.Close(killIfCloseFails: true);
            }
        }
        catch
        {
            // Preserve the primary creation failure.
        }

        try
        {
            application.Dispose();
        }
        catch
        {
            // Preserve the primary creation failure.
        }
    }

    private static Application CreateApplication(FlaUiApplicationConstraints constraints)
    {
        if (constraints.Mode == "launch")
        {
            ProcessStartInfo startInfo = new(constraints.Executable!, constraints.Arguments)
            {
                UseShellExecute = false,
            };
            return Application.Launch(startInfo);
        }

        if (constraints.ProcessId is int processId)
        {
            return Application.Attach(processId);
        }

        Process[] matches = Process.GetProcessesByName(constraints.ProcessName!).OrderBy(static process => process.Id).ToArray();
        try
        {
            if (matches.Length != 1)
            {
                throw Failure(DesktopAutomationErrorCodes.ApplicationStartFailed, "Attach by processName requires exactly one matching process.", "attach");
            }

            return Application.Attach(matches[0].Id);
        }
        finally
        {
            foreach (Process process in matches)
            {
                process.Dispose();
            }
        }
    }

    private static async ValueTask<ApplicationWindowResult> WaitForMainWindowAsync(
        Application application,
        UIA3Automation automation,
        FlaUiApplicationConstraints constraints,
        IReadOnlySet<int> existingProcessIds,
        CancellationToken cancellationToken)
    {
        int initialProcessId = application.ProcessId;
        string? launchedProcessName = constraints.Mode == "launch"
            ? Path.GetFileNameWithoutExtension(constraints.Executable!)
            : null;
        var timeout = TimeSpan.FromMilliseconds(constraints.MainWindowTimeoutMilliseconds);
        var stopwatch = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<int> candidateProcessIds = CandidateProcessIds(initialProcessId, launchedProcessName, existingProcessIds);
            foreach (int candidateProcessId in candidateProcessIds)
            {
                Process? process = null;
                try
                {
                    process = Process.GetProcessById(candidateProcessId);
                    process.Refresh();
                    IntPtr mainWindowHandle = process.MainWindowHandle;
                    if (mainWindowHandle == IntPtr.Zero)
                    {
                        continue;
                    }

                    Window? window = automation.FromHandle(mainWindowHandle)?.AsWindow();
                    if (window is null)
                    {
                        continue;
                    }

                    if (candidateProcessId == initialProcessId)
                    {
                        return new ApplicationWindowResult(application, window);
                    }

                    var successor = Application.Attach(candidateProcessId);
                    application.Dispose();
                    return new ApplicationWindowResult(successor, window);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
                {
                    // The process or its window can disappear while a delegated launch is settling.
                }
                finally
                {
                    process?.Dispose();
                }
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
        while (stopwatch.Elapsed < timeout);

        return new ApplicationWindowResult(application, null);
    }

    private static IReadOnlyList<int> CandidateProcessIds(int initialProcessId, string? launchedProcessName, IReadOnlySet<int> existingProcessIds)
    {
        List<int> processIds = [initialProcessId];
        if (string.IsNullOrWhiteSpace(launchedProcessName))
        {
            return processIds;
        }

        Process[] matches = Process.GetProcessesByName(launchedProcessName);
        try
        {
            processIds.AddRange(matches
                .Select(static process => process.Id)
                .Where(processId => processId != initialProcessId && !existingProcessIds.Contains(processId))
                .OrderBy(static processId => processId));
            return processIds;
        }
        finally
        {
            foreach (Process process in matches)
            {
                process.Dispose();
            }
        }
    }

    private static HashSet<int> CaptureProcessIds(string executable)
    {
        string processName = Path.GetFileNameWithoutExtension(executable);
        Process[] matches = Process.GetProcessesByName(processName);
        try
        {
            return matches.Select(static process => process.Id).ToHashSet();
        }
        finally
        {
            foreach (Process process in matches)
            {
                process.Dispose();
            }
        }
    }

    private static DesktopAutomationException Failure(string code, string message, string operation, Exception? exception = null)
    {
        return new DesktopAutomationException(new DesktopOperationError(code, message, operation), exception);
    }

    private sealed record ApplicationWindowResult(Application Application, Window? Window);
}
