using System.Text;

namespace RackPeek.Domain.Graph.Serialisers;

/// <summary>
///     Renders a <see cref="Graph"/> as a Mermaid flowchart string.
///     Output is deterministic (nodes/edges in insertion order) so the
///     same inventory always produces the same diagram — important for
///     golden-file tests and for committing rendered diagrams to docs.
/// </summary>
public sealed class MermaidSerialiser {
    // Single neutral palette for a sleek monochrome look. Resource kind is
    // signalled by node shape, not colour, so diagrams stay calm even with
    // every kind of resource mixed in.
    private const string _nodeFill = "#1f2937";    // gray-800
    private const string _nodeStroke = "#52525b";  // zinc-600
    private const string _nodeText = "#e5e7eb";    // gray-200
    private const string _edgeStroke = "#52525b";  // zinc-600
    private const string _groupStroke = "#3f3f46"; // zinc-700
    private const string _groupText = "#a1a1aa";   // zinc-400
    private const string _nodeClass = "rpknode";
    private const string _groupClass = "rpkgroup";

    // Mermaid node shape per resource kind. Shape choice borrows from the
    // network-diagram conventions used by NetBox/draw.io/UniFi: hexagons for
    // security boundaries, stadiums for gateways, cylinders for compute,
    // circles for radios, etc. Looking at the silhouette alone should hint
    // at the role without colour or icons.
    private static readonly IReadOnlyDictionary<string, Shape> _shapes =
        new Dictionary<string, Shape>(StringComparer.OrdinalIgnoreCase) {
            // Physical / topology view shapes
            ["Firewall"] = new("{{\"", "\"}}"),    // hexagon — boundary
            ["Router"] = new("([\"", "\"])"),      // stadium — gateway
            ["Switch"] = new("[[\"", "\"]]"),      // subroutine — distribution
            ["Server"] = new("[(\"", "\")]"),      // cylinder — compute / storage
            ["AccessPoint"] = new("((\"", "\"))"), // circle — radio
            ["Ups"] = new("{\"", "\"}"),           // rhombus — utility
            ["Desktop"] = new("(\"", "\")"),       // rounded rect — endpoint
            ["Laptop"] = new("(\"", "\")"),        // rounded rect — endpoint

            // Logical / service view shapes (don't appear with the physical
            // kinds in the same diagram, so shape reuse across views is OK)
            ["Service"] = new("[[\"", "\"]]"),     // subroutine — consumable
            ["Hypervisor"] = new("([\"", "\"])"),  // stadium — host gateway
            ["Vm"] = new("(\"", "\")"),            // rounded — virtual machine
            ["Container"] = new("{{\"", "\"}}"),   // hexagon — lightweight unit
            ["System"] = new("[\"", "\"]")         // plain rect — fallback
        };

    private static readonly Shape _fallbackShape = new("[\"", "\"]");

    public string Serialise(Graph graph, string direction = "TD") {
        var sb = new StringBuilder();

        // Right-angle (Manhattan) edge routing — the visual signal that says
        // "this is a network diagram", borrowed from every serious topology
        // tool. Diagonal/curved lines read as "flowchart".
        //
        // Edge-label background is made transparent so connection labels read
        // as floating annotations rather than chunky chips that fight with
        // the line and the nodes for attention.
        // ELK renderer + orthogonal edge routing — Mermaid's default `dagre`
        // layout is fine for simple flowcharts but produces awkward arrow
        // landings on right-angle edges. ELK (Eclipse Layout Kernel) is the
        // engine NetBox/yEd/draw.io rely on for clean topology routing.
        //
        // Spacing values are generous on purpose — homelab diagrams read
        // better with air around nodes and between subnet/host clusters.
        sb.AppendLine(
            "%%{init: {'flowchart': {'defaultRenderer': 'elk', 'curve': 'step', 'nodeSpacing': 60, 'rankSpacing': 80, 'padding': 20, 'subGraphTitleMargin': {'top': 12, 'bottom': 12}}, 'themeVariables': {'edgeLabelBackground': 'transparent', 'clusterBkg': 'transparent', 'clusterBorder': '" + _groupStroke + "'}}}%%");
        sb.Append("flowchart ").AppendLine(direction);

        EmitClassDefs(sb);

        Dictionary<string, string> idMap = AssignSafeIds(graph.Nodes);

        // Index groups & nodes for hierarchical emission.
        IReadOnlyList<GraphGroup> groups = graph.Groups ?? [];
        var childGroups = groups
            .GroupBy(g => g.ParentGroupId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());
        var groupsById = groups.ToDictionary(g => g.Id);
        HashSet<string> groupedNodeIds = new(
            groups.SelectMany(g => g.NodeIds), StringComparer.OrdinalIgnoreCase);

        // Emit top-level groups (parentGroupId == null/empty) — each recursively
        // contains its sub-groups and direct nodes.
        if (childGroups.TryGetValue(string.Empty, out List<GraphGroup>? topLevel))
            foreach (GraphGroup group in topLevel)
                EmitGroup(sb, group, childGroups, groupsById, graph.Nodes, idMap, indent: 1);

        // Emit any nodes that didn't fall into a group at the top level.
        foreach (GraphNode node in graph.Nodes) {
            if (groupedNodeIds.Contains(node.Id)) continue;
            EmitNode(sb, node, idMap, indent: 1);
        }

        if (graph.Edges.Count > 0) sb.AppendLine();

        foreach (GraphEdge edge in graph.Edges) {
            if (!idMap.TryGetValue(edge.Source, out var src) ||
                !idMap.TryGetValue(edge.Target, out var dst))
                continue;

            // Directional edges (runsOn, depends-on …) get an arrowhead so
            // the relationship reads correctly. Symmetric edges (port-to-port
            // physical connections) stay as plain lines.
            var connector = IsDirectional(edge.Kind) ? "-->" : "---";

            sb.Append("    ").Append(src);
            if (!string.IsNullOrWhiteSpace(edge.Label))
                sb.Append(' ').Append(connector).Append("|\"")
                    .Append(Escape(edge.Label)).Append("\"|");
            else
                sb.Append(' ').Append(connector);
            sb.Append(' ').Append(dst).AppendLine();
        }

        // Dotted edges matching the dotted node borders. Labels float on top
        // (themeVariables.edgeLabelBackground=transparent) so the line stays
        // visually continuous through the label region.
        if (graph.Edges.Count > 0) {
            sb.AppendLine();
            sb.Append("    linkStyle default stroke:").Append(_edgeStroke)
                .AppendLine(",stroke-width:1.25px,stroke-dasharray:4 4,fill:none");
        }

        // Apply the group styling class to every subgraph id.
        foreach (GraphGroup group in groups) {
            sb.Append("    class ").Append(group.Id).Append(' ').Append(_groupClass).AppendLine();
        }

        return sb.ToString();
    }

    private void EmitGroup(
        StringBuilder sb,
        GraphGroup group,
        Dictionary<string, List<GraphGroup>> childGroups,
        Dictionary<string, GraphGroup> groupsById,
        IReadOnlyList<GraphNode> allNodes,
        Dictionary<string, string> idMap,
        int indent) {
        var pad = new string(' ', indent * 4);
        sb.Append(pad).Append("subgraph ").Append(group.Id)
            .Append(" [\"").Append(Escape(group.Label)).Append("\"]")
            .AppendLine();

        // Nested groups first
        if (childGroups.TryGetValue(group.Id, out List<GraphGroup>? children))
            foreach (GraphGroup child in children)
                EmitGroup(sb, child, childGroups, groupsById, allNodes, idMap, indent + 1);

        // Nodes that belong to this group directly (not via a child group)
        HashSet<string> nodesInChildren = new(
            (children ?? []).SelectMany(c => CollectAllNodeIds(c, childGroups)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var nodeId in group.NodeIds) {
            if (nodesInChildren.Contains(nodeId)) continue;
            GraphNode? node = allNodes.FirstOrDefault(n =>
                string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
            if (node is null) continue;
            EmitNode(sb, node, idMap, indent + 1);
        }

        sb.Append(pad).AppendLine("end");
    }

    private static IEnumerable<string> CollectAllNodeIds(
        GraphGroup group,
        Dictionary<string, List<GraphGroup>> childGroups) {
        foreach (var id in group.NodeIds) yield return id;
        if (!childGroups.TryGetValue(group.Id, out List<GraphGroup>? children)) yield break;
        foreach (GraphGroup c in children)
            foreach (var id in CollectAllNodeIds(c, childGroups))
                yield return id;
    }

    private void EmitNode(StringBuilder sb, GraphNode node, Dictionary<string, string> idMap, int indent) {
        var safeId = idMap[node.Id];
        Shape shape = ResolveShape(node.Kind);
        var label = BuildLabel(node);
        sb.Append(new string(' ', indent * 4)).Append(safeId)
            .Append(shape.Open).Append(label).Append(shape.Close)
            .Append(":::").Append(_nodeClass)
            .AppendLine();
    }

    private static string BuildLabel(GraphNode node) {
        // Two-line label: resource name on top, optional subtitle below.
        // Each use case decides what's most useful as a subtitle (kind for
        // the topology view, ip[:port] for the logical view) — the serialiser
        // is agnostic.
        var name = Escape(node.Label);
        if (string.IsNullOrWhiteSpace(node.Subtitle)) return name;
        return $"{name}<br/>{Escape(node.Subtitle!)}";
    }

    private static void EmitClassDefs(StringBuilder sb) {
        // Dotted node borders + dotted edges (via linkStyle below) keep the
        // whole diagram visually quiet — solid borders feel heavier than the
        // information they convey.
        sb.Append("    classDef ").Append(_nodeClass)
            .Append(" fill:").Append(_nodeFill)
            .Append(",stroke:").Append(_nodeStroke)
            .Append(",color:").Append(_nodeText)
            .Append(",stroke-width:1px,stroke-dasharray:3 3")
            .AppendLine();

        // Group containers: dotted outline, no fill, muted title. The cluster
        // background/border theme variables in the init directive cover the
        // built-in Mermaid styling; this class adds the dashed outline.
        sb.Append("    classDef ").Append(_groupClass)
            .Append(" fill:none,stroke:").Append(_groupStroke)
            .Append(",color:").Append(_groupText)
            .Append(",stroke-width:1px,stroke-dasharray:3 3")
            .AppendLine();
        sb.AppendLine();
    }

    private static Dictionary<string, string> AssignSafeIds(IReadOnlyList<GraphNode> nodes) {
        // Mermaid node IDs must be a small alphabet (letters, digits, underscore).
        // Map resource names → deterministic safe IDs, suffixing on collision.
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var taken = new HashSet<string>(StringComparer.Ordinal);

        foreach (GraphNode node in nodes) {
            var baseId = "n_" + Slug(node.Id);
            var candidate = baseId;
            var counter = 2;
            while (!taken.Add(candidate)) candidate = $"{baseId}_{counter++}";
            result[node.Id] = candidate;
        }

        return result;
    }

    private static string Slug(string value) {
        var sb = new StringBuilder(value.Length);
        foreach (var c in value)
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');

        return sb.Length == 0 ? "node" : sb.ToString();
    }

    private static Shape ResolveShape(string kind) =>
        _shapes.TryGetValue(kind, out Shape shape) ? shape : _fallbackShape;

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static readonly HashSet<string> _directionalEdgeKinds = new(StringComparer.OrdinalIgnoreCase) {
        "runsOn",
        "dependsOn"
    };

    private static bool IsDirectional(string kind) =>
        _directionalEdgeKinds.Contains(kind);

    private readonly record struct Shape(string Open, string Close);
}
