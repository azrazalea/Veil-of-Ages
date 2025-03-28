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

        private EntityAction? _nextAction = null;

        public void SetNextAction(EntityAction action)
        {
            _nextAction = action;
        }

        public override void Initialize(Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
        {
            _baseMovementPointsPerTick = 0.5f; // Fast entity (2 ticks per tile)
            base.Initialize(gridArea, startGridPos, attributes);
        }

        public override EntityAction Think(Vector2 currentPosition, ObservationData currentObservation)
        {
            // Return the queued action, or default to base behavior
            if (_nextAction != null)
            {
                var action = _nextAction;
                _nextAction = null;
                return action;
            }

            return new IdleAction(this, this);
        }
    }
}
