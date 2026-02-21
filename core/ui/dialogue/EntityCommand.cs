using System.Collections.Generic;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Activities;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.UI;

public abstract class EntityCommand(Being owner, Being commander, bool isComplex = true): ISubActivityRunner
{
    /// <summary>
    /// Gets or sets any extra data the command needs to be able to function properly.
    /// </summary>
    public Dictionary<string, object> Parameters { get; protected set; } = [];

    /// <summary>
    /// The Being that is receiving the order to perform the command.
    /// </summary>
    protected readonly Being _owner = owner;

    /// <summary>
    /// The Being that is ordering the _owner.
    /// </summary>
    protected readonly Being _commander = commander;

    /// <summary>
    /// Gets or sets a value indicating whether is this command considered too complex for non sapiant beings like Mindless trait?.
    /// </summary>
    public bool IsComplex { get; protected set; } = isComplex;
    public PathFinder MyPathfinder = new ();

    public virtual string DisplayName => GetType().Name.Replace("Command", string.Empty);

    /// <summary>
    /// Add a parameter to the command.
    /// </summary>
    /// <param name="key">Name of the parameter.</param>
    /// <param name="value">Value of the parameter.</param>
    /// <returns>this.</returns>
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
    /// <param name="currentGridPos">The current grid position of _owner.</param>
    /// <param name="currentPerception">The current perception of _owner.</param>
    /// <returns></returns>
    public abstract EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception);

    /// <summary>
    /// Gets explicit ISubActivityRunner implementation — exposes _owner for the interface's default RunSubActivity.
    /// </summary>
    Being? ISubActivityRunner.SubActivityOwner => _owner;

    /// <summary>
    /// Runs a sub-activity from within a command. Delegates to the shared ISubActivityRunner
    /// default implementation. Commands default to priority -1 (higher than activities).
    /// </summary>
    protected (Activity.SubActivityResult result, EntityAction? action) RunSubActivity(
        Activity subActivity, Vector2I position, Perception perception, int priority = -1)
    {
        return ((ISubActivityRunner)this).RunSubActivity(subActivity, position, perception, priority);
    }

    /// <summary>
    /// Initializes a sub-activity with this command's owner. Call this after creating
    /// a sub-activity before passing it to RunSubActivity.
    /// </summary>
    protected Activity InitializeSubActivity(Activity subActivity)
    {
        subActivity.Initialize(_owner);
        return subActivity;
    }
}
