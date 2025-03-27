using Godot;
using VeilOfAges.Entities.Actions;
using VeilOfAges.Entities.Needs;
using VeilOfAges.Entities.Needs.Strategies;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities.Traits
{
    public class ConsumptionBehaviorTrait : BeingTrait
    {
        private string _needId;
        private Need? _need;

        private IFoodSourceIdentifier _sourceIdentifier;
        private IFoodAcquisitionStrategy _acquisitionStrategy;
        private IConsumptionEffect _consumptionEffect;
        private ICriticalStateHandler _criticalStateHandler;

        private Building? _currentFoodSource;
        private uint _consumptionTimer = 0;
        private uint _consumptionDuration = 30; // How long it takes to consume
        private bool _isConsuming = false;

        public ConsumptionBehaviorTrait(
            string needId,
            IFoodSourceIdentifier sourceIdentifier,
            IFoodAcquisitionStrategy acquisitionStrategy,
            IConsumptionEffect consumptionEffect,
            ICriticalStateHandler criticalStateHandler,
            uint consumptionDuration = 30)
        {
            _needId = needId;
            _sourceIdentifier = sourceIdentifier;
            _acquisitionStrategy = acquisitionStrategy;
            _consumptionEffect = consumptionEffect;
            _criticalStateHandler = criticalStateHandler;
            _consumptionDuration = consumptionDuration;
        }

        public override void Initialize()
        {
            base.Initialize();

            if (_owner != null)
            {
                // Get reference to the need
                _need = _owner.NeedsSystem?.GetNeed(_needId);

                if (_need == null)
                {
                    GD.PrintErr($"ConsumptionBehaviorTrait: Could not find need with ID {_needId}");
                }
            }
        }

        public override EntityAction? SuggestAction(Vector2I currentOwnerGridPosition, Perception currentPerception)
        {
            if (_owner == null || _need == null) return null;

            // If already moving, don't interrupt
            if (_owner.IsMoving()) return null;

            // If consuming, continue
            if (_isConsuming)
            {
                return HandleConsumption();
            }

            // Check if need is low (hungry)
            if (_need.IsLow())
            {
                // Find food source if we don't have one
                if (_currentFoodSource == null)
                {
                    _currentFoodSource = _sourceIdentifier.IdentifyFoodSource(_owner, currentPerception);

                    // If critical and no food source, use critical handler
                    if (_currentFoodSource == null && _need.IsCritical())
                    {
                        return _criticalStateHandler.HandleCriticalState(_owner, _need);
                    }
                }

                // If we have a food source, go to it or consume from it
                if (_currentFoodSource != null)
                {
                    // Check if we're at the food source
                    if (_acquisitionStrategy.IsAtFoodSource(_owner, _currentFoodSource))
                    {
                        // Start consuming
                        _isConsuming = true;
                        _consumptionTimer = 0;
                        return HandleConsumption();
                    }
                    else
                    {
                        // Get action to go to food source
                        return _acquisitionStrategy.GetAcquisitionAction(_owner, _currentFoodSource);
                    }
                }
            }

            return null;
        }

        private EntityAction? HandleConsumption()
        {
            if (_owner == null || _need == null || _currentFoodSource == null) return null;

            // Increment timer
            _consumptionTimer++;

            // First tick message
            if (_consumptionTimer == 1)
            {
                GD.Print($"{_owner.Name}: Started consuming {_need.DisplayName} at {_currentFoodSource.BuildingType}");
            }

            // Check if done consuming
            if (_consumptionTimer >= _consumptionDuration)
            {
                // Apply consumption effect
                _consumptionEffect.Apply(_owner, _need, _currentFoodSource);

                // Reset state
                _isConsuming = false;
                _consumptionTimer = 0;
                _currentFoodSource = null;

                return new IdleAction(_owner, this, 0);
            }

            // If still consuming, just idle
            return new IdleAction(_owner, this, 0);
        }
    }
}
