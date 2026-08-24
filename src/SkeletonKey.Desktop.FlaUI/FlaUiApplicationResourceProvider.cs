using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
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
        Application? application = null;
        UIA3Automation? automation = null;
        Window? resolvedWindow = null;
        bool ownsProcess = constraints.Mode == "launch";
        try
        {
            automation = new UIA3Automation();
            HashSet<int> existingProcessIds = constraints.Mode == "launch"
                ? CaptureProcessIds(constraints.Executable!)
                : [];
            HashSet<IntPtr> existingWindowHandles = constraints.Mode == "launch"
                ? CaptureTopLevelWindowHandles(automation, existingProcessIds)
                : [];

            application = CreateApplication(constraints);
            ApplicationWindowResult resolved = await WaitForMainWindowAsync(
                application,
                automation,
                constraints,
                existingProcessIds,
                existingWindowHandles,
                cancellationToken).ConfigureAwait(false);
            application = resolved.Application;
            resolvedWindow = resolved.Window;
            ownsProcess = resolved.OwnsProcess;
            if (resolvedWindow is null)
            {
                throw Failure(DesktopAutomationErrorCodes.WindowUnavailable, "The application main window did not become available.", "create");
            }

            return new FlaUiApplicationResource(
                request.ResourceName,
                request.Definition.Access,
                application,
                automation,
                resolvedWindow,
                constraints,
                Capabilities,
                ownsProcess);
        }
        catch (DesktopAutomationException)
        {
            CleanupFailedCreation(application, automation, resolvedWindow, constraints.Mode == "launch", ownsProcess);
            throw;
        }
        catch (OperationCanceledException)
        {
            CleanupFailedCreation(application, automation, resolvedWindow, constraints.Mode == "launch", ownsProcess);
            throw;
        }
        catch (Exception exception)
        {
            CleanupFailedCreation(application, automation, resolvedWindow, constraints.Mode == "launch", ownsProcess);
            throw Failure(DesktopAutomationErrorCodes.ApplicationStartFailed, "Application launch or attachment failed.", "create", exception);
        }
    }

    private static void CleanupFailedCreation(
        Application? application,
        UIA3Automation? automation,
        Window? window,
        bool closeApplication,
        bool ownsProcess)
    {
        if (application is not null && closeApplication)
        {
            try
            {
                CloseOwnedTarget(application, window, ownsProcess, killIfCloseFails: true);
            }
            catch
            {
                // Preserve the primary creation failure.
            }
        }

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
            application.Dispose();
        }
        catch
        {
            // Preserve the primary creation failure.
        }
    }

    private static void CloseOwnedTarget(Application application, Window? window, bool ownsProcess, bool killIfCloseFails)
    {
        if (ownsProcess)
        {
            application.Close(killIfCloseFails);
            return;
        }

        window?.Close();
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
        IReadOnlySet<IntPtr> existingWindowHandles,
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
            IReadOnlyList<int> candidateProcessIds = CandidateProcessIds(initialProcessId, launchedProcessName);
            foreach (int candidateProcessId in candidateProcessIds)
            {
                bool processExistedBeforeLaunch = existingProcessIds.Contains(candidateProcessId);
                Window? window = TryFindOwnedTopLevelWindow(
                    automation,
                    candidateProcessId,
                    processExistedBeforeLaunch ? existingWindowHandles : null);
                if (window is null)
                {
                    continue;
                }

                bool ownsProcess = !processExistedBeforeLaunch;
                if (candidateProcessId == initialProcessId)
                {
                    return new ApplicationWindowResult(application, window, ownsProcess);
                }

                var successor = Application.Attach(candidateProcessId);
                application.Dispose();
                return new ApplicationWindowResult(successor, window, ownsProcess);
            }

            TimeSpan remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                break;
            }

            await Task.Delay(remaining < TimeSpan.FromMilliseconds(100) ? remaining : TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
        }
        while (stopwatch.Elapsed < timeout);

        return new ApplicationWindowResult(application, null, !existingProcessIds.Contains(initialProcessId));
    }

    private static Window? TryFindOwnedTopLevelWindow(
        UIA3Automation automation,
        int processId,
        IReadOnlySet<IntPtr>? excludedWindowHandles)
    {
        try
        {
            AutomationElement[] windows = automation.GetDesktop().FindAllChildren(
                cf => cf.ByProcessId(processId).And(cf.ByControlType(ControlType.Window)));
            return windows
                .Select(static element => new
                {
                    Element = element,
                    Handle = element.Properties.NativeWindowHandle.ValueOrDefault,
                })
                .Where(candidate => candidate.Handle != IntPtr.Zero &&
                    (excludedWindowHandles is null || !excludedWindowHandles.Contains(candidate.Handle)))
                .OrderBy(static candidate => candidate.Handle.ToInt64())
                .Select(static candidate => candidate.Element.AsWindow())
                .FirstOrDefault(static window => window is not null);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
        {
            // The process or one of its windows can disappear while a delegated launch is settling.
            return null;
        }
    }

    private static IReadOnlyList<int> CandidateProcessIds(int initialProcessId, string? launchedProcessName)
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
                .Where(processId => processId != initialProcessId)
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

    private static HashSet<IntPtr> CaptureTopLevelWindowHandles(UIA3Automation automation, IReadOnlySet<int> processIds)
    {
        HashSet<IntPtr> handles = [];
        foreach (int processId in processIds.OrderBy(static value => value))
        {
            try
            {
                AutomationElement[] windows = automation.GetDesktop().FindAllChildren(
                    cf => cf.ByProcessId(processId).And(cf.ByControlType(ControlType.Window)));
                foreach (AutomationElement window in windows)
                {
                    IntPtr handle = window.Properties.NativeWindowHandle.ValueOrDefault;
                    if (handle != IntPtr.Zero)
                    {
                        handles.Add(handle);
                    }
                }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException)
            {
                // Existing windows are only a baseline. Ignore processes that disappear during capture.
            }
        }

        return handles;
    }

    private static DesktopAutomationException Failure(string code, string message, string operation, Exception? exception = null)
    {
        return new DesktopAutomationException(new DesktopOperationError(code, message, operation), exception);
    }

    private sealed record ApplicationWindowResult(Application Application, Window? Window, bool OwnsProcess);
}
