namespace RackPeek.Domain.Graph;

public record GraphNode(
    string Id,
    string Label,
    string Kind,
    string? Subtitle = null,
    IReadOnlyDictionary<string, string>? Data = null);

public record GraphEdge(
    string Source,
    string Target,
    string? Label,
    string Kind,
    IReadOnlyDictionary<string, string>? Data = null);

/// <summary>
///     A labelled cluster of nodes. Used to drive Mermaid <c>subgraph</c>
///     blocks. Groups may nest via <see cref="ParentGroupId"/>.
/// </summary>
public record GraphGroup(
    string Id,
    string Label,
    IReadOnlyList<string> NodeIds,
    string? ParentGroupId = null);

public record Graph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<GraphGroup>? Groups = null) {
    public static Graph Empty { get; } = new([], [], null);
}
