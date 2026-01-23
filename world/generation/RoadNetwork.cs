using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Entities;

namespace VeilOfAges.WorldGeneration;

/// <summary>
/// Manages the village road network and lot system.
/// Creates a cross pattern of roads from the village center with lots along each road.
/// </summary>
public class RoadNetwork
{
    /// <summary>Gets center of the village.</summary>
    public Vector2I VillageCenter { get; }

    /// <summary>Gets radius of the central village square (default 3 = 7x7 square).</summary>
    public int VillageSquareRadius { get; }

    /// <summary>Gets width of roads in tiles.</summary>
    public int RoadWidth { get; }

    /// <summary>Gets size of each lot (width and height).</summary>
    public int LotSize { get; }

    /// <summary>Gets number of lots per side of each road arm.</summary>
    public int LotsPerSide { get; }

    /// <summary>Gets spacing between consecutive lots in tiles.</summary>
    public int LotSpacing { get; }

    /// <summary>Gets all road segments in the network.</summary>
    public List<RoadSegment> Roads { get; } = new ();

    /// <summary>Gets all lots in the village (flattened from roads).</summary>
    public List<VillageLot> AllLots { get; } = new ();

    private readonly Random _rng = new ();

    public RoadNetwork(
        Vector2I villageCenter,
        int villageSquareRadius = 3,
        int roadWidth = 2,
        int lotSize = 10,
        int lotsPerSide = 3,
        int lotSpacing = 1)
    {
        VillageCenter = villageCenter;
        VillageSquareRadius = villageSquareRadius;
        RoadWidth = roadWidth;
        LotSize = lotSize;
        LotsPerSide = lotsPerSide;
        LotSpacing = lotSpacing;
    }

    /// <summary>
    /// Calculate the optimal lot size based on all loaded building templates.
    /// Lot size = max(all building widths, all building heights) + 2
    /// (+2 for 1 tile margin on each side when centered, plus setback from road).
    /// </summary>
    /// <param name="buildingManager">The building manager with loaded templates.</param>
    /// <param name="setback">The setback distance from the road (default 1).</param>
    /// <returns>The optimal square lot size that can fit any building.</returns>
    public static int CalculateOptimalLotSize(BuildingManager? buildingManager, int setback = 1)
    {
        if (buildingManager == null)
        {
            // Default fallback if no manager available
            return 10;
        }

        var templateNames = buildingManager.GetAllTemplateNames();
        if (templateNames.Count == 0)
        {
            // No templates loaded, use default
            return 10;
        }

        int maxDimension = 0;

        foreach (var name in templateNames)
        {
            var template = buildingManager.GetTemplate(name);
            if (template != null)
            {
                // Track the maximum of both width and height
                maxDimension = Math.Max(maxDimension, template.Size.X);
                maxDimension = Math.Max(maxDimension, template.Size.Y);
            }
        }

        // Lot size = max dimension + setback (for road side) + 1 (minimum margin on opposite side)
        // This ensures building fits with setback from road and at least 1 tile margin
        return maxDimension + setback + 1;
    }

    /// <summary>
    /// Generate the complete road network with lots.
    /// Creates 4 roads extending from center (N, S, E, W) with lots on both sides.
    /// Corner lots are placed manually via CreateCornerLots() to avoid overlap where roads meet.
    /// </summary>
    public void GenerateLayout()
    {
        // Calculate road length based on lots per side (including spacing between lots)
        // Total length = (LotsPerSide * LotSize) + ((LotsPerSide - 1) * LotSpacing)
        int roadLength = (LotsPerSide * LotSize) + ((LotsPerSide - 1) * LotSpacing);

        // Create 4 road segments extending from the village square

        // North road
        var northStart = new Vector2I(VillageCenter.X - (RoadWidth / 2), VillageCenter.Y - VillageSquareRadius - 1);
        var northEnd = new Vector2I(northStart.X, northStart.Y - roadLength);
        var northRoad = new RoadSegment(northStart, northEnd, RoadWidth);
        CreateLotsAlongRoad(northRoad, CardinalDirection.South); // Lots face south toward road
        Roads.Add(northRoad);

        // South road
        var southStart = new Vector2I(VillageCenter.X - (RoadWidth / 2), VillageCenter.Y + VillageSquareRadius + 1);
        var southEnd = new Vector2I(southStart.X, southStart.Y + roadLength);
        var southRoad = new RoadSegment(southStart, southEnd, RoadWidth);
        CreateLotsAlongRoad(southRoad, CardinalDirection.North); // Lots face north toward road
        Roads.Add(southRoad);

        // East road
        var eastStart = new Vector2I(VillageCenter.X + VillageSquareRadius + 1, VillageCenter.Y - (RoadWidth / 2));
        var eastEnd = new Vector2I(eastStart.X + roadLength, eastStart.Y);
        var eastRoad = new RoadSegment(eastStart, eastEnd, RoadWidth);
        CreateLotsAlongRoad(eastRoad, CardinalDirection.West); // Lots face west toward road
        Roads.Add(eastRoad);

        // West road
        var westStart = new Vector2I(VillageCenter.X - VillageSquareRadius - 1, VillageCenter.Y - (RoadWidth / 2));
        var westEnd = new Vector2I(westStart.X - roadLength, westStart.Y);
        var westRoad = new RoadSegment(westStart, westEnd, RoadWidth);
        CreateLotsAlongRoad(westRoad, CardinalDirection.East); // Lots face east toward road
        Roads.Add(westRoad);

        // Collect all lots from roads
        AllLots.Clear();
        foreach (var road in Roads)
        {
            AllLots.AddRange(road.AllLots);
        }

        // Create corner lots that don't belong to any specific road
        // (added after road lots so they don't get cleared)
        CreateCornerLots();
    }

    /// <summary>
    /// Create 4 corner lots around the village square in positions that don't overlap
    /// with roads or each other. These fill the gaps left by skipping corner lots
    /// in CreateLotsAlongRoad().
    /// </summary>
    private void CreateCornerLots()
    {
        // Corner lots are placed diagonally from the square corners, right next to it.
        // Just 1 tile gap from the square edge to avoid road overlap.
        int gap = 1;

        // Northeast corner: just past the square's NE corner
        var neLotPos = new Vector2I(
            VillageCenter.X + VillageSquareRadius + gap,
            VillageCenter.Y - VillageSquareRadius - LotSize);
        var neLot = new VillageLot(neLotPos, new Vector2I(LotSize, LotSize), null, CardinalDirection.South);
        AllLots.Add(neLot);

        // Northwest corner: just past the square's NW corner
        var nwLotPos = new Vector2I(
            VillageCenter.X - VillageSquareRadius - LotSize,
            VillageCenter.Y - VillageSquareRadius - LotSize);
        var nwLot = new VillageLot(nwLotPos, new Vector2I(LotSize, LotSize), null, CardinalDirection.South);
        AllLots.Add(nwLot);

        // Southeast corner: just past the square's SE corner
        var seLotPos = new Vector2I(
            VillageCenter.X + VillageSquareRadius + gap,
            VillageCenter.Y + VillageSquareRadius + gap);
        var seLot = new VillageLot(seLotPos, new Vector2I(LotSize, LotSize), null, CardinalDirection.North);
        AllLots.Add(seLot);

        // Southwest corner: just past the square's SW corner
        var swLotPos = new Vector2I(
            VillageCenter.X - VillageSquareRadius - LotSize,
            VillageCenter.Y + VillageSquareRadius + gap);
        var swLot = new VillageLot(swLotPos, new Vector2I(LotSize, LotSize), null, CardinalDirection.North);
        AllLots.Add(swLot);
    }

    private void CreateLotsAlongRoad(RoadSegment road, CardinalDirection roadFacing)
    {
        // Start from i=1 to skip the first lot on each side (closest to center)
        // This prevents corner lot overlap where perpendicular roads meet
        // For example: North road's east-side lots would overlap with East road's north-side lots
        for (int i = 1; i < LotsPerSide; i++)
        {
            Vector2I leftLotPos, rightLotPos;
            CardinalDirection leftSide, rightSide;

            // Calculate stride between lot positions (lot size + spacing between lots)
            int lotStride = LotSize + LotSpacing;

            if (road.Direction == RoadDirection.NorthSouth)
            {
                // Road runs north-south
                int y;
                if (road.Start.Y > road.End.Y) // Going north
                {
                    y = road.Start.Y - (i * lotStride) - LotSize;
                }
                else // Going south
                {
                    y = road.Start.Y + (i * lotStride);
                }

                // Left lots (west of road)
                leftLotPos = new Vector2I(road.Start.X - LotSize, y);
                leftSide = CardinalDirection.East; // Faces east toward road

                // Right lots (east of road)
                rightLotPos = new Vector2I(road.Start.X + RoadWidth, y);
                rightSide = CardinalDirection.West; // Faces west toward road
            }
            else
            {
                // Road runs east-west
                int x;
                if (road.Start.X > road.End.X) // Going west
                {
                    x = road.Start.X - (i * lotStride) - LotSize;
                }
                else // Going east
                {
                    x = road.Start.X + (i * lotStride);
                }

                // Left lots (north of road)
                leftLotPos = new Vector2I(x, road.Start.Y - LotSize);
                leftSide = CardinalDirection.South; // Faces south toward road

                // Right lots (south of road)
                rightLotPos = new Vector2I(x, road.Start.Y + RoadWidth);
                rightSide = CardinalDirection.North; // Faces north toward road
            }

            var leftLot = new VillageLot(leftLotPos, new Vector2I(LotSize, LotSize), road, leftSide);
            var rightLot = new VillageLot(rightLotPos, new Vector2I(LotSize, LotSize), road, rightSide);

            road.LeftLots.Add(leftLot);
            road.RightLots.Add(rightLot);
        }
    }

    /// <summary>
    /// Get an available lot, optionally randomized.
    /// </summary>
    public VillageLot? GetAvailableLot(bool randomize = true)
    {
        var available = AllLots.Where(l => l.State == LotState.Available).ToList();
        if (available.Count == 0)
        {
            return null;
        }

        if (randomize)
        {
            return available[_rng.Next(available.Count)];
        }

        return available[0];
    }

    /// <summary>
    /// Get all available lots.
    /// </summary>
    public List<VillageLot> GetAvailableLots()
    {
        return AllLots.Where(l => l.State == LotState.Available).ToList();
    }

    /// <summary>
    /// Mark a lot as occupied by a building.
    /// </summary>
    public static void MarkLotOccupied(VillageLot lot, Building building)
    {
        lot.State = LotState.Occupied;
        lot.OccupyingBuilding = building;
    }

    /// <summary>
    /// Get all tiles that are part of roads (for placing road tiles).
    /// </summary>
    public IEnumerable<Vector2I> GetAllRoadTiles()
    {
        return Roads.SelectMany(r => r.GetRoadTiles());
    }

    /// <summary>
    /// Get the village square tile positions.
    /// </summary>
    public IEnumerable<Vector2I> GetVillageSquareTiles()
    {
        for (int x = -VillageSquareRadius; x <= VillageSquareRadius; x++)
        {
            for (int y = -VillageSquareRadius; y <= VillageSquareRadius; y++)
            {
                yield return VillageCenter + new Vector2I(x, y);
            }
        }
    }
}
