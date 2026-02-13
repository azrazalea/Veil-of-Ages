using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;
using VeilOfAges.Grid;

namespace VeilOfAges.WorldGeneration;

/// <summary>
/// Generates cellar areas beneath buildings and links them via transition points.
/// </summary>
public static class CellarGenerator
{
    /// <summary>
    /// Create a cellar area beneath a building, linked by transition points.
    /// Finds the trapdoor decoration automatically from the building's decorations.
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
            AreaName = "Scholar's Cellar"
        };

        // Add to scene tree under GridAreas so _Ready() fires
        var gridAreasContainer = world.GetNode<Node>("GridAreas");
        gridAreasContainer.AddChild(cellar);

        // Fill cellar with dirt tiles
        for (int x = 0; x < cellarSize.X; x++)
        {
            for (int y = 0; y < cellarSize.Y; y++)
            {
                cellar.SetGroundCell(new Vector2I(x, y), Area.DirtTile);
            }
        }

        // Ladder position in cellar (center-top)
        var ladderPos = new Vector2I(3, 0);

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

        Log.Print($"CellarGenerator: Created cellar ({cellarSize.X}x{cellarSize.Y}) beneath {building.BuildingName}, transition at {trapdoorAbsPos}");
    }
}
