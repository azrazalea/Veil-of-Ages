using System.Collections.Generic;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities;

namespace VeilOfAges.Grid;

/// <summary>
/// World-level Rimworld-style room detection system.
/// Detects enclosed rooms from structural entities using flood fill.
/// </summary>
public static class RoomSystem
{
    /// <summary>
    /// Detect rooms within a stamped region by flood-filling from facility positions,
    /// bounded by walls/fences/doors. Only facilities that are fully enclosed within
    /// structural boundaries form rooms; open-air facilities are skipped.
    /// </summary>
    /// <param name="stampResult">The stamp result containing structural entities and metadata.</param>
    /// <param name="hints">Optional room data hints from the template for naming/purpose matching.</param>
    /// <returns>List of detected rooms (also populated into stampResult.Rooms).</returns>
    public static List<Room> DetectRoomsInRegion(StampResult stampResult, List<RoomData>? hints = null)
    {
        var area = stampResult.GridArea;
        var origin = stampResult.Origin;
        var size = stampResult.Size;

        // Quick check: if no structural entity is a room boundary, nothing can be enclosed.
        bool hasBoundary = false;
        foreach (var entity in stampResult.StructuralEntities)
        {
            if (entity.IsRoomBoundary)
            {
                hasBoundary = true;
                break;
            }
        }

        if (!hasBoundary)
        {
            stampResult.Rooms.Clear();
            Log.Print($"RoomSystem: No room boundaries found in '{stampResult.TemplateName}', skipping room detection.");
            return stampResult.Rooms;
        }

        // Positions that block flood fill: room boundaries (walls, fences, windows, columns)
        // and room dividers (doors, gates).
        var boundaryPositions = new HashSet<Vector2I>();
        foreach (var entity in stampResult.StructuralEntities)
        {
            if (entity.IsRoomBoundary || entity.IsRoomDivider)
            {
                boundaryPositions.Add(entity.GridPosition);
            }
        }

        // Track which positions belong to which room, to handle overlapping flood fills.
        var positionToRoom = new Dictionary<Vector2I, Room>();

        // Global visited set shared across all flood fills so each tile is claimed once.
        var visited = new HashSet<Vector2I>();

        var rooms = new List<Room>();
        int roomIndex = 0;

        foreach (var facility in stampResult.Facilities)
        {
            var facilityPositions = facility.GetAbsolutePositions();

            // If any of this facility's positions already belong to a room,
            // attach the facility to that room instead of creating a new one.
            Room? existingRoom = null;
            foreach (var pos in facilityPositions)
            {
                if (positionToRoom.TryGetValue(pos, out var candidate))
                {
                    existingRoom = candidate;
                    break;
                }
            }

            if (existingRoom != null)
            {
                existingRoom.AddFacility(facility);
                continue;
            }

            // If the facility is entirely on boundary positions (a wall), skip it.
            bool anyNonBoundary = false;
            foreach (var pos in facilityPositions)
            {
                if (!boundaryPositions.Contains(pos))
                {
                    anyNonBoundary = true;
                    break;
                }
            }

            if (!anyNonBoundary)
            {
                continue;
            }

            // BFS flood fill seeded from ALL of the facility's positions.
            var roomTiles = new HashSet<Vector2I>();
            var queue = new Queue<Vector2I>();
            bool touchesOpenSpace = false;

            foreach (var seedPos in facilityPositions)
            {
                if (!visited.Contains(seedPos) && !boundaryPositions.Contains(seedPos))
                {
                    visited.Add(seedPos);
                    queue.Enqueue(seedPos);
                }
            }

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                roomTiles.Add(current);

                // Check 4 cardinal neighbors
                Vector2I[] neighbors =
                [
                    current + Vector2I.Up,
                    current + Vector2I.Down,
                    current + Vector2I.Left,
                    current + Vector2I.Right
                ];

                foreach (var neighbor in neighbors)
                {
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    // Walls and doors stop the flood fill.
                    if (boundaryPositions.Contains(neighbor))
                    {
                        continue;
                    }

                    // Check if neighbor is within stamp region bounds.
                    bool inBounds = neighbor.X >= origin.X && neighbor.X < origin.X + size.X
                                 && neighbor.Y >= origin.Y && neighbor.Y < origin.Y + size.Y;

                    if (!inBounds)
                    {
                        // Reached the edge of the stamp without hitting a wall — open space.
                        touchesOpenSpace = true;

                        // Do NOT enqueue; we just note the leak.
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            // If the flood fill escaped into open space, the facility is not enclosed — skip.
            if (touchesOpenSpace)
            {
                continue;
            }

            // Create the room for this enclosed region.
            var room = new Room($"room_{roomIndex}", roomTiles)
            {
                GridArea = area,
                IsEnclosed = true
            };

            room.AddFacility(facility);

            // Record every tile so later facilities can find this room.
            foreach (var tile in roomTiles)
            {
                positionToRoom[tile] = room;
            }

            rooms.Add(room);
            roomIndex++;
        }

        // Assign structural entities to rooms.
        foreach (var room in rooms)
        {
            var roomTiles = room.Tiles;

            foreach (var entity in stampResult.StructuralEntities)
            {
                if (entity.IsRoomBoundary)
                {
                    // Wall is part of the room if any cardinal neighbor is inside the room.
                    bool adjacentToRoom = false;
                    foreach (var dir in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                    {
                        if (roomTiles.Contains(entity.GridPosition + dir))
                        {
                            adjacentToRoom = true;
                            break;
                        }
                    }

                    if (adjacentToRoom)
                    {
                        room.Walls.Add(entity);
                    }
                }
                else if (entity.IsRoomDivider)
                {
                    // Doors belong to ALL rooms they're adjacent to (a door between two rooms
                    // belongs to both).
                    bool adjacentToRoom = false;
                    foreach (var dir in new[] { Vector2I.Up, Vector2I.Down, Vector2I.Left, Vector2I.Right })
                    {
                        if (roomTiles.Contains(entity.GridPosition + dir))
                        {
                            adjacentToRoom = true;
                            break;
                        }
                    }

                    if (adjacentToRoom)
                    {
                        room.Doors.Add(entity);
                    }
                }
                else if (roomTiles.Contains(entity.GridPosition))
                {
                    room.Floors.Add(entity);
                }
            }
        }

        // Assign decorations to rooms via the position lookup.
        foreach (var decoration in stampResult.Decorations)
        {
            if (positionToRoom.TryGetValue(decoration.AbsoluteGridPosition, out var decorRoom))
            {
                decorRoom.AddDecoration(decoration);
            }
        }

        // Match rooms to template hints by bounding box overlap.
        if (hints != null && hints.Count > 0)
        {
            MatchRoomHints(rooms, hints, origin);
        }
        else if (rooms.Count == 1)
        {
            // No hints, single room — use template name.
            rooms[0].Name = stampResult.TemplateName;
        }

        // Populate stamp result.
        stampResult.Rooms.Clear();
        stampResult.Rooms.AddRange(rooms);

        Log.Print($"RoomSystem: Detected {rooms.Count} room(s) in '{stampResult.TemplateName}'");
        return rooms;
    }

    /// <summary>
    /// Match detected rooms to template RoomData hints by bounding box overlap.
    /// Hint positions are relative to template origin, so we offset them.
    /// </summary>
    private static void MatchRoomHints(List<Room> rooms, List<RoomData> hints, Vector2I origin)
    {
        foreach (var hint in hints)
        {
            Room? bestMatch = null;
            int bestOverlap = 0;

            // Convert hint bounding box to absolute coordinates
            var hintTopLeft = origin + hint.TopLeft;
            var hintSize = hint.Size;

            foreach (var room in rooms)
            {
                int overlap = 0;
                foreach (var tile in room.Tiles)
                {
                    if (tile.X >= hintTopLeft.X && tile.X < hintTopLeft.X + hintSize.X &&
                        tile.Y >= hintTopLeft.Y && tile.Y < hintTopLeft.Y + hintSize.Y)
                    {
                        overlap++;
                    }
                }

                if (overlap > bestOverlap)
                {
                    bestOverlap = overlap;
                    bestMatch = room;
                }
            }

            if (bestMatch != null)
            {
                if (hint.Name != null)
                {
                    bestMatch.Name = hint.Name;
                }

                if (hint.Purpose != null)
                {
                    bestMatch.Purpose = hint.Purpose;
                }

                bestMatch.IsSecret = hint.IsSecret;
            }
        }
    }
}
