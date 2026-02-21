using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Items;
using VeilOfAges.Entities.Traits;
using VeilOfAges.Grid;

namespace VeilOfAges.Entities;

/// <summary>
/// Static stamper that creates structural entities, facilities, and decorations
/// from a BuildingTemplate directly into a GridArea. Replaces Building.Initialize().
/// All created nodes are added as children of the GridArea (flat hierarchy).
/// </summary>
public static class TemplateStamper
{
    /// <summary>
    /// Stamp a building template into a grid area, creating all entities.
    /// Does NOT run room detection — the caller should run RoomSystem.DetectRoomsInRegion() after.
    /// </summary>
    /// <param name="template">The building template to stamp.</param>
    /// <param name="gridPosition">The absolute grid position for the top-left corner.</param>
    /// <param name="area">The grid area to stamp into.</param>
    /// <returns>A StampResult containing all created entities.</returns>
    public static StampResult Stamp(BuildingTemplate template, Vector2I gridPosition, Grid.Area area)
    {
        var result = new StampResult(
            template.Name ?? "Unknown",
            template.BuildingType ?? "Unknown",
            template.Capacity,
            gridPosition,
            template.Size,
            area);

        // Compute entrance positions (absolute)
        foreach (var entrance in template.EntrancePositions)
        {
            result.EntrancePositions.Add(gridPosition + entrance);
        }

        // Track which (DecorationId, Position) pairs are owned by facilities
        // so we can skip duplicate decorations
        var facilityOwnedDecorations = new HashSet<(string Id, Vector2I Position)>();

        // --- Create facilities ---
        foreach (var facilityData in template.Facilities)
        {
            // Convert positions to absolute
            var absolutePositions = facilityData.Positions.Select(p => gridPosition + p).ToList();

            var facility = new Facility(facilityData.Id, absolutePositions, facilityData.RequireAdjacent)
            {
                IsWalkable = facilityData.IsWalkable,
                GridArea = area
            };

            // Set absolute grid position of primary tile
            if (absolutePositions.Count > 0)
            {
                facility.SetGridPosition(absolutePositions[0]);
            }

            // Create StorageTrait if configured
            if (facilityData.Storage != null)
            {
                var storageTrait = new StorageTrait(
                    facilityData.Storage.VolumeCapacity,
                    facilityData.Storage.WeightCapacity,
                    facilityData.Storage.DecayRateModifier,
                    fetchDuration: facilityData.Storage.FetchDuration);
                facility.SelfAsEntity().AddTrait(storageTrait, 0);

                // Handle initial items from regeneration config
                if (facilityData.Storage.RegenerationInitialQuantity > 0
                    && !string.IsNullOrEmpty(facilityData.Storage.RegenerationItem))
                {
                    var itemDef = ItemResourceManager.Instance.GetDefinition(facilityData.Storage.RegenerationItem);
                    if (itemDef != null)
                    {
                        var initialItem = new Item(itemDef, facilityData.Storage.RegenerationInitialQuantity);
                        storageTrait.AddItem(initialItem);
                    }
                    else
                    {
                        Log.Warn($"TemplateStamper: Regeneration item '{facilityData.Storage.RegenerationItem}' not found");
                    }
                }
            }

            // Wire up interaction handler
            if (!string.IsNullOrEmpty(facilityData.InteractableType))
            {
                facility.Interactable = CreateFacilityInteractable(facilityData.InteractableType, facility);
            }

            // Initialize visual sprite if facility has a DecorationId
            if (!string.IsNullOrEmpty(facilityData.DecorationId))
            {
                var decorationDef = TileResourceManager.Instance.GetDecorationDefinition(facilityData.DecorationId);
                if (decorationDef != null && facilityData.Positions.Count > 0)
                {
                    // InitializeVisual sets up the sprite texture and relative position
                    facility.InitializeVisual(decorationDef, facilityData.Positions[0], facilityData.PixelOffset);

                    // Override position to use absolute pixel position since parent is GridArea (at 0,0),
                    // not a Building node offset to its grid position
                    var absolutePrimary = gridPosition + facilityData.Positions[0];
                    facility.Position = new Vector2(
                        (absolutePrimary.X * Grid.Utils.TileSize) + facilityData.PixelOffset.X,
                        (absolutePrimary.Y * Grid.Utils.TileSize) + facilityData.PixelOffset.Y);

                    facility.ZIndex = 4; // Above structural entities (floor=2, wall=3)

                    // Track this so we skip the duplicate decoration
                    facilityOwnedDecorations.Add((facilityData.DecorationId, facilityData.Positions[0]));
                }
                else if (decorationDef == null)
                {
                    Log.Warn($"TemplateStamper: Decoration definition '{facilityData.DecorationId}' not found for facility '{facilityData.Id}'");
                }
            }

            result.Facilities.Add(facility);
        }

        // --- Create structural entities from tiles ---
        foreach (var tileData in template.Tiles)
        {
            var entity = StructuralEntityFactory.Create(tileData, gridPosition);
            if (entity == null)
            {
                continue;
            }

            result.StructuralEntities.Add(entity);

            // Track door positions for entrance/room-boundary detection
            if (entity.IsRoomDivider)
            {
                result.DoorPositions.Add(entity.GridPosition);
            }
        }

        // --- Register structural entities with grid ---
        // Walkable entities (floors, doors, gates) get SetGroundWalkability.
        // Non-walkable entities (walls, fences) get AddEntity which marks them solid.
        // All are added as children of GridArea.
        foreach (var entity in result.StructuralEntities)
        {
            if (entity.IsWalkable)
            {
                area.SetGroundWalkability(entity.GridPosition, true);
            }

            // Add as child of GridArea (flat hierarchy)
            area.AddChild(entity);

            // Register non-walkable entities (walls, fences) as blocking in grid
            if (!entity.IsWalkable)
            {
                area.AddEntity(entity.GridPosition, entity);
            }
        }

        // --- Register facilities as grid entities ---
        // Must happen AFTER structural entities so SetGroundWalkability doesn't overwrite
        // the solid state set by AddEntity for non-walkable facilities.
        foreach (var facility in result.Facilities)
        {
            area.AddChild(facility);
            foreach (var pos in facility.GetAbsolutePositions())
            {
                area.AddEntity(pos, facility);
            }
        }

        // --- Create decorations ---
        foreach (var decorationPlacement in template.Decorations)
        {
            // Skip decorations owned by facilities (facility renders its own sprite)
            if (facilityOwnedDecorations.Contains((decorationPlacement.Id, decorationPlacement.Position)))
            {
                continue;
            }

            var decorationDef = TileResourceManager.Instance.GetDecorationDefinition(decorationPlacement.Id);
            if (decorationDef == null)
            {
                Log.Warn($"TemplateStamper: Decoration definition '{decorationPlacement.Id}' not found");
                continue;
            }

            var decoration = new Decoration();
            decoration.Initialize(decorationDef, decorationPlacement.Position,
                decorationPlacement.PixelOffset, decorationPlacement.IsWalkable,
                decorationPlacement.AdditionalPositions);
            decoration.GridArea = area;

            // Override position to absolute pixel position (parent is GridArea at 0,0)
            var absDecPos = gridPosition + decorationPlacement.Position;
            decoration.Position = new Vector2(
                (absDecPos.X * Grid.Utils.TileSize) + decorationPlacement.PixelOffset.X,
                (absDecPos.Y * Grid.Utils.TileSize) + decorationPlacement.PixelOffset.Y);

            decoration.ZIndex = 4; // Above structural entities
            area.AddChild(decoration);
            result.Decorations.Add(decoration);

            // Register decoration as grid entity for each position it occupies
            foreach (var relativePos in decoration.AllPositions)
            {
                var absolutePos = gridPosition + relativePos;
                decoration.AbsoluteGridPosition = absolutePos;
                area.AddEntity(absolutePos, decoration);
            }
        }

        // --- Transition points ---
        // Templates may define transition points (e.g., cellar stairs) in metadata.
        // The convention is metadata keys like "TransitionTarget" and "TransitionPosition".
        // These are handled by the caller (generator), not here — the StampResult provides
        // all the data needed for the caller to wire up transitions.
        return result;
    }

    /// <summary>
    /// Create an interactable handler by type name using reflection.
    /// Mirrors Building.CreateFacilityInteractable().
    /// </summary>
    private static IFacilityInteractable? CreateFacilityInteractable(string typeName, Facility facility)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.Name == typeName && typeof(IFacilityInteractable).IsAssignableFrom(type))
                {
                    try
                    {
                        return Activator.CreateInstance(type, facility) as IFacilityInteractable;
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"TemplateStamper: Failed to create interactable '{typeName}': {ex.Message}");
                        return null;
                    }
                }
            }
        }

        Log.Error($"TemplateStamper: Interactable type '{typeName}' not found");
        return null;
    }
}
