using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.Input;
using FlaUI.Core.Patterns;
using FlaUI.Core.WindowsAPI;
using SkeletonKey.Desktop.Abstractions;
using SkeletonKey.Locators;

namespace SkeletonKey.Desktop.FlaUI;

/// <summary>Executes provider-neutral desktop operations through FlaUI UIA3.</summary>
public sealed class FlaUiDesktopApplicationAdapter : IDesktopApplicationAdapter
{
    private readonly Window _window;
    private readonly int _defaultTimeoutMilliseconds;

    /// <summary>Initializes an adapter over one application main window.</summary>
    public FlaUiDesktopApplicationAdapter(Window window, int defaultTimeoutMilliseconds = 30000)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _defaultTimeoutMilliseconds = ValidateTimeout(defaultTimeoutMilliseconds);
    }

    /// <inheritdoc />
    public async ValueTask ClickAsync(ResolvedLocatorPlan locator, DesktopClickRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            if (request.Button is not ("left" or "right") || request.ClickCount is < 1 or > 2)
            {
                throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop click supports left or right button and one or two clicks.", "click");
            }

            ValidateElementIndex(request.ElementIndex);
            AutomationElement element = SelectOne(
                await ResolveAsync(locator, EffectiveTimeout(request.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false),
                request.ElementIndex);
            cancellationToken.ThrowIfCancellationRequested();
            if (request.Button == "left" && request.ClickCount == 1 && element.Patterns.Invoke.TryGetPattern(out IInvokePattern? invokePattern))
            {
                invokePattern.Invoke();
            }
            else if (request.Button == "right" && request.ClickCount == 2)
            {
                element.RightDoubleClick();
            }
            else if (request.Button == "right")
            {
                element.RightClick();
            }
            else if (request.ClickCount == 2)
            {
                element.DoubleClick();
            }
            else
            {
                element.Click();
            }
        }
        catch (DesktopAutomationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop click failed.", "click", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask FillAsync(ResolvedLocatorPlan locator, DesktopFillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ArgumentNullException.ThrowIfNull(request.Value);
            ValidateElementIndex(request.ElementIndex);
            AutomationElement element = SelectOne(
                await ResolveAsync(locator, EffectiveTimeout(request.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false),
                request.ElementIndex);
            cancellationToken.ThrowIfCancellationRequested();
            if (element.Patterns.Value.TryGetPattern(out IValuePattern? valuePattern))
            {
                valuePattern.SetValue(request.Value);
            }
            else
            {
                element.Focus();
                Keyboard.TypeSimultaneously(VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A);
                Keyboard.Type(request.Value);
            }
        }
        catch (DesktopAutomationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop fill failed.", "fill", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask PressAsync(ResolvedLocatorPlan locator, DesktopPressRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ArgumentNullException.ThrowIfNull(request.Key);
            VirtualKeyShort key = ParseKey(request.Key);
            ValidateElementIndex(request.ElementIndex);
            AutomationElement element = SelectOne(
                await ResolveAsync(locator, EffectiveTimeout(request.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false),
                request.ElementIndex);
            cancellationToken.ThrowIfCancellationRequested();
            element.Focus();
            Keyboard.Type(key);
        }
        catch (DesktopAutomationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop key press failed.", "press", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string?>> GetTextAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ValidateElementIndex(request.ElementIndex);
            IReadOnlyList<AutomationElement> matches = await ResolveAsync(locator, EffectiveTimeout(request.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
            IReadOnlyList<AutomationElement> selected = SelectForQuery(matches, request.ElementIndex);
            return selected.Select(ReadText).ToArray();
        }
        catch (DesktopAutomationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop text query failed.", "getText", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask<int> GetCountAsync(ResolvedLocatorPlan locator, DesktopQueryRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            ValidateElementIndex(request.ElementIndex);
            IReadOnlyList<AutomationElement> matches = await ResolveAsync(locator, EffectiveTimeout(request.TimeoutMilliseconds), cancellationToken).ConfigureAwait(false);
            return request.ElementIndex is null ? matches.Count : SelectForQuery(matches, request.ElementIndex).Count;
        }
        catch (DesktopAutomationException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Desktop count query failed.", "getCount", exception);
        }
    }

    private async ValueTask<IReadOnlyList<AutomationElement>> ResolveAsync(ResolvedLocatorPlan locator, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(locator);
        AutomationElement root = _window;
        foreach (ResolvedLocatorScope scope in locator.Scopes)
        {
            IReadOnlyList<AutomationElement> matches = await FindWithFallbackAsync(root, scope.Strategies, scope.Cardinality, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
            if (matches.Count != 1)
            {
                throw Failure(DesktopAutomationErrorCodes.LocatorCardinalityMismatch, "A desktop locator scope must resolve to exactly one element.", "locator");
            }

            root = matches[0];
        }

        return await FindWithFallbackAsync(root, locator.Strategies, locator.Cardinality, timeoutMilliseconds, cancellationToken).ConfigureAwait(false);
    }

    private static async ValueTask<IReadOnlyList<AutomationElement>> FindWithFallbackAsync(
        AutomationElement root,
        IReadOnlyList<ResolvedLocatorStrategy> strategies,
        LocatorCardinality cardinality,
        int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        bool supported = false;
        bool cardinalityMismatch = false;
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (ResolvedLocatorStrategy strategy in strategies)
            {
                AutomationElement[]? matches = Find(root, strategy);
                if (matches is null)
                {
                    continue;
                }

                supported = true;
                if (matches.Length > 0 && Accepts(cardinality, matches.Length))
                {
                    return matches;
                }

                cardinalityMismatch |= matches.Length > 0;
            }

            if (!supported)
            {
                throw Failure(DesktopAutomationErrorCodes.UnsupportedLocatorStrategy, "The desktop locator contains no supported UI Automation strategy.", "locator");
            }

            if (Accepts(cardinality, 0))
            {
                return Array.AsReadOnly(Array.Empty<AutomationElement>());
            }

            if (stopwatch.ElapsedMilliseconds >= timeoutMilliseconds)
            {
                break;
            }

            int remainingMilliseconds = Math.Max(1, timeoutMilliseconds - (int)stopwatch.ElapsedMilliseconds);
            await Task.Delay(Math.Min(50, remainingMilliseconds), cancellationToken).ConfigureAwait(false);
        }
        while (true);

        throw cardinalityMismatch
            ? Failure(DesktopAutomationErrorCodes.LocatorCardinalityMismatch, "Desktop element matches did not satisfy declared cardinality.", "locator")
            : Failure(DesktopAutomationErrorCodes.LocatorOperationTimeout, "Desktop locator operation timed out.", "locator");
    }

    private static AutomationElement[]? Find(AutomationElement root, ResolvedLocatorStrategy strategy)
    {
        StringComparison comparison = strategy.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        PropertyConditionFlags flags = strategy.CaseSensitive ? PropertyConditionFlags.None : PropertyConditionFlags.IgnoreCase;
        return strategy.Kind switch
        {
            "test-id" => root.FindAllDescendants(factory => factory.ByAutomationId(strategy.Value!)),
            "text" or "title" or "label" when strategy.Match == LocatorTextMatchMode.Exact => root.FindAllDescendants(factory => factory.ByName(strategy.Value!, flags)),
            "placeholder" or "alt-text" when strategy.Match == LocatorTextMatchMode.Exact => root.FindAllDescendants(factory => factory.ByHelpText(strategy.Value!, flags)),
            "text" or "title" or "label" => root.FindAllDescendants().Where(element => Contains(element.Name, strategy.Value!, comparison)).ToArray(),
            "placeholder" or "alt-text" => root.FindAllDescendants().Where(element => Contains(element.HelpText, strategy.Value!, comparison)).ToArray(),
            "role" => FindByRole(root, strategy, flags, comparison),
            _ => null,
        };
    }

    private static AutomationElement[]? FindByRole(AutomationElement root, ResolvedLocatorStrategy strategy, PropertyConditionFlags flags, StringComparison comparison)
    {
        ControlType? controlType = ControlTypeForRole(strategy.Role);
        if (controlType is null)
        {
            return null;
        }

        if (strategy.Name is null)
        {
            return root.FindAllDescendants(factory => factory.ByControlType(controlType.Value));
        }

        return strategy.Match == LocatorTextMatchMode.Exact
            ? root.FindAllDescendants(factory => factory.ByControlType(controlType.Value).And(factory.ByName(strategy.Name, flags)))
            : root.FindAllDescendants(factory => factory.ByControlType(controlType.Value)).Where(element => Contains(element.Name, strategy.Name, comparison)).ToArray();
    }

    private static ControlType? ControlTypeForRole(string? role)
    {
        return role switch
        {
            "button" => ControlType.Button,
            "checkbox" => ControlType.CheckBox,
            "combobox" => ControlType.ComboBox,
            "document" => ControlType.Document,
            "edit" or "textbox" => ControlType.Edit,
            "group" => ControlType.Group,
            "link" => ControlType.Hyperlink,
            "list" => ControlType.List,
            "listitem" or "list-item" => ControlType.ListItem,
            "menu" => ControlType.Menu,
            "menubar" or "menu-bar" => ControlType.MenuBar,
            "menuitem" or "menu-item" => ControlType.MenuItem,
            "pane" => ControlType.Pane,
            "tab" => ControlType.Tab,
            "tabitem" or "tab-item" => ControlType.TabItem,
            "text" => ControlType.Text,
            "tree" => ControlType.Tree,
            "treeitem" or "tree-item" => ControlType.TreeItem,
            "window" => ControlType.Window,
            _ => null,
        };
    }

    private static bool Contains(string? candidate, string expected, StringComparison comparison)
    {
        return candidate?.Contains(expected, comparison) == true;
    }

    private static bool Accepts(LocatorCardinality cardinality, int count)
    {
        return cardinality switch
        {
            LocatorCardinality.One => count == 1,
            LocatorCardinality.ZeroOrOne => count <= 1,
            LocatorCardinality.OneOrMore => count >= 1,
            LocatorCardinality.Many => true,
            _ => false,
        };
    }

    private static AutomationElement SelectOne(IReadOnlyList<AutomationElement> matches, int? index)
    {
        if (index is int selectedIndex)
        {
            return selectedIndex >= 0 && selectedIndex < matches.Count
                ? matches[selectedIndex]
                : throw Failure(DesktopAutomationErrorCodes.LocatorCardinalityMismatch, "Desktop elementIndex is outside the resolved match range.", "locator");
        }

        return matches.Count == 1
            ? matches[0]
            : throw Failure(DesktopAutomationErrorCodes.LocatorCardinalityMismatch, "Desktop actions require exactly one match or an explicit elementIndex.", "locator");
    }

    private static IReadOnlyList<AutomationElement> SelectForQuery(IReadOnlyList<AutomationElement> matches, int? index)
    {
        return index is null ? matches : [SelectOne(matches, index)];
    }

    private static string? ReadText(AutomationElement element)
    {
        if (element.Patterns.Value.TryGetPattern(out IValuePattern? valuePattern) && valuePattern.Value.TryGetValue(out string? value))
        {
            return value;
        }

        if (element.Patterns.Text.TryGetPattern(out ITextPattern? textPattern))
        {
            return textPattern.DocumentRange.GetText(int.MaxValue);
        }

        return element.Name;
    }

    private static VirtualKeyShort ParseKey(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "BACKSPACE" => VirtualKeyShort.BACK,
            "DELETE" => VirtualKeyShort.DELETE,
            "DOWN" or "ARROWDOWN" => VirtualKeyShort.DOWN,
            "END" => VirtualKeyShort.END,
            "ENTER" or "RETURN" => VirtualKeyShort.ENTER,
            "ESC" or "ESCAPE" => VirtualKeyShort.ESCAPE,
            "HOME" => VirtualKeyShort.HOME,
            "LEFT" or "ARROWLEFT" => VirtualKeyShort.LEFT,
            "PAGEDOWN" => VirtualKeyShort.NEXT,
            "PAGEUP" => VirtualKeyShort.PRIOR,
            "RIGHT" or "ARROWRIGHT" => VirtualKeyShort.RIGHT,
            "SPACE" => VirtualKeyShort.SPACE,
            "TAB" => VirtualKeyShort.TAB,
            "UP" or "ARROWUP" => VirtualKeyShort.UP,
            _ => throw Failure(DesktopAutomationErrorCodes.DesktopActionFailed, "Unsupported desktop key.", "press"),
        };
    }

    private int EffectiveTimeout(int? timeoutMilliseconds)
    {
        return timeoutMilliseconds is int value ? ValidateTimeout(value) : _defaultTimeoutMilliseconds;
    }

    private static void ValidateElementIndex(int? elementIndex)
    {
        if (elementIndex < 0)
        {
            throw Failure(DesktopAutomationErrorCodes.LocatorCardinalityMismatch, "Desktop elementIndex cannot be negative.", "locator");
        }
    }

    private static int ValidateTimeout(int timeoutMilliseconds)
    {
        return timeoutMilliseconds is > 0 and <= 300000
            ? timeoutMilliseconds
            : throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Desktop timeout must be from 1 through 300000 milliseconds.");
    }

    private static DesktopAutomationException Failure(string code, string message, string operation, Exception? exception = null)
    {
        return new DesktopAutomationException(new DesktopOperationError(code, message, operation), exception);
    }
}
