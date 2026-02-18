using Godot;
using VeilOfAges.Core.Lib;

namespace VeilOfAges.Entities.Terrain;

public partial class Tree : Node2D, IBlocksPathfinding
{
    private const string DEFINITIONID = "tree";

    public Vector2I GridSize = new (1, 1);

    private Vector2I _gridPosition;
    private VeilOfAges.Grid.Area? _gridArea;

    public void Initialize(VeilOfAges.Grid.Area gridArea, Vector2I gridPos)
    {
        _gridArea = gridArea;
        _gridPosition = gridPos;

        ZIndex = 1;
        Position = VeilOfAges.Grid.Utils.GridToWorld(_gridPosition);

        var definition = TileResourceManager.Instance.GetDecorationDefinition(DEFINITIONID);
        if (definition == null)
        {
            Log.Error($"Tree decoration definition '{DEFINITIONID}' not found");
            return;
        }

        GridSize = definition.TileSize;
        CreateSprite(definition);
    }

    private void CreateSprite(DecorationDefinition definition)
    {
        var atlasTexture = TileResourceManager.Instance.GetCachedAtlasTexture(
            definition.AtlasSource!, definition.AtlasCoords.Y, definition.AtlasCoords.X);
        if (atlasTexture == null)
        {
            Log.Error($"Tree: Failed to get atlas texture for '{definition.AtlasSource}'");
            return;
        }

        var sprite = new Sprite2D { Texture = atlasTexture };
        AddChild(sprite);
    }

    public override void _ExitTree()
    {
        _gridArea?.RemoveEntity(_gridPosition, GridSize);
    }

    public static void Interact()
    {
        Log.Print("Player is interacting with tree");
    }
}
