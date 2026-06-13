using RackPeek.Domain.Persistence;
using RackPeek.Domain.Resources;
using RackPeek.Domain.Resources.Connections;
using RackPeek.Domain.Resources.Hardware;
using RackPeek.Domain.Resources.Servers;
using RackPeek.Domain.Resources.SubResources;

namespace RackPeek.Domain.Graph.UseCases;

public class BuildPhysicalTopologyUseCase(IResourceCollection repo) : IUseCase {
    public async Task<Graph> ExecuteAsync() {
        IReadOnlyList<Hardware> hardware = repo.HardwareResources;
        IReadOnlyList<Connection> connections = await repo.GetConnectionsAsync();

        var nodes = hardware
            .OrderBy(h => h.Kind, StringComparer.OrdinalIgnoreCase)
            .ThenBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
            .Select(BuildNode)
            .ToList();

        var hardwareByName = hardware.ToDictionary(
            h => h.Name,
            StringComparer.OrdinalIgnoreCase);

        var edges = connections
            .Where(c => hardwareByName.ContainsKey(c.A.Resource) && hardwareByName.ContainsKey(c.B.Resource))
            .Select(c => BuildEdge(c, hardwareByName))
            .ToList();

        return new Graph(nodes, edges);
    }

    private static GraphNode BuildNode(Hardware resource) {
        var data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (resource.Tags.Length > 0) data["tags"] = string.Join(",", resource.Tags);

        return new GraphNode(
            Id: resource.Name,
            Label: resource.Name,
            Kind: resource.Kind,
            Subtitle: resource.Kind.ToLowerInvariant(),
            Data: data);
    }

    private static GraphEdge BuildEdge(Connection c, Dictionary<string, Hardware> hardwareByName) {
        var label = BuildEdgeLabel(c, hardwareByName);
        return new GraphEdge(
            Source: c.A.Resource,
            Target: c.B.Resource,
            Label: label,
            Kind: "connection");
    }

    private static string? BuildEdgeLabel(Connection c, Dictionary<string, Hardware> hardwareByName) {
        if (!string.IsNullOrWhiteSpace(c.Label))
            return c.Label;

        var a = PortLabel(c.A, hardwareByName);
        var b = PortLabel(c.B, hardwareByName);

        if (a is null && b is null) return null;
        return $"{a ?? "?"} ↔ {b ?? "?"}";
    }

    private static string? PortLabel(PortReference reference, Dictionary<string, Hardware> hardwareByName) {
        if (!hardwareByName.TryGetValue(reference.Resource, out Hardware? hardware))
            return null;

        if (hardware is not IPortResource portResource || portResource.Ports is null)
            return null;

        if (reference.PortGroup < 0 || reference.PortGroup >= portResource.Ports.Count)
            return null;

        Port group = portResource.Ports[reference.PortGroup];
        var type = string.IsNullOrWhiteSpace(group.Type) ? "port" : group.Type;
        return $"{type}{reference.PortIndex}";
    }
}
