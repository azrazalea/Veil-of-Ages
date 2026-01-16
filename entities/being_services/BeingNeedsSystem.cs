using System.Collections.Generic;
using System.Globalization;
using System.Text;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Needs;

public class BeingNeedsSystem
{
    private readonly Dictionary<string, Need> _needs = new ();
    private readonly Being _owner;

    public BeingNeedsSystem(Being owner)
    {
        _owner = owner;
    }

    public void AddNeed(Need need)
    {
        _needs[need.Id] = need;
    }

    public Need? GetNeed(string id)
    {
        return _needs.TryGetValue(id, out var need) ? need : null;
    }

    public void UpdateNeeds()
    {
        var activity = _owner.GetCurrentActivity();

        foreach (var need in _needs.Values)
        {
            float multiplier = activity?.GetNeedDecayMultiplier(need.Id) ?? 1.0f;
            need.Decay(multiplier);
        }

        // Periodic debug logging (rate-limited by Log.EntityDebug)
        if (_owner.DebugEnabled && _needs.Count > 0)
        {
            var sb = new StringBuilder();
            foreach (var need in _needs.Values)
            {
                string state = need.IsCritical() ? "CRITICAL" : (need.IsLow() ? "LOW" : "OK");
                sb.Append(CultureInfo.InvariantCulture, $"{need.DisplayName}: {need.Value:F1}/100 ({state}), ");
            }

            if (sb.Length > 2)
            {
                sb.Length -= 2;
            }

            Log.EntityDebug(_owner.Name, "NEEDS", sb.ToString());
        }
    }

    public bool HasNeed(string id)
    {
        return _needs.ContainsKey(id);
    }

    public IEnumerable<Need> GetAllNeeds()
    {
        return _needs.Values;
    }
}
