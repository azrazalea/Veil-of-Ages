using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;

namespace VeilOfAges.Core.UI;

/// <summary>
/// Toggleable panel showing what the player knows about facilities and their storage contents.
/// Press J to show/hide. Displays known facilities from SharedKnowledge and storage
/// observations from PersonalMemory with staleness indicators.
/// </summary>
public partial class KnowledgePanel : PanelContainer
{
    // Staleness thresholds in ticks
    private const uint FRESHTHRESHOLD = 500;
    private const uint RECENTTHRESHOLD = 4000;
    private const uint STALETHRESHOLD = 14000;
    private const uint OLDTHRESHOLD = 28000;

    // Staleness colors
    private static readonly Color ColorFresh = Colors.White;
    private static readonly Color ColorRecent = new ("#ccccdd");
    private static readonly Color ColorStale = new ("#9999aa");
    private static readonly Color ColorOld = new ("#666677");
    private static readonly Color ColorDim = new ("#888899");

    private VBoxContainer? _facilitiesContainer;
    private readonly List<FacilityRowEntry> _facilityRows = new ();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        AddChild(scroll);

        _facilitiesContainer = new VBoxContainer();
        _facilitiesContainer.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_facilitiesContainer);

        var header = new Label
        {
            ThemeTypeVariation = "HeaderLabel",
            Text = L.Tr("ui.knowledge.HEADER")
        };
        _facilitiesContainer.AddChild(header);
    }

    public override void _EnterTree()
    {
        GameEvents.UITickFired += OnUITick;
    }

    public override void _ExitTree()
    {
        GameEvents.UITickFired -= OnUITick;
    }

    private void OnUITick()
    {
        if (!Visible)
        {
            return;
        }

        if (!Services.TryGet<Player>(out var player) || player == null)
        {
            return;
        }

        uint currentTick = GameController.CurrentTick;

        // Collect all known facilities from SharedKnowledge sources
        var knownFacilities = new Dictionary<Facility, FacilityReference>();
        foreach (var knowledge in player.SharedKnowledge)
        {
            foreach (var facilityRef in knowledge.GetAllFacilityReferences())
            {
                if (facilityRef.Facility != null && !knownFacilities.ContainsKey(facilityRef.Facility))
                {
                    knownFacilities[facilityRef.Facility] = facilityRef;
                }
            }
        }

        // Collect storage observations from PersonalMemory
        var observations = new Dictionary<Facility, StorageObservation>();
        if (player.Memory != null)
        {
            foreach (var obs in player.Memory.GetAllStorageObservations())
            {
                observations[obs.Facility] = obs;
            }
        }

        // Build display list — only include facilities the player has observed
        var displayItems = new List<FacilityDisplayData>();
        foreach (var (facility, facilityRef) in knownFacilities)
        {
            if (!observations.TryGetValue(facility, out var observation))
            {
                continue;
            }

            uint ticksAgo = currentTick - observation.ObservedTick;

            displayItems.Add(new FacilityDisplayData(
                facility, facilityRef, observation, ticksAgo));
        }

        // Sort: freshest observations first
        displayItems.Sort((a, b) => a.TicksAgo.CompareTo(b.TicksAgo));

        EnsureFacilityRowCount(displayItems.Count);

        for (int i = 0; i < displayItems.Count; i++)
        {
            var data = displayItems[i];
            var entry = _facilityRows[i];

            UpdateFacilityRow(entry, data);

            if (entry.Container != null)
            {
                entry.Container.Visible = true;
            }
        }

        // Hide excess rows
        for (int i = displayItems.Count; i < _facilityRows.Count; i++)
        {
            if (_facilityRows[i].Container is { } container)
            {
                container.Visible = false;
            }
        }
    }

    private static void UpdateFacilityRow(FacilityRowEntry entry, FacilityDisplayData data)
    {
        // Update facility name
        string displayName = GetFacilityDisplayName(data);
        if (entry.NameLabel != null && entry.NameLabel.Text != displayName)
        {
            entry.NameLabel.Text = displayName;
        }

        // Update tags from SharedKnowledge/PersonalMemory (captured at observation time)
        string tagText = data.Reference.StorageTags.Count > 0
            ? L.TrFmt("ui.knowledge.TAGS", string.Join(", ", data.Reference.StorageTags.Select(t => L.Tr($"tag.storage.{t}"))))
            : L.Tr("ui.knowledge.NO_STORAGE");

        if (entry.TagsLabel != null && entry.TagsLabel.Text != tagText)
        {
            entry.TagsLabel.Text = tagText;
        }

        // Update staleness label
        if (entry.StalenessLabel != null)
        {
            string stalenessText = GetStalenessLabel(data.TicksAgo);
            var stalenessColor = GetStalenessColor(data.TicksAgo);

            if (entry.StalenessLabel.Text != stalenessText)
            {
                entry.StalenessLabel.Text = stalenessText;
            }

            entry.StalenessLabel.AddThemeColorOverride("font_color", stalenessColor);
        }

        // Update item rows
        if (data.Observation.Items.Count > 0)
        {
            bool isOld = data.TicksAgo >= OLDTHRESHOLD;
            var itemColor = GetStalenessColor(data.TicksAgo);

            EnsureItemRowCount(entry, data.Observation.Items.Count);

            for (int j = 0; j < data.Observation.Items.Count; j++)
            {
                var item = data.Observation.Items[j];
                var itemEntry = entry.ItemRows[j];

                string itemText = isOld
                    ? L.TrFmt("ui.knowledge.ITEM_QUANTITY_APPROX", item.Name, item.Quantity)
                    : L.TrFmt("ui.knowledge.ITEM_QUANTITY", item.Name, item.Quantity);

                if (itemEntry.Label != null)
                {
                    if (itemEntry.Label.Text != itemText)
                    {
                        itemEntry.Label.Text = itemText;
                    }

                    itemEntry.Label.AddThemeColorOverride("font_color", itemColor);
                    itemEntry.Label.Visible = true;
                }
            }

            // Hide excess item rows
            for (int j = data.Observation.Items.Count; j < entry.ItemRows.Count; j++)
            {
                var hiddenLabel = entry.ItemRows[j].Label;
                if (hiddenLabel != null)
                {
                    hiddenLabel.Visible = false;
                }
            }
        }
        else
        {
            // Hide all item rows when no items in observation
            for (int j = 0; j < entry.ItemRows.Count; j++)
            {
                var hiddenLabel = entry.ItemRows[j].Label;
                if (hiddenLabel != null)
                {
                    hiddenLabel.Visible = false;
                }
            }
        }
    }

    private static string GetFacilityDisplayName(FacilityDisplayData data)
    {
        var room = data.Facility.ContainingRoom;
        string facilityType = LocalizeFacilityType(data.Reference.FacilityType);

        if (room != null)
        {
            return L.TrFmt("ui.knowledge.FACILITY_NAME", room.Name, facilityType);
        }

        return facilityType;
    }

    private static string LocalizeFacilityType(string facilityType)
    {
        if (string.IsNullOrEmpty(facilityType))
        {
            return L.Tr("ui.knowledge.UNKNOWN_FACILITY");
        }

        return L.Tr($"facility.{facilityType}.NAME");
    }

    private static string GetStalenessLabel(uint ticksAgo)
    {
        if (ticksAgo < FRESHTHRESHOLD)
        {
            return L.Tr("ui.knowledge.STALENESS_FRESH");
        }

        if (ticksAgo < RECENTTHRESHOLD)
        {
            return L.Tr("ui.knowledge.STALENESS_RECENT");
        }

        if (ticksAgo < STALETHRESHOLD)
        {
            return L.Tr("ui.knowledge.STALENESS_STALE");
        }

        return L.Tr("ui.knowledge.STALENESS_OLD");
    }

    private static Color GetStalenessColor(uint ticksAgo)
    {
        if (ticksAgo < FRESHTHRESHOLD)
        {
            return ColorFresh;
        }

        if (ticksAgo < RECENTTHRESHOLD)
        {
            return ColorRecent;
        }

        if (ticksAgo < STALETHRESHOLD)
        {
            return ColorStale;
        }

        return ColorOld;
    }

    private void EnsureFacilityRowCount(int count)
    {
        while (_facilityRows.Count < count)
        {
            var rowContainer = new VBoxContainer();
            rowContainer.AddThemeConstantOverride("separation", 1);
            _facilitiesContainer?.AddChild(rowContainer);

            // Facility name
            var nameLabel = new Label();
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            nameLabel.AddThemeColorOverride("font_color", Colors.White);
            rowContainer.AddChild(nameLabel);

            // Tags
            var tagsLabel = new Label();
            tagsLabel.AddThemeFontSizeOverride("font_size", 10);
            tagsLabel.AddThemeColorOverride("font_color", ColorDim);
            rowContainer.AddChild(tagsLabel);

            // Staleness
            var stalenessLabel = new Label();
            stalenessLabel.AddThemeFontSizeOverride("font_size", 10);
            stalenessLabel.AddThemeColorOverride("font_color", ColorDim);
            rowContainer.AddChild(stalenessLabel);

            _facilityRows.Add(new FacilityRowEntry
            {
                Container = rowContainer,
                NameLabel = nameLabel,
                TagsLabel = tagsLabel,
                StalenessLabel = stalenessLabel,
            });
        }
    }

    private static void EnsureItemRowCount(FacilityRowEntry facilityEntry, int count)
    {
        while (facilityEntry.ItemRows.Count < count)
        {
            var label = new Label();
            label.AddThemeFontSizeOverride("font_size", 10);
            facilityEntry.Container?.AddChild(label);

            facilityEntry.ItemRows.Add(new ItemRowEntry
            {
                Label = label,
            });
        }
    }

    private sealed class FacilityRowEntry
    {
        public VBoxContainer? Container;
        public Label? NameLabel;
        public Label? TagsLabel;
        public Label? StalenessLabel;
        public List<ItemRowEntry> ItemRows = new ();
    }

    private sealed class ItemRowEntry
    {
        public Label? Label;
    }

    private sealed record FacilityDisplayData(
        Facility Facility,
        FacilityReference Reference,
        StorageObservation Observation,
        uint TicksAgo);
}
