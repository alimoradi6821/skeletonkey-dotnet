using System.Collections.ObjectModel;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Execution;
using SkeletonKey.Runtime.Resources;
using SkeletonKey.Workflow.Resources;

namespace SkeletonKey.Desktop.FlaUI;

/// <summary>Owns one FlaUI application, UIA3 automation instance, and main window.</summary>
public sealed class FlaUiApplicationResource : IWorkflowRuntimeResourceInstance
{
    private readonly Application _application;
    private readonly UIA3Automation _automation;
    private readonly Window _window;
    private readonly FlaUiDesktopApplicationAdapter _adapter;
    private readonly FlaUiApplicationConstraints _constraints;
    private readonly IReadOnlyList<string> _capabilities;
    private readonly bool _ownsProcess;
    private bool _disposed;

    /// <summary>Initializes a FlaUI desktop application resource.</summary>
    public FlaUiApplicationResource(
        string resourceName,
        WorkflowResourceAccessMode access,
        Application application,
        UIA3Automation automation,
        Window window,
        FlaUiApplicationConstraints constraints,
        IReadOnlyList<string> capabilities,
        bool ownsProcess = true)
    {
        ResourceName = resourceName;
        Access = access;
        _application = application ?? throw new ArgumentNullException(nameof(application));
        _automation = automation ?? throw new ArgumentNullException(nameof(automation));
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        _capabilities = new ReadOnlyCollection<string>([.. capabilities]);
        _ownsProcess = ownsProcess;
        _adapter = new FlaUiDesktopApplicationAdapter(_window, constraints.DefaultTimeoutMilliseconds);
        InstanceId = "flaui:desktop.application:" + application.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public string ResourceName { get; }

    /// <inheritdoc />
    public string Kind => StandardWorkflowResourceKinds.DesktopApplication;

    /// <inheritdoc />
    public string InstanceId { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Capabilities => new ReadOnlyCollection<string>([.. _capabilities]);

    /// <inheritdoc />
    public WorkflowResourceAccessMode Access { get; }

    /// <inheritdoc />
    public INodeResourceHandle CreateHandle()
    {
        return new DesktopApplicationResourceHandle(ResourceName, Kind, InstanceId, Capabilities, _adapter);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        List<Exception> errors = [];
        if (_constraints.CloseOnDispose)
        {
            try
            {
                CloseOwnedTarget(errors);
            }
            catch (Exception exception)
            {
                errors.Add(exception);
            }
        }

        TryDispose(_automation.Dispose, errors);
        TryDispose(_application.Dispose, errors);
        return errors.Count switch
        {
            0 => ValueTask.CompletedTask,
            1 => ValueTask.FromException(errors[0]),
            _ => ValueTask.FromException(new AggregateException(errors)),
        };
    }

    private void CloseOwnedTarget(List<Exception> errors)
    {
        if (_ownsProcess)
        {
            if (!_application.Close(killIfCloseFails: _constraints.Mode == "launch") && _constraints.Mode != "launch")
            {
                errors.Add(new InvalidOperationException("Desktop application did not close within the bounded shutdown timeout."));
            }

            return;
        }

        _window.Close();
    }

    private static void TryDispose(Action dispose, List<Exception> errors)
    {
        try
        {
            dispose();
        }
        catch (Exception exception)
        {
            errors.Add(exception);
        }
    }

    private sealed class DesktopApplicationResourceHandle(
        string resourceName,
        string kind,
        string instanceId,
        IReadOnlyList<string> capabilities,
        IDesktopApplicationAdapter adapter) : INodeResourceHandle
    {
        public string ResourceName { get; } = resourceName;

        public string Kind { get; } = kind;

        public string InstanceId { get; } = instanceId;

        public IReadOnlyList<string> Capabilities { get; } = new ReadOnlyCollection<string>([.. capabilities]);

        public bool TryGetAdapter<TAdapter>(out TAdapter? typedAdapter)
            where TAdapter : class
        {
            typedAdapter = adapter as TAdapter;
            return typedAdapter is not null;
        }

        public TAdapter GetRequiredAdapter<TAdapter>()
            where TAdapter : class
        {
            return TryGetAdapter(out TAdapter? typedAdapter) && typedAdapter is not null
                ? typedAdapter
                : throw new InvalidOperationException("The requested resource adapter is not available.");
        }
    }
}
