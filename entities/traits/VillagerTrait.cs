using Godot;
using VeilOfAges.Entities.Beings.Health;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Sensory;
using VeilOfAges.Core.Lib;
using System.Collections.Generic;
using System;
using System.Runtime.CompilerServices;
using VeilOfAges.UI;
using VeilOfAges.UI.Commands;

namespace VeilOfAges.Entities.Traits
{
    public class VillagerTrait : BeingTrait
    {
        // Villager behavior properties
        public float WanderProbability { get; set; } = 0.1f;
        public float VisitBuildingProbability { get; set; } = 0.05f;
        public uint IdleTime { get; set; } = 20;
        public uint FleeDelay { get; set; } = 10; // Ticks of delay before fleeing

        // State machine
        private enum VillagerState { IdleAtHome, IdleAtSquare, VisitingBuilding, Fleeing }
        private VillagerState _currentState = VillagerState.IdleAtHome;

        // Village knowledge
        private Vector2I _homePosition;
        private Vector2I _squarePosition;
        private List<Building> _knownBuildings = new();
        private Building? _currentDestinationBuilding;

        // Path and movement tracking
        private uint _currentFleeDelay = 0;
        private bool _hasSeenUndead = false;
        private Vector2I? _lastUndeadPosition = null;
        private uint _lastUndeadTick = 0;

        public override void Initialize(Being owner, BodyHealth health)
        {
            base.Initialize(owner, health);

            // Find home position (where they spawned)
            _homePosition = _spawnPosition; // this is just an alias

            // Find village square (assume it's near the center of the map)
            if (owner?.GridArea != null)
            {
                _squarePosition = new Vector2I(owner.GridArea.GridSize.X / 2, owner.GridArea.GridSize.Y / 2);
                GD.Print($"Grid of size {owner.GridArea.GridSize} position at {_squarePosition}");
            }

            // Discover buildings in the world
            DiscoverBuildings();

            _currentState = VillagerState.IdleAtHome;
            GD.Print($"{_owner?.Name}: Villager trait initialized fully");
            IsInitialized = true;
        }

        private void DiscoverBuildings()
        {
            // Find all buildings in the scene
            var world = _owner?.GetTree().GetFirstNodeInGroup("World") as World;
            if (world == null) return;

            var entitiesNode = world.GetNode<Node2D>("Entities");
            foreach (Node child in entitiesNode.GetChildren())
            {
                if (child is Building building)
                {
                    _knownBuildings.Add(building);
                }
            }

            GD.Print($"{_owner?.Name}: Discovered {_knownBuildings.Count} buildings");
        }

        public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null) return null;

            // Only process AI if movement is complete
            if (_owner.IsMoving())
                return null;

            // Decrement timers
            if (_stateTimer > 0)
            {
                _stateTimer--;
            }

            // Check for undead nearby
            _hasSeenUndead = CheckForUndead(currentPerception);

            // If we've seen undead, start the flee delay countdown
            if (_hasSeenUndead && _currentState != VillagerState.Fleeing)
            {
                if (_currentFleeDelay == 0)
                {
                    _currentFleeDelay = FleeDelay;
                }
                else
                {
                    _currentFleeDelay--;
                    if (_currentFleeDelay == 0)
                    {
                        // Time to flee!
                        _currentState = VillagerState.Fleeing;
                        MyPathfinder.SetPositionGoal(_owner, _homePosition);
                        _stateTimer = 100; // Long flee timer
                        GD.Print($"{_owner.Name}: Fleeing from undead to home!");
                    }
                }
            }

            // Process current state
            switch (_currentState)
            {
                case VillagerState.IdleAtHome:
                    return ProcessIdleAtHomeState();
                case VillagerState.IdleAtSquare:
                    return ProcessIdleAtSquareState();
                case VillagerState.VisitingBuilding:
                    return ProcessVisitingBuildingState();
                case VillagerState.Fleeing:
                    return ProcessFleeingState();
                default:
                    return new IdleAction(_owner, this);
            }
        }

        private bool CheckForUndead(Perception perception)
        {
            // Look through entities to find any with UndeadTrait
            var entities = perception.GetEntitiesOfType<Being>();

            foreach (var (entity, position) in entities)
            {
                if (entity != _owner && entity.selfAsEntity().HasTrait<UndeadTrait>())
                {
                    _lastUndeadPosition = position;
                    _lastUndeadTick = MyPathfinder.CurrentTick;
                    return true;
                }
            }

            return false;
        }

        private EntityAction? ProcessIdleAtHomeState()
        {
            if (_owner == null) return null;

            if (_stateTimer == 0)
            {
                // Chance to go to the village square
                if (_rng.Randf() < WanderProbability)
                {
                    _currentState = VillagerState.IdleAtSquare;
                    _stateTimer = (uint)_rng.RandiRange(100, 200);

                    // Set goal to go to village square - lazy path calculation
                    MyPathfinder.SetPositionGoal(_owner, _squarePosition);

                    GD.Print($"{_owner.Name}: Going to village square");
                    return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
                }
                // Chance to visit a building
                else if (_rng.Randf() < VisitBuildingProbability && _knownBuildings.Count > 0)
                {
                    _currentState = VillagerState.VisitingBuilding;
                    _currentDestinationBuilding = _knownBuildings[_rng.RandiRange(0, _knownBuildings.Count - 1)];

                    if (_currentDestinationBuilding != null)
                    {
                        // Set goal to go to building - lazy path calculation
                        Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();
                        MyPathfinder.SetPositionGoal(_owner, buildingPos);

                        _stateTimer = (uint)_rng.RandiRange(80, 150);
                        GD.Print($"{_owner.Name}: Going to visit {_currentDestinationBuilding.BuildingType}");

                        return new MoveAlongPathAction(_owner, this, MyPathfinder, priority: 1);
                    }
                }
                else
                {
                    // Just reset the timer
                    _stateTimer = IdleTime;
                }
            }

            // Check if we're already at home
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            if (currentPos != _homePosition)
            {
                // We're not at home, so let's go there using lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, _homePosition);
                return new MoveAlongPathAction(_owner, this, MyPathfinder);
            }

            return new IdleAction(_owner, this);
        }

        private EntityAction? ProcessIdleAtSquareState()
        {
            if (_owner == null) return null;

            if (_stateTimer == 0)
            {
                // Time to go back home
                _currentState = VillagerState.IdleAtHome;

                // Set goal to go home - lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, _homePosition);

                _stateTimer = (uint)_rng.RandiRange(150, 300);
                GD.Print($"{_owner.Name}: Going back home");

                return new MoveAlongPathAction(_owner, this, MyPathfinder);
            }

            // Check if we're already at the square
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            if (currentPos.DistanceTo(_squarePosition) > 3)
            {
                // We're not at the square, so let's go there using lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, _squarePosition);
                return new MoveAlongPathAction(_owner, this, MyPathfinder);
            }

            // If we're at the square, just idle or wander slightly
            if (_rng.Randf() < 0.2f)
            {
                // Small random movement within the square
                int dx = _rng.RandiRange(-1, 1);
                int dy = _rng.RandiRange(-1, 1);
                Vector2I targetPos = currentPos + new Vector2I(dx, dy);

                if (_owner.GetGridArea()?.IsCellWalkable(targetPos) == true)
                {
                    return new MoveAction(_owner, this, targetPos);
                }
            }

            return new IdleAction(_owner, this);
        }

        private EntityAction? ProcessVisitingBuildingState()
        {
            if (_owner == null) return null;

            if (_stateTimer == 0 || _currentDestinationBuilding == null)
            {
                // Time to go back home
                _currentState = VillagerState.IdleAtHome;

                // Set goal to go home - lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, _homePosition);

                _stateTimer = (uint)_rng.RandiRange(150, 300);
                GD.Print($"{_owner.Name}: Finished visiting, going home");

                return new MoveAlongPathAction(_owner, this, MyPathfinder);
            }

            if (_currentDestinationBuilding == null) return null;

            // Check if we're at the building
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            Vector2I buildingPos = _currentDestinationBuilding.GetCurrentGridPosition();

            // We consider "at the building" when within 2 tiles of its perimeter
            bool atBuilding = false;
            Vector2I buildingSize = Building.BuildingSizes.ContainsKey(_currentDestinationBuilding.BuildingType) ?
                                    Building.BuildingSizes[_currentDestinationBuilding.BuildingType] :
                                    new Vector2I(2, 2);

            for (int x = -1; x <= buildingSize.X && !atBuilding; x++)
            {
                for (int y = -1; y <= buildingSize.Y && !atBuilding; y++)
                {
                    if (currentPos == buildingPos + new Vector2I(x, y))
                    {
                        atBuilding = true;
                    }
                }
            }

            if (!atBuilding)
            {
                // We're not at the building, so let's go there using lazy path calculation
                MyPathfinder.SetPositionGoal(_owner, buildingPos);
                return new MoveAlongPathAction(_owner, this, MyPathfinder);
            }

            // If we're at the building, just idle
            return new IdleAction(_owner, this);
        }

        private EntityAction? ProcessFleeingState()
        {
            if (_owner == null) return null;

            // Check if we've reached safety (home)
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            if (currentPos == _homePosition)
            {
                // We've reached safety
                _currentState = VillagerState.IdleAtHome;
                _stateTimer = (uint)_rng.RandiRange(200, 400); // Long rest after fleeing
                _hasSeenUndead = false;
                _lastUndeadPosition = null;
                GD.Print($"{_owner.Name}: Reached safety!");
                return new IdleAction(_owner, this);
            }

            // Continue following flee path to home
            bool undeadStillClose = false;

            // Check if undead was seen recently (within last 20 ticks)
            if (_lastUndeadPosition.HasValue && MyPathfinder.CurrentTick - _lastUndeadTick < 20)
            {
                undeadStillClose = true;
            }

            // If undead still close, flee directly away from it
            if (undeadStillClose && _lastUndeadPosition.HasValue)
            {
                // Calculate direction away from undead
                Vector2I directionAway = currentPos - _lastUndeadPosition.Value;
                Vector2I normalizedDirection = new(
                    Math.Sign(directionAway.X),
                    Math.Sign(directionAway.Y)
                );

                // Try to move away while also moving toward home
                Vector2I homeDirection = new(
                    Math.Sign(_homePosition.X - currentPos.X),
                    Math.Sign(_homePosition.Y - currentPos.Y)
                );

                // Blend the directions, prioritizing away from undead
                Vector2I moveDirection = new(
                    normalizedDirection.X != 0 ? normalizedDirection.X : homeDirection.X,
                    normalizedDirection.Y != 0 ? normalizedDirection.Y : homeDirection.Y
                );

                // Try to move one step in this direction
                Vector2I targetPos = currentPos + moveDirection;
                if (_owner.GetGridArea()?.IsCellWalkable(targetPos) == true)
                {
                    return new MoveAction(_owner, this, targetPos);
                }
            }

            // Otherwise use pathfinding to get home - lazy path calculation
            if (!MyPathfinder.HasValidPath() || !MyPathfinder.IsGoalReached(_owner))
            {
                MyPathfinder.SetPositionGoal(_owner, _homePosition);
            }

            return new MoveAlongPathAction(_owner, this, MyPathfinder);
        }

        public override string InitialDialogue(Being speaker)
        {
            return $"Hello there {speaker.Name}";
        }
    }
}
