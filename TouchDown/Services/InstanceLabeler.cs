namespace TD.Services;

/// <summary>
/// Assigns a stable display label to each play. A play belonging to a fan-out agent
/// (MaxInstances &gt; 1), or to any member that owns more than one play, is numbered
/// ("The Offensive Line #1", "#2", …) in OrderIndex order. Everything else keeps the base
/// member name. The orchestrator and the monitor both use this so live status and replayed
/// plays agree on instance identity.
/// </summary>
public static class InstanceLabeler
{
    public readonly record struct PlayRef(int PlayId, int? MemberId, string MemberName, int OrderIndex, int MaxInstances);

    public static Dictionary<int, string> Label(IEnumerable<PlayRef> plays)
    {
        var list = plays.ToList();

        // -1 stands in for an unassigned member so the grouping key is non-nullable.
        var perMember = list
            .GroupBy(p => p.MemberId ?? -1)
            .ToDictionary(
                g => g.Key,
                g => new { Count = g.Count(), FanOut = g.Any(p => p.MaxInstances > 1) });

        var ordinals = new Dictionary<int, int>();
        var result = new Dictionary<int, string>();

        foreach (var p in list.OrderBy(p => p.OrderIndex).ThenBy(p => p.PlayId))
        {
            var key = p.MemberId ?? -1;
            var info = perMember[key];
            if (info.FanOut || info.Count > 1)
            {
                var n = ordinals.GetValueOrDefault(key) + 1;
                ordinals[key] = n;
                result[p.PlayId] = $"{p.MemberName} #{n}";
            }
            else
            {
                result[p.PlayId] = p.MemberName;
            }
        }

        return result;
    }
}
