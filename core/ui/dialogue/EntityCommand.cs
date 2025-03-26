using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.UI
{
    public abstract class EntityCommand(Being owner, Being commander, bool IsComplex = true)
    {
        /// <summary>
        /// Any extra data the command needs to be able to function properly
        /// </summary>
        public Dictionary<string, object> Parameters { get; protected set; } = [];
        /// <summary>
        /// The Being that is receiving the order to perform the command
        /// </summary>
        protected readonly Being _owner = owner;
        /// <summary>
        /// The Being that is ordering the _owner
        /// </summary>
        protected readonly Being _commander = commander;
        /// <summary>
        /// Is this command considered too complex for non sapiant beings like Mindless trait?
        /// </summary>
        public bool IsComplex { get; protected set; } = IsComplex;
        public PathFinder MyPathfinder = new();


        /// <summary>
        /// Add a parameter to the command
        /// </summary>
        /// <param name="key">Name of the parameter</param>
        /// <param name="value">Value of the parameter</param>
        /// <returns>this</returns>
        public EntityCommand WithParameter(string key, object value)
        {
            Parameters[key] = value;
            return this;
        }
        /// <summary>
        /// This is modeled after the ITrait SuggestAction and should perform similarly.
        /// The command should keep track of its own state as needed and suggest actions one at a time
        /// until the command is completed. Keep in mind default trait actions are priority 1 so you should
        /// typically use a priority of 0 for command actions so they take priority. Cruicial commands should be -10 priority
        /// so that traits/entities can suggest actions at -1 if they want it to override normal commands.
        /// </summary>
        /// <param name="currentGridPos">The current grid position of _owner</param>
        /// <param name="currentPerception">The current perception of _owner</param>
        /// <returns></returns>
        public abstract EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception);
    }
}
