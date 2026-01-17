using Godot;
using VeilOfAges.Core;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities.Beings;

public partial class HumanTownsfolk : Being
{
    public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;

    /// <summary>
    /// Gets or sets home building to assign to VillagerTrait during initialization.
    /// Set this before adding the node to the scene tree.
    /// </summary>
    public Building? PendingHome { get; set; }

    public override void _Ready()
    {
        if (Health == null)
        {
            return;
        }

        // Create VillagerTrait with pending home
        var villagerTrait = new VillagerTrait(PendingHome);
        SelfAsEntity().AddTrait(villagerTrait, 1);

        // Register as resident if home was set
        PendingHome?.AddResident(this);

        base._Ready();
    }

    public override void Initialize(Area gridArea, Vector2I startGridPos, GameController? gameController = null, BeingAttributes? attributes = null, bool debugEnabled = false)
    {
        BaseMovementPointsPerTick = 0.33f; // Average human speed (3.33 ticks per tile)
        base.Initialize(gridArea, startGridPos, gameController, attributes, debugEnabled);
    }
}
