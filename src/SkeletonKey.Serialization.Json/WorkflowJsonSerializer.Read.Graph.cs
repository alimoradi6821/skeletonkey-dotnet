using System.Text.Json;
using SkeletonKey.Serialization.Json.Internal;
using SkeletonKey.Workflow.Connections;
using SkeletonKey.Workflow.Designer;

namespace SkeletonKey.Serialization.Json;

public sealed partial class WorkflowJsonSerializer
{
    private static IReadOnlyList<WorkflowConnection> ReadConnections(JsonElement element, string path)
    {
        JsonElement connectionsElement = ReadRequiredProperty(element, "connections", Append(path, "connections"));
        RequireArray(connectionsElement, Append(path, "connections"));
        List<WorkflowConnection> connections = [];
        int index = 0;

        foreach (JsonElement connectionElement in connectionsElement.EnumerateArray())
        {
            string connectionPath = Append(Append(path, "connections"), index);
            if (connectionElement.ValueKind is JsonValueKind.Null)
            {
                throw JsonExceptionFactory.Create("Workflow connection entries cannot be null.", connectionPath);
            }

            connections.Add(ReadConnection(connectionElement, connectionPath));
            index++;
        }

        return connections;
    }

    private static WorkflowConnection ReadConnection(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["from", "to"]);

        return new WorkflowConnection(
            ReadEndpoint(ReadRequiredProperty(element, "from", Append(path, "from")), Append(path, "from")),
            ReadEndpoint(ReadRequiredProperty(element, "to", Append(path, "to")), Append(path, "to")));
    }

    private static WorkflowEndpoint ReadEndpoint(JsonElement element, string path)
    {
        RequireObject(element, path);
        RejectUnknownProperties(element, path, ["node", "port"]);

        return new WorkflowEndpoint(
            ReadRequiredString(element, "node", Append(path, "node")),
            ReadRequiredString(element, "port", Append(path, "port")));
    }

    private static WorkflowDesignerMetadata? ReadDesigner(JsonElement element, string path)
    {
        if (!element.TryGetProperty("designer", out JsonElement designerElement))
        {
            return null;
        }

        string designerPath = Append(path, "designer");
        if (designerElement.ValueKind is JsonValueKind.Null)
        {
            return null;
        }

        RequireObject(designerElement, designerPath);
        RejectUnknownProperties(designerElement, designerPath, ["positions", "sizes"]);

        return new WorkflowDesignerMetadata(
            ReadPositions(designerElement, designerPath),
            ReadSizes(designerElement, designerPath));
    }

    private static IReadOnlyDictionary<string, WorkflowNodePosition> ReadPositions(JsonElement element, string path)
    {
        if (!element.TryGetProperty("positions", out JsonElement positionsElement))
        {
            return new Dictionary<string, WorkflowNodePosition>();
        }

        string positionsPath = Append(path, "positions");
        RequireObject(positionsElement, positionsPath);
        Dictionary<string, WorkflowNodePosition> positions = new(StringComparer.Ordinal);

        foreach (JsonProperty positionProperty in positionsElement.EnumerateObject())
        {
            string positionPath = Append(positionsPath, positionProperty.Name);
            RequireObject(positionProperty.Value, positionPath);
            RejectUnknownProperties(positionProperty.Value, positionPath, ["x", "y"]);
            positions[positionProperty.Name] = new WorkflowNodePosition(
                ReadRequiredDouble(positionProperty.Value, "x", Append(positionPath, "x")),
                ReadRequiredDouble(positionProperty.Value, "y", Append(positionPath, "y")));
        }

        return positions;
    }

    private static IReadOnlyDictionary<string, WorkflowNodeSize> ReadSizes(JsonElement element, string path)
    {
        if (!element.TryGetProperty("sizes", out JsonElement sizesElement))
        {
            return new Dictionary<string, WorkflowNodeSize>();
        }

        string sizesPath = Append(path, "sizes");
        RequireObject(sizesElement, sizesPath);
        Dictionary<string, WorkflowNodeSize> sizes = new(StringComparer.Ordinal);

        foreach (JsonProperty sizeProperty in sizesElement.EnumerateObject())
        {
            string sizePath = Append(sizesPath, sizeProperty.Name);
            RequireObject(sizeProperty.Value, sizePath);
            RejectUnknownProperties(sizeProperty.Value, sizePath, ["width", "height"]);
            sizes[sizeProperty.Name] = new WorkflowNodeSize(
                ReadRequiredDouble(sizeProperty.Value, "width", Append(sizePath, "width")),
                ReadRequiredDouble(sizeProperty.Value, "height", Append(sizePath, "height")));
        }

        return sizes;
    }
}
