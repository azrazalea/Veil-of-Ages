using System.Collections.Generic;
using Godot;

namespace VeilOfAges.Entities.Needs
{
    public class BeingNeedsSystem
    {
        private Dictionary<string, Need> _needs = new();
        private Being _owner;

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
            foreach (var need in _needs.Values)
            {
                need.Decay();
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
}
