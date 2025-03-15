using Godot;
using System;

public partial class MindlessSkeleton : Being
{
    private enum SkeletonState
    {
        Idle,
        Wandering,
        Following
    }

    [Export]
    public float WanderProbability = 0.2f; // Chance to wander when idle

    [Export]
    public float WanderRange = 10f; // Maximum wander distance from spawn

    [Export]
    public float IdleTime = 2.0f; // Time to stay idle before potentially wandering

    private Vector2I _spawnGridPos; // Original spawn position
    private SkeletonState _currentState = SkeletonState.Idle;
    private float _stateTimer = 0f;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // Called when the node enters the scene tree for the first time
    public override void _Ready()
    {
        base._Ready();
        _rng.Randomize();

        // Slower movement than player
        MoveSpeed = 0.4f;
    }

    // Initialize with specific data for skeletons
    public override void Initialize(GridSystem gridSystem, Vector2I startGridPos)
    {
        base.Initialize(gridSystem, startGridPos);
        _spawnGridPos = startGridPos;
        _currentState = SkeletonState.Idle;
        _stateTimer = IdleTime;

        // Start with idle animation
        _animatedSprite.Play("idle");
    }

    // Main update loop
    public override void _PhysicsProcess(double delta)
    {
        if (_gridSystem == null)
            return;

        // Update state machine
        UpdateStateMachine((float)delta);

        // Update movement (from Being base class)
        UpdateMovement(delta);
    }

    // State machine logic
    private void UpdateStateMachine(float delta)
    {
        // If we're currently moving to a target, don't change state
        if (_moveProgress < 1.0f)
            return;

        // Update state timer
        _stateTimer -= delta;

        switch (_currentState)
        {
            case SkeletonState.Idle:
                // Handle idle state
                if (_stateTimer <= 0)
                {
                    // Chance to wander
                    if (_rng.Randf() < WanderProbability)
                    {
                        _currentState = SkeletonState.Wandering;
                        _stateTimer = _rng.RandfRange(2.0f, 5.0f);
                        TryToWander();
                    }
                    else
                    {
                        // Reset idle timer
                        _stateTimer = IdleTime;
                    }
                }
                break;

            case SkeletonState.Wandering:
                // Handle wandering state
                if (_stateTimer <= 0)
                {
                    // Either continue wandering or return to idle
                    if (_rng.Randf() < 0.3f)
                    {
                        _currentState = SkeletonState.Idle;
                        _stateTimer = IdleTime;
                        _direction = Vector2.Zero;
                    }
                    else
                    {
                        _stateTimer = _rng.RandfRange(1.0f, 3.0f);
                        TryToWander();
                    }
                }
                break;

                // Could add more states here like Following for future expansion
        }
    }

    // Try to move in a random direction within wander range
    private void TryToWander()
    {
        // Pick a random direction (up, down, left, right)
        int randomDir = _rng.RandiRange(0, 3);

        Vector2 newDirection = Vector2.Zero;

        switch (randomDir)
        {
            case 0:
                newDirection = Vector2.Right;
                break;
            case 1:
                newDirection = Vector2.Left;
                break;
            case 2:
                newDirection = Vector2.Down;
                break;
            case 3:
                newDirection = Vector2.Up;
                break;
        }

        SetDirection(newDirection);

        // Calculate target grid position
        Vector2I targetGridPos = _currentGridPos + new Vector2I(
            (int)_direction.X,
            (int)_direction.Y
        );

        // Check if the target position is within wander range
        Vector2I distanceFromSpawn = targetGridPos - _spawnGridPos;

        if (Mathf.Abs(distanceFromSpawn.X) > WanderRange ||
            Mathf.Abs(distanceFromSpawn.Y) > WanderRange)
        {
            // Too far from spawn, try to move back toward spawn
            Vector2 towardSpawn = (_gridSystem.GridToWorld(_spawnGridPos) - Position).Normalized();

            // Find the cardinal direction closest to the direction to spawn
            if (Mathf.Abs(towardSpawn.X) > Mathf.Abs(towardSpawn.Y))
            {
                // Move horizontally
                newDirection = new Vector2(Mathf.Sign(towardSpawn.X), 0);
            }
            else
            {
                // Move vertically
                newDirection = new Vector2(0, Mathf.Sign(towardSpawn.Y));
            }

            SetDirection(newDirection);

            // Recalculate target position
            targetGridPos = _currentGridPos + new Vector2I(
                (int)_direction.X,
                (int)_direction.Y
            );
        }

        // Try to move to the target position
        if (!TryMoveToGridPosition(targetGridPos))
        {
            // Movement failed, return to idle
            _currentState = SkeletonState.Idle;
            _stateTimer = IdleTime;
            _direction = Vector2.Zero;
        }
    }
}
