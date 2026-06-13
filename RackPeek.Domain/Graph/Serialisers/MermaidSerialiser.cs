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
    private const string _smallRowClass = "rpkrow";

    // Compact-mode (logical view) tuning. Small-row size controls how many
    // single-service host cards pack into one invisible row before wrapping.
    private const int _compactSmallRowSize = 4;
    private const int _compactTableColumns = 3;

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
        if (graph.RenderHint == GraphRenderHint.Compact)
            return SerialiseCompact(graph, direction);

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
        // - `layout: elk`              : use the Mermaid 11 ELK plugin (the
        //                                older `flowchart.defaultRenderer`
        //                                still works but is the legacy path).
        // - `elk.aspectRatio: 0.5`     : ask ELK to favour tall over wide so
        //                                a host with dozens of services
        //                                doesn't fan out into a single row
        //                                kilometres long.
        // - `layered.wrapping.strategy : MULTI_EDGE
        //                                wraps an overlong layer into several
        //                                shorter ones — exactly what large
        //                                logical/service diagrams need.
        sb.AppendLine(
            "%%{init: {'layout': 'elk', 'flowchart': {'curve': 'step', 'nodeSpacing': 60, 'rankSpacing': 80, 'padding': 20, 'subGraphTitleMargin': {'top': 12, 'bottom': 12}}, 'elk': {'algorithm': 'layered', 'aspectRatio': 0.5, 'layered.wrapping.strategy': 'MULTI_EDGE', 'layered.nodePlacement.strategy': 'BRANDES_KOEPF'}, 'themeVariables': {'edgeLabelBackground': 'transparent', 'clusterBkg': 'transparent', 'clusterBorder': '" + _groupStroke + "'}}}%%");
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

    // ---------------------------------------------------------------------
    // Compact mode (logical view): each system becomes a single "host card"
    // whose label is an HTML table of its services. No edges are drawn —
    // subgraph containment carries the runs-on relationship. Sibling cards
    // are chained vertically via invisible ~~~ links so ELK doesn't fan
    // them out into a kilometre-wide row, and single-row hosts are packed
    // into invisible row subgraphs of N to use the horizontal space.
    // ---------------------------------------------------------------------
    private string SerialiseCompact(Graph graph, string direction) {
        var sb = new StringBuilder();

        // htmlLabels + securityLevel: 'loose' let us put raw HTML inside the
        // node labels. aspectRatio is set above 0.5 because compact mode
        // already wraps long sibling lists itself via the small-row packing.
        sb.AppendLine(
            "%%{init: {'layout': 'elk', 'flowchart': {'curve': 'step', 'nodeSpacing': 10, 'rankSpacing': 10, 'padding': 0, 'htmlLabels': true, 'subGraphTitleMargin': {'top': 0, 'bottom': 0}, 'titleTopMargin': 0}, 'securityLevel': 'loose', 'elk': {'algorithm': 'layered', 'padding': '[top=0,bottom=4,left=6,right=6]', 'spacing.nodeNode': 8, 'spacing.nodeNodeBetweenLayers': 8, 'spacing.componentComponent': 6, 'layered.spacing.nodeNodeBetweenLayers': 8, 'nodeLabels.placement': '[H_CENTER, V_TOP, INSIDE]'}, 'themeVariables': {'edgeLabelBackground': 'transparent', 'clusterBkg': 'transparent', 'clusterBorder': '" + _groupStroke + "'}}}%%");
        sb.Append("flowchart ").AppendLine(direction);

        EmitClassDefs(sb);
        sb.Append("    classDef ").Append(_smallRowClass)
            .AppendLine(" fill:none,stroke:none,color:transparent");
        sb.AppendLine();

        Dictionary<string, string> idMap = AssignSafeIds(graph.Nodes);

        IReadOnlyList<GraphGroup> groups = graph.Groups ?? [];
        var childGroups = groups
            .GroupBy(g => g.ParentGroupId ?? string.Empty)
            .ToDictionary(g => g.Key, g => g.ToList());
        HashSet<string> groupedNodeIds = new(
            groups.SelectMany(g => g.NodeIds), StringComparer.OrdinalIgnoreCase);

        // Invisible chains and packed-row ids are collected during traversal
        // and emitted in a block at the end.
        var chains = new List<IReadOnlyList<string>>();
        var smallRowIds = new List<string>();

        void Emit(GraphGroup group, int indent) {
            var pad = new string(' ', indent * 4);
            sb.Append(pad).Append("subgraph ").Append(group.Id)
                .Append(" [\"").Append(Escape(group.Label)).Append("\"]").AppendLine();

            List<GraphGroup> subChildren =
                childGroups.TryGetValue(group.Id, out List<GraphGroup>? cs) ? cs : new();
            foreach (GraphGroup child in subChildren) Emit(child, indent + 1);
            if (subChildren.Count > 1)
                chains.Add(subChildren.Select(c => c.Id).ToList());

            HashSet<string> nodesInChildren = new(
                subChildren.SelectMany(c => CollectAllNodeIds(c, childGroups)),
                StringComparer.OrdinalIgnoreCase);

            // Partition the group's direct nodes into "big" cards (host with
            // multiple service rows) and "small" cards (no rows or one row).
            // Bigs get a dedicated row each; smalls pack horizontally.
            var bigs = new List<GraphNode>();
            var smalls = new List<GraphNode>();
            foreach (var nodeId in group.NodeIds) {
                if (nodesInChildren.Contains(nodeId)) continue;
                GraphNode? node = graph.Nodes.FirstOrDefault(n =>
                    string.Equals(n.Id, nodeId, StringComparison.OrdinalIgnoreCase));
                if (node is null) continue;
                if ((node.Rows?.Count ?? 0) > 1) bigs.Add(node);
                else smalls.Add(node);
            }

            var verticalChain = new List<string>();

            foreach (GraphNode b in bigs) {
                EmitCompactNode(sb, b, idMap, indent + 1);
                verticalChain.Add(idMap[b.Id]);
            }

            for (int i = 0, rowIdx = 0; i < smalls.Count; i += _compactSmallRowSize, rowIdx++) {
                var slice = smalls.Skip(i).Take(_compactSmallRowSize).ToList();
                // Single small host doesn't need an invisible row wrapper —
                // wrapping adds another nested subgraph (with its own
                // padding/title overhead) for no layout benefit.
                if (slice.Count == 1) {
                    EmitCompactNode(sb, slice[0], idMap, indent + 1);
                    verticalChain.Add(idMap[slice[0].Id]);
                    continue;
                }
                var rowId = group.Id + "__srow" + rowIdx;
                smallRowIds.Add(rowId);
                verticalChain.Add(rowId);
                sb.Append(pad).Append("    subgraph ").Append(rowId).AppendLine(" [\" \"]");
                sb.Append(pad).Append("        direction LR").AppendLine();
                foreach (GraphNode s in slice)
                    EmitCompactNode(sb, s, idMap, indent + 2);
                sb.Append(pad).AppendLine("    end");
                sb.Append(pad).Append("    ");
                sb.AppendJoin(" ~~~ ", slice.Select(s => idMap[s.Id]));
                sb.AppendLine();
            }

            if (verticalChain.Count > 1) chains.Add(verticalChain);

            sb.Append(pad).AppendLine("end");
        }

        if (childGroups.TryGetValue(string.Empty, out List<GraphGroup>? topLevel)) {
            foreach (GraphGroup g in topLevel) Emit(g, 1);
            if (topLevel.Count > 1)
                chains.Add(topLevel.Select(g => g.Id).ToList());
        }

        // Ungrouped nodes (uncommon in compact mode but render them sanely).
        foreach (GraphNode node in graph.Nodes) {
            if (groupedNodeIds.Contains(node.Id)) continue;
            EmitCompactNode(sb, node, idMap, 1);
        }

        // Invisible vertical chains last — these are what tell ELK to stack
        // siblings vertically instead of flowing into one long row.
        if (chains.Count > 0) sb.AppendLine();
        foreach (IReadOnlyList<string> chain in chains) {
            if (chain.Count < 2) continue;
            sb.Append("    ");
            sb.AppendJoin(" ~~~ ", chain);
            sb.AppendLine();
        }

        sb.AppendLine();
        foreach (GraphGroup group in groups)
            sb.Append("    class ").Append(group.Id).Append(' ').Append(_groupClass).AppendLine();
        foreach (var rowId in smallRowIds)
            sb.Append("    class ").Append(rowId).Append(' ').Append(_smallRowClass).AppendLine();

        return sb.ToString();
    }

    private void EmitCompactNode(StringBuilder sb, GraphNode node, Dictionary<string, string> idMap, int indent) {
        var safeId = idMap[node.Id];
        Shape shape = ResolveShape(node.Kind);
        var label = BuildCompactLabel(node);
        sb.Append(new string(' ', indent * 4)).Append(safeId)
            .Append(shape.Open).Append(label).Append(shape.Close)
            .Append(":::").Append(_nodeClass)
            .AppendLine();
    }

    private static string BuildCompactLabel(GraphNode node) {
        var sb = new StringBuilder();
        sb.Append("<div style='text-align:left;font-family:system-ui;padding:4px 6px'>");
        sb.Append("<div style='font-weight:600;font-size:14px'>");
        sb.Append(EscapeHtml(node.Label));
        if (!string.IsNullOrWhiteSpace(node.Subtitle)) {
            sb.Append(" - <span style='color:#9ca3af'>");
            sb.Append(EscapeHtml(node.Subtitle!));
            sb.Append("</span>");
        }
        sb.Append("</div>");

        if (node.Rows is { Count: > 0 }) {
            sb.Append("<hr style='border:none;border-top:1px dashed #52525b;margin:6px 0'>");
            sb.Append("<table style='border-collapse:collapse;font-size:11px'>");
            for (var i = 0; i < node.Rows.Count; i += _compactTableColumns) {
                sb.Append("<tr>");
                for (var c = 0; c < _compactTableColumns; c++) {
                    var idx = i + c;
                    if (idx >= node.Rows.Count) { sb.Append("<td></td>"); continue; }
                    GraphNodeRow row = node.Rows[idx];
                    sb.Append("<td style='padding:2px 10px 2px 0;white-space:nowrap'>");
                    sb.Append("<span style='color:#e5e7eb'>").Append(EscapeHtml(row.Name)).Append("</span>");
                    if (!string.IsNullOrEmpty(row.Detail))
                        sb.Append("<span style='color:#71717a'>").Append(EscapeHtml(row.Detail!)).Append("</span>");
                    sb.Append("</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
        }
        sb.Append("</div>");
        // Mermaid label is wrapped in "...", so any " in our HTML must be
        // entity-encoded. We avoid literal " in inline styles by using
        // single quotes; this last pass catches anything still embedded.
        return sb.ToString().Replace("\"", "&quot;");
    }

    private static string EscapeHtml(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
