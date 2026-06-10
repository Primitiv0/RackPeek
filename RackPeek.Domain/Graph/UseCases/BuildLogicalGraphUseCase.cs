using RackPeek.Domain.Persistence;
using RackPeek.Domain.Resources;
using RackPeek.Domain.Resources.Hardware;
using RackPeek.Domain.Resources.Services;
using RackPeek.Domain.Resources.Services.Networking;
using RackPeek.Domain.Resources.SystemResources;

namespace RackPeek.Domain.Graph.UseCases;

/// <summary>
///     Logical / service-oriented view: services and systems grouped first
///     by IP subnet (/24), then by their ultimate parent hardware. Edges
///     show the immediate <c>runsOn</c> dependency.
/// </summary>
public class BuildLogicalGraphUseCase(IResourceCollection repo) : IUseCase {
    private const int _defaultPrefix = 24;

    public async Task<Graph> ExecuteAsync() {
        IReadOnlyList<Service> services = await repo.GetAllOfTypeAsync<Service>();
        IReadOnlyList<SystemResource> systems = await repo.GetAllOfTypeAsync<SystemResource>();
        IReadOnlyList<Hardware> hardware = repo.HardwareResources;

        var byName = new Dictionary<string, Resource>(StringComparer.OrdinalIgnoreCase);
        foreach (Hardware hw in hardware) byName[hw.Name] = hw;
        foreach (SystemResource s in systems) byName[s.Name] = s;
        foreach (Service svc in services) byName[svc.Name] = svc;

        // Classify each non-hardware resource: which subnet, which parent host,
        // and what ip[:port] to show as the subtitle.
        var entries = new List<Entry>();
        foreach (Resource resource in services.Cast<Resource>().Concat(systems)) {
            var ip = FindIp(resource, byName);
            var subnet = SubnetCidr(ip, _defaultPrefix);
            Hardware? parentHw = FindParentHardware(resource, byName);
            if (subnet is null) continue; // skip orphans with no IP anywhere up the chain
            var subtitle = BuildSubtitle(resource, ip);
            entries.Add(new Entry(resource, subnet, parentHw?.Name, subtitle));
        }

        var nodes = entries
            .OrderBy(e => e.Subnet, StringComparer.Ordinal)
            .ThenBy(e => e.HardwareName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Resource.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new GraphNode(
                e.Resource.Name, e.Resource.Name, NodeKind(e.Resource), e.Subtitle))
            .ToList();

        List<GraphGroup> groups = BuildGroups(entries);

        // Edges from each resource to its immediate runsOn target if both
        // ends are nodes in the graph. We omit edges that point at hardware
        // (hardware is the grouping label, not a node).
        HashSet<string> nodeIds = new(nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);
        List<GraphEdge> edges = new();
        foreach (Entry entry in entries) {
            var parentName = entry.Resource.RunsOn?.FirstOrDefault();
            if (parentName is null) continue;
            if (!nodeIds.Contains(parentName)) continue;
            edges.Add(new GraphEdge(entry.Resource.Name, parentName, null, "runsOn"));
        }

        return new Graph(nodes, edges, groups);
    }

    private static List<GraphGroup> BuildGroups(IReadOnlyList<Entry> entries) {
        var groups = new List<GraphGroup>();

        IOrderedEnumerable<IGrouping<string, Entry>> bySubnet = entries
            .GroupBy(e => e.Subnet, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (IGrouping<string, Entry> subnetGroup in bySubnet) {
            var subnetId = "g_" + Slug(subnetGroup.Key!);

            // Inner groups keyed by parent hardware. Entries with no parent
            // hardware fall directly into the subnet group.
            var directNodes = new List<string>();
            IOrderedEnumerable<IGrouping<string?, Entry>> byHardware = subnetGroup
                .GroupBy(e => e.HardwareName)
                .OrderBy(g => g.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string?, Entry> hwGroup in byHardware) {
                if (hwGroup.Key is null) {
                    directNodes.AddRange(hwGroup.Select(e => e.Resource.Name));
                    continue;
                }

                var hwGroupId = subnetId + "__" + Slug(hwGroup.Key);
                groups.Add(new GraphGroup(
                    hwGroupId,
                    hwGroup.Key,
                    hwGroup.Select(e => e.Resource.Name).ToList(),
                    subnetId));
            }

            groups.Add(new GraphGroup(subnetId, subnetGroup.Key!, directNodes, null));
        }

        return groups;
    }

    private static string NodeKind(Resource resource) {
        if (resource is Service) return "Service";
        if (resource is SystemResource sys) {
            if (string.IsNullOrWhiteSpace(sys.Type)) return "System";
            // "vm" → "Vm", "hypervisor" → "Hypervisor"; we look these up in the
            // serialiser's shape map case-insensitively, so casing doesn't
            // matter — but a canonical form keeps test assertions tidy.
            var t = sys.Type.Trim().ToLowerInvariant();
            return t switch {
                "hypervisor" => "Hypervisor",
                "vm" => "Vm",
                "container" => "Container",
                _ => "System"
            };
        }

        return resource.Kind;
    }

    private static string? FindIp(Resource resource, Dictionary<string, Resource> byName) {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Resource? current = resource;
        while (current is not null && visited.Add(current.Name)) {
            switch (current) {
                case Service { Network.Ip: { Length: > 0 } svcIp }:
                    return svcIp;
                case SystemResource { Ip: { Length: > 0 } sysIp }:
                    return sysIp;
            }

            var parent = current.RunsOn.FirstOrDefault();
            if (parent is null) return null;
            current = byName.GetValueOrDefault(parent);
        }

        return null;
    }

    private static Hardware? FindParentHardware(Resource resource, Dictionary<string, Resource> byName) {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Resource? current = resource;
        while (current is not null && visited.Add(current.Name)) {
            if (current is Hardware hw) return hw;
            var parent = current.RunsOn.FirstOrDefault();
            if (parent is null) return null;
            current = byName.GetValueOrDefault(parent);
        }

        return null;
    }

    private static string? SubnetCidr(string? ip, int prefix) {
        if (string.IsNullOrWhiteSpace(ip)) return null;
        try {
            var u = IpHelper.ToUInt32(ip);
            var mask = IpHelper.MaskFromPrefix(prefix);
            return $"{IpHelper.ToIp(u & mask)}/{prefix}";
        }
        catch {
            return null;
        }
    }

    private static string Slug(string value) {
        var chars = value.Select(c => char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_').ToArray();
        return new string(chars);
    }

    private static string? BuildSubtitle(Resource resource, string? ip) {
        // Services: ip:port (port from Network.Port if present). The ip is
        // whatever the runsOn chain resolves to — it may belong to the host,
        // not the service itself, but that's the relevant address for users.
        if (resource is Service service) {
            var port = service.Network?.Port;
            if (!string.IsNullOrWhiteSpace(ip) && port.HasValue) return $"{ip}:{port}";
            if (!string.IsNullOrWhiteSpace(ip)) return ip;
            return port.HasValue ? $":{port}" : null;
        }

        // Systems: just their own IP. We don't show ports for systems because
        // the data model doesn't track listening ports at the system level.
        if (resource is SystemResource system && !string.IsNullOrWhiteSpace(system.Ip))
            return system.Ip;

        return null;
    }

    private readonly record struct Entry(
        Resource Resource,
        string Subnet,
        string? HardwareName,
        string? Subtitle);
}
