namespace RackPeek.Domain.Graph;

public record GraphNode(
    string Id,
    string Label,
    string Kind,
    string? Subtitle = null,
    IReadOnlyDictionary<string, string>? Data = null,
    IReadOnlyList<GraphNodeRow>? Rows = null);

/// <summary>
///     A bullet/list row rendered inside a node label. Used by the logical
///     view to fold a host's services into a single host card.
/// </summary>
public record GraphNodeRow(string Name, string? Detail = null);

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

/// <summary>
///     How a graph should be rendered. <see cref="Standard"/> is the
///     hardware-topology view: one node per resource with shape-based kind
///     signalling. <see cref="Compact"/> is the logical-services view:
///     each host is a single card listing its services as rows, no edges,
///     siblings packed vertically.
/// </summary>
public enum GraphRenderHint {
    Standard,
    Compact
}

public record Graph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyList<GraphGroup>? Groups = null,
    GraphRenderHint RenderHint = GraphRenderHint.Standard) {
    public static Graph Empty { get; } = new([], [], null);
}
