using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;

namespace VeilOfAges.Entities.Traits;

/// <summary>
/// Trait that produces items over time into a Facility's StorageTrait.
/// Replaces Building.ProcessRegeneration() for Rimworld-style architecture.
/// Attach to a Facility that has a StorageTrait (e.g., well produces water).
/// </summary>
public class RegenerationTrait : Trait
{
    /// <summary>
    /// Gets the item definition ID to regenerate (e.g., "water").
    /// </summary>
    public string ItemId { get; }

    /// <summary>
    /// Gets the rate at which items are added per tick (accumulates until >= 1.0).
    /// </summary>
    public float Rate { get; }

    /// <summary>
    /// Gets the maximum quantity of the item. Regeneration stops at this cap.
    /// </summary>
    public int MaxQuantity { get; }

    private float _progress;

    /// <summary>
    /// The facility this trait is attached to. Set after initialization.
    /// </summary>
    private Facility? _facility;

    public RegenerationTrait(string itemId, float rate, int maxQuantity)
    {
        ItemId = itemId;
        Rate = rate;
        MaxQuantity = maxQuantity;
    }

    /// <summary>
    /// Set the owning facility. Called by TemplateStamper after adding the trait.
    /// </summary>
    public void SetFacility(Facility facility)
    {
        _facility = facility;
    }

    /// <summary>
    /// Process regeneration for the given number of ticks.
    /// Called by World during decay processing.
    /// </summary>
    /// <param name="tickMultiplier">Number of ticks since last processing.</param>
    public void ProcessRegeneration(int tickMultiplier)
    {
        if (string.IsNullOrEmpty(ItemId) || Rate <= 0 || _facility == null)
        {
            return;
        }

        var storage = _facility.SelfAsEntity().GetTrait<StorageTrait>();
        if (storage == null)
        {
            return;
        }

        int currentQuantity = storage.GetItemCount(ItemId);
        if (currentQuantity >= MaxQuantity)
        {
            _progress = 0f;
            return;
        }

        _progress += Rate * tickMultiplier;

        if (_progress >= 1.0f)
        {
            int unitsToAdd = (int)_progress;
            _progress -= unitsToAdd;

            int spaceAvailable = MaxQuantity - currentQuantity;
            unitsToAdd = System.Math.Min(unitsToAdd, spaceAvailable);

            if (unitsToAdd > 0)
            {
                var itemDef = ItemResourceManager.Instance.GetDefinition(ItemId);
                if (itemDef != null)
                {
                    var newItem = new Item(itemDef, unitsToAdd);
                    storage.AddItem(newItem);
                }
            }
        }
    }
}
