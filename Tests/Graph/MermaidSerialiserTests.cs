using RackPeek.Domain.Graph;
using RackPeek.Domain.Graph.Serialisers;

namespace Tests.Graph;

public sealed class MermaidSerialiserTests {
    private readonly MermaidSerialiser _serialiser = new();

    [Fact]
    public void Empty_Graph_Renders_Header_And_ClassDef_Only() {
        var output = _serialiser.Serialise(RackPeek.Domain.Graph.Graph.Empty);

        Assert.Contains("flowchart TD", output);
        Assert.Contains("classDef rpknode", output);
        // No node lines because the only content after the classDef is blank.
        Assert.DoesNotContain("[\"", output);
    }

    [Fact]
    public void Renders_Step_Curve_Init_Directive() {
        // Right-angle (Manhattan) edge routing — the convention for network
        // diagrams. Anything else (linear/curved) reads as a flowchart.
        var output = _serialiser.Serialise(RackPeek.Domain.Graph.Graph.Empty);
        Assert.Contains("'curve': 'step'", output);
    }

    [Fact]
    public void Direction_Override_Is_Honoured() {
        var output = _serialiser.Serialise(RackPeek.Domain.Graph.Graph.Empty, "LR");
        Assert.Contains("flowchart LR", output);
    }

    [Fact]
    public void Node_Renders_Name_Only_When_No_Subtitle() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("srv-01", "srv-01", "Server")],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_srv_01[(\"srv-01\")]:::rpknode", output);
        Assert.DoesNotContain("srv-01<br/>", output);
    }

    [Fact]
    public void Subtitle_Renders_As_Second_Label_Line() {
        // The serialiser is agnostic — each use case decides what's useful
        // as a subtitle (kind for topology, ip[:port] for logical view).
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("srv-01", "srv-01", "Server", Subtitle: "192.168.0.10:8080")],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_srv_01[(\"srv-01<br/>192.168.0.10:8080\")]:::rpknode", output);
    }

    [Theory]
    [InlineData("Firewall", "{{\"", "\"}}")]    // hexagon — security boundary
    [InlineData("Router", "([\"", "\"])")]      // stadium — gateway
    [InlineData("Switch", "[[\"", "\"]]")]      // subroutine — distribution
    [InlineData("Server", "[(\"", "\")]")]      // cylinder — compute/storage
    [InlineData("AccessPoint", "((\"", "\"))")] // circle — radio
    [InlineData("Ups", "{\"", "\"}")]           // rhombus — utility
    [InlineData("Desktop", "(\"", "\")")]       // rounded rect — endpoint
    [InlineData("Laptop", "(\"", "\")")]        // rounded rect — endpoint
    public void Kind_Maps_To_Documented_Mermaid_Shape(string kind, string openBracket, string closeBracket) {
        // Shape conveys role at a glance without colour or icons — pin the
        // mapping so a future refactor can't silently change diagrams.
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("x", "x", kind)],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains($"n_x{openBracket}x{closeBracket}", output);
    }

    [Fact]
    public void Unknown_Kind_Falls_Back_To_Plain_Rectangle() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("x", "x", "Toaster")],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_x[\"x\"]:::rpknode", output);
    }

    [Fact]
    public void All_Nodes_Share_A_Single_Visual_Class_Regardless_Of_Kind() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [
                new GraphNode("a", "a", "Firewall"),
                new GraphNode("b", "b", "Server"),
                new GraphNode("c", "c", "Mystery")
            ],
            []);

        var output = _serialiser.Serialise(graph);

        // All three classed identically — no per-kind colour.
        Assert.Equal(3, CountOccurrences(output, ":::rpknode"));
    }

    [Fact]
    public void No_Emoji_Or_Icon_Appears_In_Output() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [
                new GraphNode("srv-01", "srv-01", "Server"),
                new GraphNode("fw-01", "fw-01", "Firewall"),
                new GraphNode("sw-01", "sw-01", "Switch")
            ],
            []);

        var output = _serialiser.Serialise(graph);

        // Pin against the previous emoji-y design.
        Assert.DoesNotContain("🖥", output);
        Assert.DoesNotContain("🛡", output);
        Assert.DoesNotContain("🔀", output);
    }

    [Fact]
    public void Edge_With_Label_Renders_With_Pipe_Syntax() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server"), new GraphNode("b", "b", "Switch")],
            [new GraphEdge("a", "b", "eth0 ↔ port1", "connection")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_a ---|\"eth0 ↔ port1\"| n_b", output);
    }

    [Fact]
    public void Edge_Without_Label_Renders_Plain_Line() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server"), new GraphNode("b", "b", "Switch")],
            [new GraphEdge("a", "b", null, "connection")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_a --- n_b", output);
    }

    [Fact]
    public void RunsOn_Edge_Gets_An_Arrow() {
        // Directional relationships need a visible arrowhead so the reader
        // can tell which side depends on which.
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("svc", "svc", "Service"), new GraphNode("vm", "vm", "Vm")],
            [new GraphEdge("svc", "vm", null, "runsOn")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_svc --> n_vm", output);
        Assert.DoesNotContain("n_svc --- n_vm", output);
    }

    [Fact]
    public void Connection_Edge_Stays_Plain_Line() {
        // Port-to-port physical connections are symmetric; no arrow.
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server"), new GraphNode("b", "b", "Switch")],
            [new GraphEdge("a", "b", null, "connection")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_a --- n_b", output);
        Assert.DoesNotContain("n_a --> n_b", output);
    }

    [Fact]
    public void Edges_Get_Muted_Stroke_Via_LinkStyle() {
        // A single linkStyle default rule keeps edges visually quiet so they
        // never compete with node labels.
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server"), new GraphNode("b", "b", "Switch")],
            [new GraphEdge("a", "b", null, "connection")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("linkStyle default stroke:", output);
    }

    [Fact]
    public void Node_Borders_Are_Dotted() {
        // Pin the dashed-border styling — keeps the look intentionally light.
        var output = _serialiser.Serialise(RackPeek.Domain.Graph.Graph.Empty);
        Assert.Contains("stroke-dasharray:3 3", output);
    }

    [Fact]
    public void Connection_Lines_Are_Dotted() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server"), new GraphNode("b", "b", "Switch")],
            [new GraphEdge("a", "b", null, "connection")]);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("linkStyle default ", output);
        Assert.Contains("stroke-dasharray:4 4", output);
    }

    [Fact]
    public void Edge_Label_Background_Is_Transparent() {
        // Solid label boxes feel chunky and clip the connection line. A
        // transparent background lets labels read as floating annotations.
        var output = _serialiser.Serialise(RackPeek.Domain.Graph.Graph.Empty);
        Assert.Contains("'edgeLabelBackground': 'transparent'", output);
    }

    private static int CountOccurrences(string haystack, string needle) {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0) {
            count++;
            idx += needle.Length;
        }

        return count;
    }

    [Fact]
    public void Duplicate_Slugs_Get_Disambiguated() {
        // "srv-01" and "srv_01" both slug to "srv_01" — the serialiser must
        // produce distinct IDs so Mermaid doesn't collapse them into one node.
        var graph = new RackPeek.Domain.Graph.Graph(
            [
                new GraphNode("srv-01", "srv-01", "Server"),
                new GraphNode("srv_01", "srv_01", "Server")
            ],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("n_srv_01[", output);
        Assert.Contains("n_srv_01_2[", output);
    }

    [Fact]
    public void Special_Characters_In_Labels_Are_Escaped() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("x", "host \"with\" quotes", "Server")],
            []);

        var output = _serialiser.Serialise(graph);

        Assert.Contains("host \\\"with\\\" quotes", output);
    }

    [Fact]
    public void Edge_Referencing_Missing_Node_Is_Dropped() {
        var graph = new RackPeek.Domain.Graph.Graph(
            [new GraphNode("a", "a", "Server")],
            [new GraphEdge("a", "ghost", null, "connection")]);

        var output = _serialiser.Serialise(graph);

        // Edge with a missing target must be silently dropped — Mermaid would
        // otherwise emit a syntax error and the whole diagram would fail.
        Assert.DoesNotContain("ghost", output);
    }

}
