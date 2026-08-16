namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Provides standard provider-neutral workflow resource capability identifiers.
/// </summary>
public static class StandardWorkflowResourceCapabilities
{
    /// <summary>Requires a persistent web browser profile.</summary>
    public const string WebPersistentProfile = "web.persistent-profile";

    /// <summary>Requires an ephemeral web browser profile.</summary>
    public const string WebEphemeralProfile = "web.ephemeral-profile";

    /// <summary>Requires a headful web browser.</summary>
    public const string WebHeadful = "web.headful";

    /// <summary>Requires a headless web browser.</summary>
    public const string WebHeadless = "web.headless";

    /// <summary>Requires support for multiple browser pages.</summary>
    public const string WebMultiplePages = "web.multiple-pages";

    /// <summary>Requires browser download support.</summary>
    public const string WebDownloads = "web.downloads";

    /// <summary>Requires frame-aware browser automation support.</summary>
    public const string WebFrames = "web.frames";

    /// <summary>Requires shadow DOM browser automation support.</summary>
    public const string WebShadowDom = "web.shadow-dom";

    /// <summary>Requires web page navigation.</summary>
    public const string WebNavigation = "web.navigation";

    /// <summary>Requires web element actions.</summary>
    public const string WebActions = "web.actions";

    /// <summary>Requires web locator resolution.</summary>
    public const string WebLocators = "web.locators";

    /// <summary>Requires text extraction.</summary>
    public const string WebText = "web.text";

    /// <summary>Requires attribute extraction.</summary>
    public const string WebAttributes = "web.attributes";

    /// <summary>Requires form interaction.</summary>
    public const string WebForms = "web.forms";

    /// <summary>Requires screenshot capture.</summary>
    public const string WebScreenshot = "web.screenshot";

    /// <summary>Requires confirmation interactions.</summary>
    public const string InteractionConfirmation = "interaction.confirmation";

    /// <summary>Requires text input interactions.</summary>
    public const string InteractionTextInput = "interaction.text-input";

    /// <summary>Requires secret text input interactions.</summary>
    public const string InteractionSecretInput = "interaction.secret-input";

    /// <summary>Requires choice input interactions.</summary>
    public const string InteractionChoiceInput = "interaction.choice-input";

    /// <summary>Requires manual action interactions.</summary>
    public const string InteractionManualAction = "interaction.manual-action";
}
