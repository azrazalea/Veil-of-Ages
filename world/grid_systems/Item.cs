using System;
using Godot;

namespace VeilOfAges.Grid;

public class ItemSystem(Vector2I? gridSize): System<(int, object[])>(gridSize)
{
}
