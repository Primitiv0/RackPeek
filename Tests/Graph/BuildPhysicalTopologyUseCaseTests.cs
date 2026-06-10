using NSubstitute;
using RackPeek.Domain.Graph;
using RackPeek.Domain.Graph.UseCases;
using RackPeek.Domain.Persistence;
using RackPeek.Domain.Resources.Connections;
using RackPeek.Domain.Resources.Firewalls;
using RackPeek.Domain.Resources.Hardware;
using RackPeek.Domain.Resources.Servers;
using RackPeek.Domain.Resources.SubResources;
using RackPeek.Domain.Resources.Switches;

namespace Tests.Graph;

public sealed class BuildPhysicalTopologyUseCaseTests {
    private readonly IResourceCollection _repo = Substitute.For<IResourceCollection>();
    private readonly BuildPhysicalTopologyUseCase _useCase;

    public BuildPhysicalTopologyUseCaseTests() {
        _useCase = new BuildPhysicalTopologyUseCase(_repo);
    }

    private void Seed(IReadOnlyList<Hardware> hardware, params Connection[] connections) {
        _repo.HardwareResources.Returns(hardware);
        _repo.GetConnectionsAsync().Returns(connections);
    }

    private static Server Server(string name) => new() { Name = name, Kind = "Server" };
    private static Switch Switch(string name, params Port[] ports) =>
        new() { Name = name, Kind = "Switch", Ports = ports.ToList() };
    private static Firewall Firewall(string name, params Port[] ports) =>
        new() { Name = name, Kind = "Firewall", Ports = ports.ToList() };

    [Fact]
    public async Task Empty_Inventory_Produces_Empty_Graph() {
        Seed([]);

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Empty(graph.Nodes);
        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task Each_Hardware_Resource_Becomes_A_Node() {
        Seed([Server("srv-01"), Switch("sw-01"), Firewall("fw-01")]);

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Equal(3, graph.Nodes.Count);
        Assert.Contains(graph.Nodes, n => n.Id == "srv-01" && n.Kind == "Server");
        Assert.Contains(graph.Nodes, n => n.Id == "sw-01" && n.Kind == "Switch");
        Assert.Contains(graph.Nodes, n => n.Id == "fw-01" && n.Kind == "Firewall");
    }

    [Fact]
    public async Task Nodes_Are_Sorted_For_Deterministic_Output() {
        Seed([Server("srv-02"), Server("srv-01"), Switch("sw-01")]);

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        // Sorted by kind then name for a stable diagram across runs.
        Assert.Equal(new[] { "Server", "Server", "Switch" },
            graph.Nodes.Select(n => n.Kind).ToArray());
        Assert.Equal(new[] { "srv-01", "srv-02", "sw-01" },
            graph.Nodes.Select(n => n.Id).ToArray());
    }

    [Fact]
    public async Task Connection_Between_Two_Hardware_Resources_Becomes_An_Edge() {
        Seed(
            [Server("srv-01"), Switch("sw-01")],
            new Connection {
                A = new PortReference { Resource = "srv-01", PortGroup = 0, PortIndex = 0 },
                B = new PortReference { Resource = "sw-01", PortGroup = 0, PortIndex = 1 }
            });

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        GraphEdge edge = Assert.Single(graph.Edges);
        Assert.Equal("srv-01", edge.Source);
        Assert.Equal("sw-01", edge.Target);
        Assert.Equal("connection", edge.Kind);
    }

    [Fact]
    public async Task Edge_Label_Uses_Explicit_Connection_Label_When_Present() {
        Seed(
            [Server("srv-01"), Switch("sw-01")],
            new Connection {
                Label = "primary uplink",
                A = new PortReference { Resource = "srv-01", PortGroup = 0, PortIndex = 0 },
                B = new PortReference { Resource = "sw-01", PortGroup = 0, PortIndex = 1 }
            });

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Equal("primary uplink", graph.Edges[0].Label);
    }

    [Fact]
    public async Task Edge_Label_Derived_From_Port_Group_Types_When_No_Explicit_Label() {
        Seed(
            [
                Switch("sw-01", new Port { Type = "RJ45", Count = 24 }),
                Firewall("fw-01", new Port { Type = "SFP+", Count = 4 })
            ],
            new Connection {
                A = new PortReference { Resource = "sw-01", PortGroup = 0, PortIndex = 3 },
                B = new PortReference { Resource = "fw-01", PortGroup = 0, PortIndex = 1 }
            });

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Equal("RJ453 ↔ SFP+1", graph.Edges[0].Label);
    }

    [Fact]
    public async Task Connection_Referencing_Unknown_Resource_Is_Dropped() {
        // Defensive: stale connection pointing at a deleted resource shouldn't
        // crash the diagram or produce a dangling edge.
        Seed(
            [Server("srv-01")],
            new Connection {
                A = new PortReference { Resource = "srv-01", PortGroup = 0, PortIndex = 0 },
                B = new PortReference { Resource = "deleted-host", PortGroup = 0, PortIndex = 0 }
            });

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Empty(graph.Edges);
    }

    [Fact]
    public async Task Tags_Are_Carried_Onto_The_Node_For_Future_Filtering() {
        Server server = Server("srv-01");
        server.Tags = ["homelab", "prod"];
        Seed([server]);

        RackPeek.Domain.Graph.Graph graph = await _useCase.ExecuteAsync();

        Assert.Equal("homelab,prod", graph.Nodes[0].Data!["tags"]);
    }
}
