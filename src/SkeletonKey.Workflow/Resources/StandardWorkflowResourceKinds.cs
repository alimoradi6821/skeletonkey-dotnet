namespace SkeletonKey.Workflow.Resources;

/// <summary>
/// Provides standard provider-neutral workflow resource kind identifiers.
/// </summary>
public static class StandardWorkflowResourceKinds
{
    /// <summary>Identifies a web browser resource requirement.</summary>
    public const string WebBrowser = "web.browser";

    /// <summary>Identifies a web browser context resource requirement.</summary>
    public const string WebContext = "web.context";

    /// <summary>Identifies a web page resource requirement.</summary>
    public const string WebPage = "web.page";

    /// <summary>Identifies a human interaction handler resource requirement.</summary>
    public const string InteractionHandler = "interaction.handler";
}
