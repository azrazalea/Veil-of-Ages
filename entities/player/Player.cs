using System;
using System.Collections.Generic;
using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities
{
    public partial class Player : Being
    {
        public override BeingAttributes DefaultAttributes { get; } = BaseAttributesSet;

        public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
        {
            _baseMovementPointsPerTick = 0.5f; // Fast entity (2 ticks per tile)
            base.Initialize(gridArea, startGridPos, attributes);
        }
    }
}
