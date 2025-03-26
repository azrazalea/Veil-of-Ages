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
    public class VillagerTrait : ITrait
    {
        protected Being? _owner;
        public bool IsInitialized { get; protected set; }
        public PathFinder? MyPathfinder { get; set; }


        private RandomNumberGenerator _rng = new();

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
        private uint _stateTimer = 0;
        private uint _currentFleeDelay = 0;
        private bool _hasSeenUndead = false;
        public int Priority { get; set; }

        public void Initialize(Being owner, BodyHealth health)
        {
            _owner = owner;
            _rng.Randomize();
            _stateTimer = IdleTime;
            MyPathfinder = _owner.GetPathfinder();

            // Find home position (where they spawned)
            _homePosition = _owner.GetCurrentGridPosition();

            // Find village square (assume it's near the center of the map)
            if (owner?.GridArea != null)
            {
                _squarePosition = new Vector2I(owner.GridArea.GridSize.X / 2, owner.GridArea.GridSize.Y / 2);
                GD.Print($"Grid of size {owner.GridArea.GridSize} position at {_squarePosition}");
            }


            // Discover buildings in the world
            DiscoverBuildings();

            _currentState = VillagerState.IdleAtHome;
            GD.Print($"{_owner.Name}: Villager trait initialized fully");
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

        public void Process(double delta)
        {
            // No per-frame processing needed, handled by SuggestAction
        }

        public EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
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
                        MyPathfinder?.SetPathTo(_owner, _homePosition);
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
                    MyPathfinder?.SetPathTo(_owner, _squarePosition);
                    _stateTimer = (uint)_rng.RandiRange(100, 200);
                    GD.Print($"{_owner.Name}: Going to village square");

                    if (!MyPathfinder?.IsPathComplete() ?? false)
                    {
                        return new MoveAlongPathAction(_owner, this);
                    }
                }
                // Chance to visit a building
                else if (_rng.Randf() < VisitBuildingProbability && _knownBuildings.Count > 0)
                {
                    _currentState = VillagerState.VisitingBuilding;
                    _currentDestinationBuilding = _knownBuildings[_rng.RandiRange(0, _knownBuildings.Count - 1)];

                    if (_currentDestinationBuilding != null)
                    {

                        MyPathfinder?.SetPathTo(_owner, _currentDestinationBuilding.GetCurrentGridPosition());
                        _stateTimer = (uint)_rng.RandiRange(80, 150);
                        GD.Print($"{_owner.Name}: Going to visit {_currentDestinationBuilding.BuildingType}");

                        if (!MyPathfinder?.IsPathComplete() ?? false)
                        {
                            return new MoveAlongPathAction(_owner, this);
                        }
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
                // We're not at home, so let's go there
                MyPathfinder?.SetPathTo(_owner, _homePosition);

                if (!MyPathfinder?.IsPathComplete() ?? false)
                {
                    return new MoveAlongPathAction(_owner, this);
                }
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
                MyPathfinder?.SetPathTo(_owner, _squarePosition);

                _stateTimer = (uint)_rng.RandiRange(150, 300);
                GD.Print($"{_owner.Name}: Going back home");

                if (!MyPathfinder?.IsPathComplete() ?? false)
                {
                    return new MoveAlongPathAction(_owner, this);
                }
            }

            // Check if we're already at the square
            Vector2I currentPos = _owner.GetCurrentGridPosition();
            if (currentPos.DistanceTo(_squarePosition) > 3)
            {
                // We're not at the square, so let's go there
                MyPathfinder?.SetPathTo(_owner, _squarePosition);

                if (!MyPathfinder?.IsPathComplete() ?? false)
                {
                    return new MoveAlongPathAction(_owner, this);
                }
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

                MyPathfinder?.SetPathTo(_owner, _homePosition);
                _stateTimer = (uint)_rng.RandiRange(150, 300);
                GD.Print($"{_owner.Name}: Finished visiting, going home");

                if (!MyPathfinder?.IsPathComplete() ?? false)
                {
                    return new MoveAlongPathAction(_owner, this);
                }
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
                // We're not at the building, so let's go there
                MyPathfinder?.SetPathTo(_owner, buildingPos);

                if (!MyPathfinder?.IsPathComplete() ?? false)
                {
                    return new MoveAlongPathAction(_owner, this);
                }
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
                GD.Print($"{_owner.Name}: Reached safety!");
                return new IdleAction(_owner, this);
            }

            // Continue following flee path
            if (!MyPathfinder?.IsPathComplete() ?? false)
            {
                return new MoveAlongPathAction(_owner, this);
            }

            // If we've run out of path, recalculate
            MyPathfinder?.SetPathTo(_owner, _homePosition);

            if (!MyPathfinder?.IsPathComplete() ?? false)
            {
                return new MoveAlongPathAction(_owner, this);
            }

            // If we still have no path, just try to move away from current position towards home
            Vector2 directionToHome = (Grid.Utils.GridToWorld(_homePosition) - Grid.Utils.GridToWorld(currentPos)).Normalized();
            Vector2I nextPos = new(
                currentPos.X + Mathf.RoundToInt(directionToHome.X),
                currentPos.Y + Mathf.RoundToInt(directionToHome.Y)
            );

            if (_owner.GetGridArea()?.IsCellWalkable(nextPos) == true)
            {
                return new MoveAction(_owner, this, nextPos);
            }

            // If all else fails, just idle (we're stuck)
            return new IdleAction(_owner, this);
        }

        public bool RefusesCommand(EntityCommand command)
        {
            return false;
        }

        public bool IsOptionAvailable(DialogueOption option)
        {
            return true;
        }

        public string InitialDialogue(Being speaker)
        {
            return $"Hello there {speaker.Name}";
        }

        public void OnEvent(string eventName, params object[] args)
        {
            // Handle events
        }

        public string? GetSuccessResponse(EntityCommand command)
        {
            return null;
        }
        public string? GetFailureResponse(EntityCommand command)
        {
            return null;
        }
        public string? GetSuccessResponse(string text)
        {
            return null;
        }
        public string? GetFailureResponse(string text)
        {
            return null;
        }

        public List<DialogueOption> GenerateDialogueOptions(Being speaker)
        {
            return [];
        }

        public string? GenerateDialogueDescription()
        {
            return null;
        }

        public int CompareTo(object? obj)
        {
            return (this as ITrait).GeneralCompareTo(obj);
        }
    }
}
