using System;
using Godot;

namespace VeilOfAges.Entities.Needs
{
    public class Need
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }

        // Value from 0-100, where 0=bad (starving) and 100=good (full)
        private float _value;
        public float Value
        {
            get => _value;
            private set => _value = Math.Clamp(value, 0f, 100f);
        }

        // Decay rate per tick
        public float DecayRate { get; private set; }

        // Thresholds
        public float CriticalThreshold { get; private set; }
        public float LowThreshold { get; private set; }
        public float SatisfiedThreshold { get; private set; }

        public Need(string id, string displayName, float initialValue = 100f, float decayRate = 0.1f,
            float criticalThreshold = 10f, float lowThreshold = 30f, float satisfiedThreshold = 90f)
        {
            Id = id;
            DisplayName = displayName;
            Value = initialValue;
            DecayRate = decayRate;
            CriticalThreshold = criticalThreshold;
            LowThreshold = lowThreshold;
            SatisfiedThreshold = satisfiedThreshold;
        }

        public void Decay()
        {
            Value -= DecayRate;
        }

        public void Restore(float amount)
        {
            Value += amount;
        }

        public bool IsCritical() => Value <= CriticalThreshold;
        public bool IsLow() => Value <= LowThreshold;
        public bool IsSatisfied() => Value >= SatisfiedThreshold;

        public string GetStatus()
        {
            if (IsCritical()) return "Critical";
            if (IsLow()) return "Low";
            if (IsSatisfied()) return "Satisfied";
            return "Normal";
        }
    }
}
