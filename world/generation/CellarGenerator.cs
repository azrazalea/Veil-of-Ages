using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration;

/// <summary>
/// Generates cellar areas beneath buildings and links them via transition points.
/// Stamps the cellar from a JSON template and sets up the necromancy altar
/// interaction handler programmatically.
/// </summary>
public static class CellarGenerator
{
    /// <summary>
    /// Create a cellar area beneath a building, linked by transition points.
    /// Finds the trapdoor decoration automatically from the StampResult's decorations.
    /// Stamps the cellar from template and configures the altar interaction.
    /// Registers cellar knowledge (transition points, facility) with the player's
    /// personal SharedKnowledge so the cellar remains secret from the village.
    /// </summary>
    /// <param name="world">The world to register the cellar with.</param>
    /// <param name="overworldStampResult">The StampResult of the overworld building that contains the trapdoor.</param>
    public static void CreateCellar(World world, StampResult overworldStampResult)
    {
        var overworldArea = overworldStampResult.GridArea;

        // Find the trapdoor decoration in the stamp result
        var trapdoorDecoration = overworldStampResult.Decorations
            .FirstOrDefault(d => d.DecorationId == "trapdoor");
        if (trapdoorDecoration == null)
        {
            Log.Error($"CellarGenerator: StampResult '{overworldStampResult.TemplateName}' has no trapdoor decoration");
            return;
        }

        // The trapdoor decoration stores its absolute grid position
        var trapdoorAbsPos = trapdoorDecoration.AbsoluteGridPosition;

        // Check if the trapdoor tile itself is walkable
        if (!overworldArea.IsCellWalkable(trapdoorAbsPos))
        {
            // Find nearest walkable tile from the building's structural entities (floors)
            Vector2I? nearest = null;
            int bestDist = int.MaxValue;
            foreach (var entity in overworldStampResult.StructuralEntities)
            {
                if (entity.IsWalkable && !entity.IsRoomDivider)
                {
                    var dist = Mathf.Abs(entity.GridPosition.X - trapdoorAbsPos.X) + Mathf.Abs(entity.GridPosition.Y - trapdoorAbsPos.Y);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        nearest = entity.GridPosition;
                    }
                }
            }

            if (nearest == null)
            {
                Log.Error($"CellarGenerator: No walkable tile near trapdoor in '{overworldStampResult.TemplateName}'");
                return;
            }

            Log.Print($"CellarGenerator: Trapdoor decoration at {trapdoorAbsPos} is not walkable, using nearest walkable tile {nearest.Value}");
            trapdoorAbsPos = nearest.Value;
        }

        // Create the cellar area (small: 7x8)
        var cellarSize = new Vector2I(7, 8);
        var cellar = new Area(cellarSize)
        {
            AreaName = "area.SCHOLARS_CELLAR"
        };

        // Initialize off-tree so data structures (TileMapLayers, AStarGrid) are created
        // without adding to the scene tree — cellar should not render until the player enters
        cellar.InitializeOffTree();

        // Fill cellar with dirt tiles (needed for CanPlaceBuildingAt validation)
        for (int x = 0; x < cellarSize.X; x++)
        {
            for (int y = 0; y < cellarSize.Y; y++)
            {
                cellar.SetGroundCell(new Vector2I(x, y), Area.DirtTile);
            }
        }

        // Ladder position in cellar (bottom-left, matches trapdoor above)
        var ladderPos = new Vector2I(1, 6);

        // Create transition points
        var trapdoor = new TransitionPoint(overworldArea, trapdoorAbsPos, "Trapdoor");
        var ladder = new TransitionPoint(cellar, ladderPos, "Ladder");
        TransitionPoint.Link(trapdoor, ladder);

        // Register transition points with their areas
        overworldArea.AddTransitionPoint(trapdoor);
        cellar.AddTransitionPoint(ladder);

        // Register with world
        world.RegisterTransitionPoint(trapdoor);
        world.RegisterTransitionPoint(ladder);
        world.RegisterGridArea(cellar);

        // Stamp the cellar building from JSON template using TemplateStamper
        var cellarStampResult = BuildingManager.Instance?.StampBuilding("Scholar's Cellar", Vector2I.Zero, cellar);
        if (cellarStampResult == null)
        {
            Log.Error("CellarGenerator: Failed to stamp cellar building from template");
            return;
        }

        // Register cellar knowledge with the player's PERSONAL SharedKnowledge.
        // The cellar is SECRET - village SharedKnowledge must NOT know about it.
        RegisterCellarWithPlayer(world, cellar, trapdoor, ladder, cellarStampResult);

        Log.Print($"CellarGenerator: Created cellar ({cellarSize.X}x{cellarSize.Y}) beneath {overworldStampResult.TemplateName}, transition at {trapdoorAbsPos}");
    }

    /// <summary>
    /// Registers cellar transition points and facility with the player's personal
    /// SharedKnowledge via the cellar room's secrecy system. This keeps the cellar
    /// SECRET from the village - only the player knows about the trapdoor, ladder,
    /// and altar.
    /// </summary>
    /// <param name="world">The world node for finding the player.</param>
    /// <param name="cellar">The cellar area.</param>
    /// <param name="trapdoor">The trapdoor transition point (overworld side).</param>
    /// <param name="ladder">The ladder transition point (cellar side).</param>
    /// <param name="cellarStampResult">The stamp result of the cellar containing rooms and facilities.</param>
    private static void RegisterCellarWithPlayer(
        World world,
        Area cellar,
        TransitionPoint trapdoor,
        TransitionPoint ladder,
        StampResult cellarStampResult)
    {
        var player = world.GetNode<Player>("Entities/Player");
        if (player == null)
        {
            Log.Error("CellarGenerator: Player not found, cannot register cellar knowledge");
            return;
        }

        // Get the cellar room via the necromancy_altar facility, with fallback to any facility's room
        var cellarRoom = cellarStampResult.Facilities
            .FirstOrDefault(f => f.Id == "necromancy_altar")
            ?.ContainingRoom
            ?? cellarStampResult.Facilities.Select(f => f.ContainingRoom).FirstOrDefault(r => r != null);
        if (cellarRoom == null)
        {
            Log.Error("CellarGenerator: Cellar stamp result has no rooms");
            return;
        }

        // Room was already marked IsSecret by template hint matching in RoomSystem.
        // Initialize its SharedKnowledge scope so we can register facilities and transitions.
        cellarRoom.InitializeSecrecy("player_cellar", "Secret Cellar");

        var roomKnowledge = cellarRoom.RoomKnowledge;
        if (roomKnowledge == null)
        {
            Log.Error("CellarGenerator: Failed to initialize cellar room secrecy");
            return;
        }

        // Register both transition points so WorldNavigator can plan routes
        roomKnowledge.RegisterTransitionPoint(trapdoor);
        roomKnowledge.RegisterTransitionPoint(ladder);

        // Register the cellar room so the player knows about it
        roomKnowledge.RegisterRoom(cellarRoom, cellar);

        // Register the necromancy_altar facility so FindNearestFacilityOfType works
        // Use the actual altar tile position, not the building origin (which is a wall tile)
        var altarFacility = cellarRoom.GetFacility("necromancy_altar");
        var altarAbsolutePositions = altarFacility?.GetAbsolutePositions();
        var altarPos = altarAbsolutePositions != null && altarAbsolutePositions.Count > 0
            ? altarAbsolutePositions[0]
            : cellarStampResult.Origin;
        if (altarFacility != null)
        {
            roomKnowledge.RegisterFacility("necromancy_altar", altarFacility, cellar, altarPos);
        }
        else
        {
            Log.Warn("CellarGenerator: necromancy_altar facility not found in cellar stamp result; facility registration skipped");
        }

        // Give this knowledge to the player (permanent - will persist for the game)
        player.AddSharedKnowledge(roomKnowledge);

        Log.Print("CellarGenerator: Registered cellar knowledge with player via Room.RoomKnowledge");
    }
}
