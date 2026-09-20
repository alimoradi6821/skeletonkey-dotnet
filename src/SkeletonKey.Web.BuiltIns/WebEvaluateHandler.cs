using System.Text.Json;
using System.Text.Json.Nodes;
using SkeletonKey.Execution;
using SkeletonKey.Handlers;
using SkeletonKey.Web.Abstractions;

namespace SkeletonKey.Web.BuiltIns;

/// <summary>Executes bounded provider-neutral page JavaScript and exposes its JSON-serializable result.</summary>
public sealed class WebEvaluateHandler : WebHandlerBase
{
    private const int MaximumScriptCharacters = 64 * 1024;

    /// <summary>Initializes the handler.</summary>
    public WebEvaluateHandler() : base("web.evaluate") { }

    /// <inheritdoc />
    protected override async ValueTask<NodeHandlerResult> ExecuteWebAsync(
        NodeExecutionRequest request,
        INodeExecutionContext context,
        IWebPageAdapter adapter,
        CancellationToken cancellationToken)
    {
        JsonObject parameters = request.Parameters;
        string script = RequiredString(parameters, "script");
        if (script.Length is 0 or > MaximumScriptCharacters)
        {
            return NodeHandlerResult.Failure(new SkeletonKey.Abstractions.Execution.WorkflowError(
                WebAutomationErrorCodes.WebActionFailed,
                "web.evaluate script must contain between 1 and 65536 characters.",
                request.Identity.NodeId));
        }

        int timeoutMilliseconds = OptionalInt(parameters, "timeoutMilliseconds", 30000);
        if (timeoutMilliseconds is <= 0 or > 300000)
        {
            return NodeHandlerResult.Failure(new SkeletonKey.Abstractions.Execution.WorkflowError(
                WebAutomationErrorCodes.WebActionFailed,
                "web.evaluate timeoutMilliseconds must be between 1 and 300000.",
                request.Identity.NodeId));
        }

        JsonNode? result = await adapter.EvaluateAsync(
            script,
            timeoutMilliseconds,
            TargetContext(context, parameters),
            cancellationToken).ConfigureAwait(false);

        return Main(new Dictionary<string, NodePortValueSet>(StringComparer.Ordinal)
        {
            ["result"] = new([result?.DeepClone()]),
        });
    }
}
