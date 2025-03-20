using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities;
using System.Threading;

namespace VeilOfAges.Core
{
    public partial class GameController : Node
    {
        [Export] public float SimulationTickRate { get; set; } = 10f; // Ticks per second
        [Export] public float TimeScale { get; set; } = 1.0f; // Can be adjusted for speed
        [Export] public uint MaxPlayerActions { get; set; } = 3;

        private float _tickInterval => 1f / SimulationTickRate;
        private float _timeSinceLastTick = 0f;
        private bool _simulationPaused = false;
        private bool _processingTick = false;

        private World _world;
        private Player _player;
        private EntityThinkingSystem _thinkingSystem;

        private Queue<EntityAction> _pendingPlayerActions = new();

        public override void _Ready()
        {
            _world = GetTree().GetFirstNodeInGroup("World") as World;
            _player = GetNode<Player>("/root/World/Entities/Player");
            _thinkingSystem = GetNode<EntityThinkingSystem>("/root/World/EntityThinkingSystem");

            if (_world == null || _player == null || _thinkingSystem == null)
            {
                GD.PrintErr("GameController: Failed to find required nodes!");
            }
        }

        public override void _Process(double delta)
        {
            if (_simulationPaused || _processingTick)
                return;

            _timeSinceLastTick += (float)delta * TimeScale;

            if (_timeSinceLastTick >= _tickInterval)
            {
                _timeSinceLastTick -= _tickInterval;
                ProcessNextTick();
            }
        }

        private async void ProcessNextTick()
        {
            _processingTick = true;
            // Feed any pending player actions to the player entity
            if (_pendingPlayerActions.Count > 0)
            {
                var nextAction = _pendingPlayerActions.Dequeue();
                _player.SetNextAction(nextAction);
            }
            else
            {
                // If no explicit player action, player gets an idle action
                _player.SetNextAction(new IdleAction(_player));
            }

            // Process the tick with all entities (including player)
            await _thinkingSystem.ProcessGameTick();
            // Update world state, UI, and advance time
            // _world.UpdateWorldState();
            // _world.UpdateUI();
            // _world.AdvanceTime();

            _processingTick = false;
        }

        // Methods to handle player input
        public void QueuePlayerAction(EntityAction action)
        {
            _pendingPlayerActions.Enqueue(action);
        }

        public bool CanQueuePlayerAction()
        {
            return _pendingPlayerActions.Count < MaxPlayerActions;
        }

        // Simulation control
        public void PauseSimulation()
        {
            _simulationPaused = true;
        }

        public void ResumeSimulation()
        {
            _simulationPaused = false;
        }

        public bool SimulationPaused()
        {
            return _simulationPaused;
        }

        public void SetTimeScale(float scale)
        {
            TimeScale = Mathf.Clamp(scale, 0.1f, 10f);
        }

        // Time skip functionality
        // public async Task SkipTime(TimeSpan duration)
        // {
        //     // Save current state
        //     float previousTimeScale = TimeScale;
        //     bool previousPauseState = _simulationPaused;

        //     try
        //     {
        //         // Calculate how many ticks to process
        //         int ticksToProcess = (int)(duration.TotalSeconds * SimulationTickRate);

        //         // Show time skip UI
        //         _world.ShowTimeSkipProgress(duration);

        //         // Speed up simulation
        //         _simulationPaused = false;
        //         TimeScale = 20f;

        //         // Process ticks in batches to keep UI responsive
        //         const int batchSize = 50;
        //         for (int i = 0; i < ticksToProcess; i += batchSize)
        //         {
        //             // Process a batch
        //             int currentBatchSize = Math.Min(batchSize, ticksToProcess - i);
        //             for (int j = 0; j < currentBatchSize; j++)
        //             {
        //                 await ForceProcessTick();
        //             }

        //             // Update progress indicator
        //             _world.UpdateTimeSkipProgress(i + currentBatchSize, ticksToProcess);

        //             // Check for critical events
        //             if (_world.HasCriticalEventPending())
        //             {
        //                 _world.ShowInterruptionEvent();
        //                 break;
        //             }

        //             // Yield to allow UI updates
        //             await Task.Delay(1);
        //         }
        //     }
        //     finally
        //     {
        //         // Restore original settings
        //         TimeScale = previousTimeScale;
        //         _simulationPaused = previousPauseState;

        //         // Hide progress UI
        //         _world.HideTimeSkipProgress();
        //     }
        // }

        private async Task ForceProcessTick()
        {
            _processingTick = true;

            // Player idles during skipped time
            _player.SetNextAction(new IdleAction(_player));

            // Process the tick
            await _thinkingSystem.ProcessGameTick();
            // _world.UpdateWorldState();
            // _world.UpdateUI();
            // _world.AdvanceTime();

            _processingTick = false;
        }
    }
}
