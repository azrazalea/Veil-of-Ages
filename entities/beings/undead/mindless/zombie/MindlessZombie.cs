using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.BeingServices;
using VeilOfAges.Entities.Traits;

namespace VeilOfAges.Entities.Beings;

public partial class MindlessZombie : Being
{
    public AudioStreamPlayer2D? _zombieGroan;

    public override BeingAttributes DefaultAttributes { get; } = new (
        12.0f,
        6.0f,
        14.0f,
        3.0f,
        8.0f,
        4.0f,
        2.0f);

    private MindlessZombie()
    {
        BaseMovementPointsPerTick = 0.15f;
    }

    public override void _Ready()
    {
        // Configure zombie specific properties
        if (Health == null)
        {
            return;
        }

        // Add zombie traits
        SelfAsEntity().AddTrait<MindlessTrait>(1);
        SelfAsEntity().AddTrait<ZombieTrait>(2);

        _zombieGroan = GetNode<AudioStreamPlayer2D>("AudioStreamPlayer2D");

        base._Ready();
        Log.Print("Zombie initialized with traits");
    }

    public override void Initialize(Grid.Area gridArea, Vector2I startGridPos, BeingAttributes? attributes = null)
    {
        base.Initialize(gridArea, startGridPos, attributes);
        ApplyRandomDecayDamage();

        // Any zombie-specific initialization after base initialization
        Log.Print($"Zombie spawned at {startGridPos}");
        Health?.PrintSystemStatuses();
    }

    // Custom zombie behavior overrides (if needed)
    public override void _PhysicsProcess(double delta)
    {
        // Let the traits and base class handle most behavior
        base._PhysicsProcess(delta);
    }

    public void PlayZombieGroan()
    {
        if (_zombieGroan == null)
        {
            return;
        }

        _zombieGroan.Position = Grid.Utils.GridToWorld(GetCurrentGridPosition());
        _zombieGroan.Play();
    }

    private void ApplyRandomDecayDamage()
    {
        if (BodyPartGroups == null)
        {
            return;
        }

        // Simulate zombie decay with random damage
        var rng = new RandomNumberGenerator();
        rng.Randomize();

        // Number of parts to damage
        int partsToAffect = rng.RandiRange(2, 5);
        int affected = 0;

        // List of eligible groups/parts
        var candidates = new List<(string groupName, string partName)>();

        // Collect all body parts
        foreach (var group in BodyPartGroups)
        {
            foreach (var part in group.Value.Parts)
            {
                // Skip vital parts like brain and heart for playability
                if (part.Name is not "Brain" and not "Heart" and not "Spine")
                {
                    candidates.Add((group.Key, part.Name));
                }
            }
        }

        // Shuffle candidates
        for (int i = 0; i < candidates.Count; i++)
        {
            int swapIndex = rng.RandiRange(0, candidates.Count - 1);
            (candidates[swapIndex], candidates[i]) = (candidates[i], candidates[swapIndex]);
        }

        // Apply damage to random parts
        foreach (var (groupName, partName) in candidates)
        {
            if (affected >= partsToAffect)
            {
                break;
            }

            // Get the part
            if (BodyPartGroups.TryGetValue(groupName, out var group))
            {
                var part = group.Parts.FirstOrDefault(p => p.Name == partName);
                if (part != null)
                {
                    // Apply random amount of decay damage
                    float damageAmount = part.MaxHealth * rng.RandfRange(0.3f, 0.7f);
                    DamageBodyPart(groupName, partName, damageAmount);

                    Log.Print($"Zombie {Name}: {partName} shows decay damage");
                    affected++;
                }
            }
        }
    }
}
