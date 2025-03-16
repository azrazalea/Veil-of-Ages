using Godot;
using System;

namespace NecromancerKingdom.Entities
{
    // Base interface for all traits
    public interface ITrait
    {
        // Initialize the trait with its owner
        void Initialize(Being owner);

        // Process trait behavior (called every frame/tick)
        void Process(double delta);

        // Optional method for handling events
        void OnEvent(string eventName, params object[] args) { }
    }
}
