using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.UI;

namespace VeilOfAges.UI.Commands
{
    public class FollowCommand : EntityCommand
    {
        private const int SEARCH_TIMEOUT = 100; // How many ticks before giving up on finding a lost commander
        private const int SEARCH_RADIUS_MAX = 8; // Maximum search radius when looking for lost commander

        private Vector2I? _lastKnownPosition = null;
        private uint _lastSeenTick = 0;
        private bool _isSearching = false;
        private int _searchRadius = 2;

        public FollowCommand(Being owner, Being commander, bool isComplex = false)
            : base(owner, commander, isComplex)
        {
            // Command initialization
        }

        public override EntityAction? SuggestAction(Vector2I currentGridPos, Perception currentPerception)
        {
            if (MyPathfinder == null) return null;

            // Try to find the commander in perception
            bool commanderVisible = false;
            Vector2I commanderPosition = Vector2I.Zero;

            foreach (var (entity, position) in currentPerception.GetEntitiesOfType<Being>())
            {
                if (entity == _commander)
                {
                    commanderVisible = true;
                    commanderPosition = position;
                    _lastKnownPosition = position;
                    _lastSeenTick = MyPathfinder.CurrentTick;
                    _isSearching = false;
                    break;
                }
            }

            // Commander is visible - follow directly
            if (commanderVisible)
            {
                // Check if already in desired proximity (to avoid micro-adjustments)
                if (currentGridPos.DistanceTo(commanderPosition) <= 1)
                {
                    // Already at the commander, just stay close
                    return new IdleAction(_owner, this, -1);
                }

                // Set the goal but don't calculate the path yet
                MyPathfinder.SetEntityProximityGoal(_owner, _commander, 1);
                return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
            }
            // Commander is not visible - handle loss of visibility
            else
            {
                // Check if it's been too long since we've seen the commander
                uint ticksSinceLastSeen = MyPathfinder.CurrentTick - _lastSeenTick;

                if (ticksSinceLastSeen > SEARCH_TIMEOUT)
                {
                    // Commander lost for too long, give up
                    GD.Print($"{_owner.Name}: Lost commander for too long, giving up follow command");
                    return null; // End command
                }

                // If we have a last known position, try to move there first
                if (_lastKnownPosition.HasValue)
                {
                    // If we're already at the last known position, start searching
                    if (currentGridPos == _lastKnownPosition.Value)
                    {
                        _isSearching = true;

                        // Expand search radius over time
                        _searchRadius = Math.Min(SEARCH_RADIUS_MAX,
                                               2 + (int)(ticksSinceLastSeen / 10));
                    }

                    if (!_isSearching)
                    {
                        // Move to last known position
                        MyPathfinder.SetPositionGoal(_owner, _lastKnownPosition.Value);
                        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
                    }
                    else
                    {
                        // Search behavior: try to find commander in expanding radius
                        // Pick a semi-random position in search radius to explore
                        var randomGen = new RandomNumberGenerator();
                        randomGen.Randomize();

                        // Random angle and distance within search radius
                        float angle = randomGen.RandfRange(0, Mathf.Pi * 2);
                        int distance = randomGen.RandiRange(1, _searchRadius);

                        Vector2I searchPoint = new(
                            _lastKnownPosition.Value.X + (int)(Mathf.Cos(angle) * distance),
                            _lastKnownPosition.Value.Y + (int)(Mathf.Sin(angle) * distance)
                        );

                        // Make sure point is valid
                        var gridArea = _owner.GetGridArea();
                        if (gridArea != null && gridArea.IsCellWalkable(searchPoint))
                        {
                            MyPathfinder.SetPositionGoal(_owner, searchPoint);
                            return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: -1);
                        }

                        // If no valid point found, just idle briefly
                        return new IdleAction(_owner, this, -1);
                    }
                }

                // No last known position (shouldn't happen normally)
                return new IdleAction(_owner, this, -1);
            }
        }
    }
}
