using RackPeek.Domain.Persistence;
using RackPeek.Domain.Resources;
using RackPeek.Domain.Resources.Hardware;
using RackPeek.Domain.Resources.Services;
using RackPeek.Domain.Resources.Services.Networking;
using RackPeek.Domain.Resources.SystemResources;

namespace RackPeek.Domain.Graph.UseCases;

/// <summary>
///     Logical / service-oriented view. Each system (hypervisor, VM, LXC,
///     container) becomes a single "host card" whose body lists every
///     service running on it. Cards are grouped subnet → hardware. No edges
///     are emitted — containment alone conveys "runs on", and the
///     serialiser stacks siblings vertically via invisible links.
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

        // Group services by the system they ultimately run on. We resolve
        // the immediate runsOn first — that's the host the service was
        // declared against. Services whose immediate runsOn isn't a known
        // system (e.g. it points at hardware or is missing) are dropped from
        // the compact view since they have no host card to live inside.
        var servicesByHost = new Dictionary<string, List<Service>>(StringComparer.OrdinalIgnoreCase);
        foreach (Service service in services) {
            var parent = service.RunsOn.FirstOrDefault();
            if (parent is null) continue;
            if (!byName.TryGetValue(parent, out Resource? parentResource)) continue;
            if (parentResource is not SystemResource) continue;
            if (!servicesByHost.TryGetValue(parent, out List<Service>? list))
                servicesByHost[parent] = list = new List<Service>();
            list.Add(service);
        }

        // Each system becomes a host card. Hosts without services still
        // appear as a labelled card (e.g. a hypervisor that only contains
        // VMs has no services running directly on it, but is still a
        // meaningful logical entity).
        var hostEntries = new List<HostEntry>();
        foreach (SystemResource sys in systems) {
            var ip = FindIp(sys, byName);
            var subnet = SubnetCidr(ip, _defaultPrefix);
            if (subnet is null) continue;
            Hardware? parentHw = FindParentHardware(sys, byName);
            servicesByHost.TryGetValue(sys.Name, out List<Service>? hostServices);
            var rows = (hostServices ?? new List<Service>())
                .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .Select(s => new GraphNodeRow(s.Name, ServiceDetail(s)))
                .ToList();
            hostEntries.Add(new HostEntry(sys, subnet, parentHw?.Name, ip, rows));
        }

        var nodes = hostEntries
            .OrderBy(e => e.Subnet, StringComparer.Ordinal)
            .ThenBy(e => e.HardwareName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(e => e.Rows.Count) // big cards first within a hardware bucket
            .ThenBy(e => e.System.Name, StringComparer.OrdinalIgnoreCase)
            .Select(e => new GraphNode(
                e.System.Name,
                e.System.Name,
                NodeKind(e.System),
                e.Ip,
                Rows: e.Rows.Count > 0 ? e.Rows : null))
            .ToList();

        List<GraphGroup> groups = BuildGroups(hostEntries);

        return new Graph(nodes, [], groups, GraphRenderHint.Compact);
    }

    private static List<GraphGroup> BuildGroups(IReadOnlyList<HostEntry> entries) {
        var groups = new List<GraphGroup>();

        IOrderedEnumerable<IGrouping<string, HostEntry>> bySubnet = entries
            .GroupBy(e => e.Subnet, StringComparer.Ordinal)
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (IGrouping<string, HostEntry> subnetGroup in bySubnet) {
            var subnetId = "g_" + Slug(subnetGroup.Key);

            var directNodes = new List<string>();
            IOrderedEnumerable<IGrouping<string?, HostEntry>> byHardware = subnetGroup
                .GroupBy(e => e.HardwareName)
                .OrderBy(g => g.Key ?? string.Empty, StringComparer.OrdinalIgnoreCase);

            foreach (IGrouping<string?, HostEntry> hwGroup in byHardware) {
                if (hwGroup.Key is null) {
                    directNodes.AddRange(hwGroup.Select(e => e.System.Name));
                    continue;
                }

                var hwGroupId = subnetId + "__" + Slug(hwGroup.Key);
                groups.Add(new GraphGroup(
                    hwGroupId,
                    hwGroup.Key,
                    hwGroup.Select(e => e.System.Name).ToList(),
                    subnetId));
            }

            groups.Add(new GraphGroup(subnetId, subnetGroup.Key, directNodes, null));
        }

        return groups;
    }

    private static string NodeKind(SystemResource sys) {
        if (string.IsNullOrWhiteSpace(sys.Type)) return "System";
        var t = sys.Type.Trim().ToLowerInvariant();
        return t switch {
            "hypervisor" => "Hypervisor",
            "vm" => "Vm",
            "container" => "Container",
            _ => "System"
        };
    }

    private static string? ServiceDetail(Service service) {
        var port = service.Network?.Port;
        return port.HasValue ? ":" + port.Value : null;
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

    private readonly record struct HostEntry(
        SystemResource System,
        string Subnet,
        string? HardwareName,
        string? Ip,
        IReadOnlyList<GraphNodeRow> Rows);
}
