using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Entities.Memory;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration;

/// <summary>
/// Generates cellar areas beneath buildings and links them via transition points.
/// Places the cellar building from a JSON template and sets up the necromancy altar
/// interaction handler programmatically.
/// </summary>
public static class CellarGenerator
{
    /// <summary>
    /// Create a cellar area beneath a building, linked by transition points.
    /// Finds the trapdoor decoration automatically from the building's decorations.
    /// Places the cellar building from template and configures the altar interaction.
    /// Registers cellar knowledge (transition points, facility) with the player's
    /// personal SharedKnowledge so the cellar remains secret from the village.
    /// </summary>
    /// <param name="world">The world to register the cellar with.</param>
    /// <param name="building">The building that contains the trapdoor.</param>
    public static void CreateCellar(World world, Building building)
    {
        var overworldArea = building.GridArea;
        if (overworldArea == null)
        {
            Log.Error("CellarGenerator: Building has no GridArea");
            return;
        }

        // Find the trapdoor decoration in the building
        var trapdoorDecoration = building.Decorations
            .FirstOrDefault(d => d.DecorationId == "trapdoor");
        if (trapdoorDecoration == null)
        {
            Log.Error($"CellarGenerator: Building '{building.BuildingName}' has no trapdoor decoration");
            return;
        }

        // The trapdoor decoration may be on a non-walkable tile (e.g., a wall).
        // Find the nearest walkable interior position for the transition point.
        var buildingPos = building.GetCurrentGridPosition();
        var trapdoorRelativePos = trapdoorDecoration.GridPosition;
        var trapdoorAbsPos = buildingPos + trapdoorRelativePos;

        // Check if the trapdoor tile itself is walkable
        if (!overworldArea.IsCellWalkable(trapdoorAbsPos))
        {
            // Find nearest walkable interior position
            var walkablePositions = building.GetWalkableInteriorPositions();
            Vector2I? nearest = null;
            int bestDist = int.MaxValue;
            foreach (var pos in walkablePositions)
            {
                var absPos = buildingPos + pos;
                var dist = Mathf.Abs(absPos.X - trapdoorAbsPos.X) + Mathf.Abs(absPos.Y - trapdoorAbsPos.Y);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = absPos;
                }
            }

            if (nearest == null)
            {
                Log.Error($"CellarGenerator: No walkable tile near trapdoor in '{building.BuildingName}'");
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

        // Add to scene tree under GridAreas so _Ready() fires
        var gridAreasContainer = world.GetNode<Node>("GridAreas");
        gridAreasContainer.AddChild(cellar);

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

        // Place the cellar building from JSON template
        var cellarBuilding = BuildingManager.Instance?.PlaceBuilding("Scholar's Cellar", Vector2I.Zero, cellar);
        if (cellarBuilding == null)
        {
            Log.Error("CellarGenerator: Failed to place cellar building from template");
            return;
        }

        // Register cellar knowledge with the player's PERSONAL SharedKnowledge.
        // The cellar is SECRET - village SharedKnowledge must NOT know about it.
        RegisterCellarWithPlayer(world, cellar, trapdoor, ladder, cellarBuilding);

        Log.Print($"CellarGenerator: Created cellar ({cellarSize.X}x{cellarSize.Y}) beneath {building.BuildingName}, transition at {trapdoorAbsPos}");
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
    /// <param name="cellarBuilding">The cellar building containing the altar.</param>
    private static void RegisterCellarWithPlayer(
        World world,
        Area cellar,
        TransitionPoint trapdoor,
        TransitionPoint ladder,
        Building cellarBuilding)
    {
        var player = world.GetNode<Player>("Entities/Player");
        if (player == null)
        {
            Log.Error("CellarGenerator: Player not found, cannot register cellar knowledge");
            return;
        }

        // Get the cellar room (detected via flood fill during Building.Initialize)
        var cellarRoom = cellarBuilding.GetDefaultRoom();
        if (cellarRoom == null)
        {
            Log.Error("CellarGenerator: Cellar building has no rooms");
            return;
        }

        // Room was already marked IsSecret by template hint matching in DetectRooms().
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

        // Register the cellar building so the player knows about it
        roomKnowledge.RegisterBuilding(cellarBuilding, cellar);

        // Register the necromancy_altar facility so FindNearestFacilityOfType works
        // Use the actual altar tile position, not the building origin (which is a wall tile)
        var altarFacility = cellarBuilding.GetFacility("necromancy_altar");
        var altarPositions = cellarBuilding.GetFacilityPositions("necromancy_altar");
        var altarPos = altarPositions.Count > 0
            ? cellarBuilding.GetCurrentGridPosition() + altarPositions[0]
            : cellarBuilding.GetCurrentGridPosition();
        if (altarFacility != null)
        {
            roomKnowledge.RegisterFacility("necromancy_altar", altarFacility, cellar, altarPos);
        }
        else
        {
            Log.Warn("CellarGenerator: necromancy_altar facility not found in cellar building; facility registration skipped");
        }

        // Give this knowledge to the player (permanent - will persist for the game)
        player.AddSharedKnowledge(roomKnowledge);

        Log.Print("CellarGenerator: Registered cellar knowledge with player via Room.RoomKnowledge");
    }
}
