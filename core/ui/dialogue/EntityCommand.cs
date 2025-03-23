using System.Collections.Generic;
using VeilOfAges.Entities;

namespace VeilOfAges.UI
{
    public abstract class EntityCommand(EntityCommand.CommandType type, Being owner, Being commander)
    {
        public enum CommandType
        {
            Follow,
            Guard,
            MoveTo,
            Gather,
            Build,
            Patrol,
            AttackTarget,
            DefendPosition,
            ReturnHome,
            Rest,
            Cancel
        }

        public CommandType Type { get; private set; } = type;
        public Dictionary<string, object> Parameters { get; private set; } = [];
        private readonly Being _owner = owner;
        private readonly Being _commander = commander;
        public bool IsComplex { get; private set; } = true;

        public EntityCommand WithParameter(string key, object value)
        {
            Parameters[key] = value;
            return this;
        }
    }
}
