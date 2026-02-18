using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using VeilOfAges.Core.Lib;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities;

/// <summary>
/// Manages tile-related resources including materials, tile definitions, and atlas sources.
/// </summary>
public partial class TileResourceManager : Node
{
    // Singleton instance
    private static TileResourceManager? _instance;

    public static TileResourceManager Instance => _instance
        ?? throw new InvalidOperationException("TileResourceManager not initialized. Ensure it's registered as an autoload in project.godot");

    // Resource collections
    private Dictionary<string, TileMaterialDefinition> _materials = new ();
    private readonly Dictionary<string, TileDefinition> _tileDefinitions = new ();
    private Dictionary<string, TileAtlasSourceDefinition> _atlasSources = new ();
    private Dictionary<string, DecorationDefinition> _decorationDefinitions = new ();
    private Dictionary<string, Beings.SpriteAnimationDefinition> _decorationAnimations = new ();
    private Dictionary<string, TerrainTileDefinition> _terrainDefinitions = new ();

    // Atlas source ID to tileset source ID mapping
    private readonly Dictionary<string, int> _tilesetSourceIds = new ();

    // Loaded texture resources
    private readonly Dictionary<string, Texture2D> _loadedTextures = new ();

    // Shared TileSet instance — created once, reused by all TileMapLayers
    private TileSet? _sharedTileSet;

    // Global AtlasTexture cache — avoids creating duplicate native objects for the same sprite region
    private readonly Dictionary<(string atlasSourceId, int row, int col, int widthInTiles, int heightInTiles), AtlasTexture> _atlasTextureCache = new ();

    // Cached SpriteFrames for animated decorations — shared across all instances of the same animation
    private readonly Dictionary<string, SpriteFrames> _spriteFramesCache = new ();

    public override void _Ready()
    {
        MemoryProfiler.Checkpoint("TileResourceManager _Ready start");
        _instance = this;

        LoadAllMaterials();
        MemoryProfiler.Checkpoint("TileResourceManager after LoadAllMaterials");
        LoadAllAtlasSources();
        MemoryProfiler.Checkpoint("TileResourceManager after LoadAllAtlasSources");
        LoadAllTileDefinitions();
        MemoryProfiler.Checkpoint("TileResourceManager after LoadAllTileDefinitions");
        LoadAllDecorationDefinitions();
        LoadAllDecorationAnimations();
        LoadAllTerrainDefinitions();

        Log.Print($"TileResourceManager initialized with {_materials.Count} materials, {_atlasSources.Count} atlas sources, {_tileDefinitions.Count} tile definitions, {_decorationDefinitions.Count} decoration definitions, and {_terrainDefinitions.Count} terrain definitions");
        MemoryProfiler.Checkpoint("TileResourceManager _Ready end");
    }

    /// <summary>
    /// Load all material definitions from the resources folder.
    /// </summary>
    private void LoadAllMaterials()
    {
        _materials = JsonResourceLoader.LoadAllFromDirectory<TileMaterialDefinition>(
            "res://resources/tiles/materials",
            m => m.Id,
            m => m.Validate(),
            JsonOptions.Default);
    }

    /// <summary>
    /// Load all atlas source definitions from the resources folder.
    /// </summary>
    private void LoadAllAtlasSources()
    {
        _atlasSources = JsonResourceLoader.LoadAllFromDirectory<TileAtlasSourceDefinition>(
            "res://resources/tiles/atlases",
            a => a.Id,
            a => a.Validate(),
            JsonOptions.WithGodotTypes);
    }

    /// <summary>
    /// Load all tile definitions from the resources folder.
    /// </summary>
    private void LoadAllTileDefinitions()
    {
        string tilesPath = "res://resources/tiles/definitions";
        string projectPath = JsonResourceLoader.ResolveResPath(tilesPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Error($"Tile definitions directory not found: {projectPath}");
            return;
        }

        // First pass: Load all base definitions (*.json files in root)
        var baseDefinitions = new Dictionary<string, TileDefinition>();
        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            var tileDefinition = TileDefinition.LoadFromJson(file);
            if (tileDefinition != null && tileDefinition.Validate())
            {
                var id = tileDefinition.Id;
                if (id != null)
                {
                    baseDefinitions[id] = tileDefinition;
                    Log.Print($"Loaded base tile definition: {tileDefinition.Id}");
                }
            }
            else
            {
                Log.Error($"Failed to load base tile definition from: {file}");
            }
        }

        // Second pass: Process subdirectories for variant definitions
        foreach (var directory in Directory.GetDirectories(projectPath))
        {
            string dirName = Path.GetFileName(directory);

            // Check if we have a corresponding base definition
            if (baseDefinitions.TryGetValue(dirName, out var baseDefinition))
            {
                // Load all variant files in this subdirectory
                foreach (var variantFile in Directory.GetFiles(directory, "*.json"))
                {
                    var variantDefinition = TileDefinition.LoadFromJson(variantFile);
                    if (variantDefinition != null)
                    {
                        // Merge the variant with the base definition
                        var mergedDefinition = baseDefinition.MergeWithVariant(variantDefinition);

                        if (mergedDefinition.Validate())
                        {
                            // Update the base definition with merged data
                            baseDefinitions[dirName] = mergedDefinition;

                            string variantFileName = Path.GetFileNameWithoutExtension(variantFile);
                            Log.Print($"Merged variant definition: {dirName}/{variantFileName}");
                        }
                        else
                        {
                            Log.Error($"Merged tile definition failed validation: {dirName}/{Path.GetFileName(variantFile)}");
                        }
                    }
                    else
                    {
                        Log.Error($"Failed to load variant definition from: {variantFile}");
                    }
                }
            }
            else
            {
                Log.Error($"Found variant directory '{dirName}' but no corresponding base definition");
            }
        }

        // Final pass: Add all validated definitions to the main collection
        foreach (var kvp in baseDefinitions)
        {
            var tileDefinition = kvp.Value;

            // Check if using the new category system or legacy system
            bool usesCategories = tileDefinition.Categories != null && tileDefinition.Categories.Count > 0;
            bool hasLegacyAtlas = !string.IsNullOrEmpty(tileDefinition.AtlasSource);

            if (usesCategories || hasLegacyAtlas)
            {
                _tileDefinitions[kvp.Key] = tileDefinition;
                Log.Print($"Registered tile definition: {tileDefinition.Id}");
            }
            else
            {
                Log.Error($"Tile definition has neither categories nor a legacy atlas source: {tileDefinition.Id}");
            }
        }
    }

    private void LoadAllDecorationDefinitions()
    {
        _decorationDefinitions = JsonResourceLoader.LoadAllFromDirectory<DecorationDefinition>(
            "res://resources/decorations/definitions",
            d => d.Id,
            d => d.Validate(),
            JsonOptions.WithGodotTypes);
    }

    private void LoadAllDecorationAnimations()
    {
        _decorationAnimations = JsonResourceLoader.LoadAllFromDirectory<Beings.SpriteAnimationDefinition>(
            "res://resources/decorations/animations",
            a => a.Id,
            validate: null,
            JsonOptions.Default);
    }

    /// <summary>
    /// Load all terrain tile definitions from the resources folder.
    /// </summary>
    private void LoadAllTerrainDefinitions()
    {
        _terrainDefinitions = JsonResourceLoader.LoadAllFromDirectory<TerrainTileDefinition>(
            "res://resources/tiles/terrain",
            t => t.Id,
            t => t.Validate(),
            JsonOptions.WithVector2I);
    }

    /// <summary>
    /// Get a terrain tile by ID, resolving the atlas source ID at runtime.
    /// Returns null if the terrain definition or atlas source is not found.
    /// Note: Requires SetupTileSet() to have been called first to populate source IDs.
    /// </summary>
    public Grid.Tile? GetTerrainTile(string id)
    {
        if (!_terrainDefinitions.TryGetValue(id, out var def))
        {
            Log.Error($"Terrain tile definition not found: {id}");
            return null;
        }

        int sourceId = GetTileSetSourceId(def.AtlasSource);
        if (sourceId == -1)
        {
            Log.Error($"Atlas source not found for terrain tile '{id}': {def.AtlasSource}");
            return null;
        }

        return new Grid.Tile(sourceId, def.AtlasCoords, def.IsWalkable, def.WalkDifficulty);
    }

    /// <summary>
    /// Get a decoration definition by ID.
    /// </summary>
    public DecorationDefinition? GetDecorationDefinition(string id)
    {
        return _decorationDefinitions.TryGetValue(id, out var def) ? def : null;
    }

    /// <summary>
    /// Get a decoration animation definition by ID.
    /// </summary>
    public Beings.SpriteAnimationDefinition? GetDecorationAnimation(string id)
    {
        return _decorationAnimations.TryGetValue(id, out var anim) ? anim : null;
    }

    /// <summary>
    /// Get atlas texture and metadata for building Sprite2D decorations.
    /// </summary>
    public (Texture2D Texture, Vector2I TileSize, Vector2I Margin, Vector2I Separation)? GetAtlasInfo(string atlasSourceId)
    {
        if (!_atlasSources.TryGetValue(atlasSourceId, out var atlasDef))
        {
            return null;
        }

        if (!_loadedTextures.TryGetValue(atlasDef.TexturePath, out var texture))
        {
            // Try loading it
            texture = ResourceLoader.Load<Texture2D>(atlasDef.TexturePath);
            if (texture == null)
            {
                return null;
            }

            _loadedTextures[atlasDef.TexturePath] = texture;
        }

        return (texture, atlasDef.TileSize, atlasDef.Margin, atlasDef.Separation);
    }

    /// <summary>
    /// Get a cached AtlasTexture by atlas source ID and grid coordinates.
    /// Creates the AtlasTexture once; subsequent calls with the same parameters return the same instance.
    /// </summary>
    /// <param name="atlasSourceId">The atlas source ID (e.g., "kenney_1bit", "dcss_utumno").</param>
    /// <param name="row">Row in the atlas grid (Y coordinate).</param>
    /// <param name="col">Column in the atlas grid (X coordinate).</param>
    /// <param name="widthInTiles">Width in tiles (default 1). For multi-tile sprites.</param>
    /// <param name="heightInTiles">Height in tiles (default 1). For multi-tile sprites.</param>
    /// <returns>The cached AtlasTexture, or null if the atlas source could not be found.</returns>
    public AtlasTexture? GetCachedAtlasTexture(string atlasSourceId, int row, int col, int widthInTiles = 1, int heightInTiles = 1)
    {
        var key = (atlasSourceId, row, col, widthInTiles, heightInTiles);
        if (_atlasTextureCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (!_atlasSources.TryGetValue(atlasSourceId, out var atlasDef))
        {
            Log.Error($"Atlas source not found for AtlasTexture cache: {atlasSourceId}");
            return null;
        }

        // Load or retrieve the backing texture
        if (!_loadedTextures.TryGetValue(atlasDef.TexturePath, out var texture))
        {
            texture = ResourceLoader.Load<Texture2D>(atlasDef.TexturePath);
            if (texture == null)
            {
                Log.Error($"Failed to load texture for AtlasTexture cache: {atlasDef.TexturePath}");
                return null;
            }

            _loadedTextures[atlasDef.TexturePath] = texture;
        }

        var m = atlasDef.Margin;
        var s = atlasDef.Separation;
        var ts = atlasDef.TileSize;

        int pw = (widthInTiles * ts.X) + ((widthInTiles - 1) * s.X);
        int ph = (heightInTiles * ts.Y) + ((heightInTiles - 1) * s.Y);

        var atlasTexture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(
                m.X + (col * (ts.X + s.X)),
                m.Y + (row * (ts.Y + s.Y)),
                pw,
                ph)
        };

        _atlasTextureCache[key] = atlasTexture;
        return atlasTexture;
    }

    /// <summary>
    /// Get a cached SpriteFrames for a decoration animation.
    /// Built once per animation ID, shared across all instances.
    /// </summary>
    /// <param name="animationId">The animation definition ID.</param>
    /// <returns>The cached SpriteFrames, or null if the animation is not found.</returns>
    public SpriteFrames? GetCachedSpriteFrames(string animationId)
    {
        if (_spriteFramesCache.TryGetValue(animationId, out var cached))
        {
            return cached;
        }

        var animDef = GetDecorationAnimation(animationId);
        if (animDef == null)
        {
            return null;
        }

        var spriteFrames = animDef.CreateSpriteFrames();
        _spriteFramesCache[animationId] = spriteFrames;
        return spriteFrames;
    }

    /// <summary>
    /// Build the shared TileSet with all atlas sources. Called once on first use.
    /// </summary>
    private TileSet BuildSharedTileSet()
    {
        var tileSet = new TileSet
        {
            TileSize = new Vector2I((int)Grid.Utils.TileSize, (int)Grid.Utils.TileSize)
        };

        foreach (var atlasSource in _atlasSources.Values)
        {
            try
            {
                // Get or assign a stable source ID for this atlas
                if (!_tilesetSourceIds.TryGetValue(atlasSource.Id, out int sourceId))
                {
                    sourceId = _tilesetSourceIds.Count;
                    _tilesetSourceIds[atlasSource.Id] = sourceId;
                }

                // Load the texture
                if (!_loadedTextures.TryGetValue(atlasSource.TexturePath, out var texture))
                {
                    texture = ResourceLoader.Load<Texture2D>(atlasSource.TexturePath);
                    if (texture != null)
                    {
                        _loadedTextures[atlasSource.TexturePath] = texture;
                    }
                    else
                    {
                        Log.Error($"Failed to load texture: {atlasSource.TexturePath}");
                        continue;
                    }
                }

                // Create the atlas source
                var tileSetAtlasSource = new TileSetAtlasSource
                {
                    Texture = texture,
                    TextureRegionSize = atlasSource.TileSize,
                    Margins = atlasSource.Margin,
                    Separation = atlasSource.Separation,
                    ResourceName = atlasSource.Id
                };

                // Center smaller tiles within 32x32 cells if any atlas uses sub-32 tiles
                if (atlasSource.TileSize.X < (int)Grid.Utils.TileSize || atlasSource.TileSize.Y < (int)Grid.Utils.TileSize)
                {
                    tileSetAtlasSource.UseTexturePadding = true;
                }

                tileSet.AddSource(tileSetAtlasSource, sourceId);
                Log.Print($"Added atlas source {atlasSource.Id} as source ID {sourceId} with texture {texture.GetSize()}");
            }
            catch (Exception e)
            {
                Log.Error($"Error adding atlas source {atlasSource.Id}: {e.Message}");
            }
        }

        return tileSet;
    }

    /// <summary>
    /// Assign the shared TileSet to a TileMapLayer.
    /// The TileSet is created once and reused by all layers to avoid duplicating atlas sources.
    /// </summary>
    /// <param name="tileMap">The TileMapLayer to configure.</param>
    public void SetupTileSet(TileMapLayer tileMap)
    {
        _sharedTileSet ??= BuildSharedTileSet();
        tileMap.TileSet = _sharedTileSet;
    }

    /// <summary>
    /// Get the shared TileSet instance.
    /// Used for programmatic areas (e.g., cellars) that don't have a scene-defined TileMapLayer.
    /// </summary>
    public TileSet GetTileSet()
    {
        _sharedTileSet ??= BuildSharedTileSet();
        return _sharedTileSet;
    }

    /// <summary>
    /// Get a material definition by ID.
    /// </summary>
    /// <param name="materialId">The material ID.</param>
    /// <returns>The material definition or null if not found.</returns>
    public TileMaterialDefinition? GetMaterial(string materialId)
    {
        if (_materials.TryGetValue(materialId, out var material))
        {
            return material;
        }

        return null;
    }

    /// <summary>
    /// Get a tile definition by ID.
    /// </summary>
    /// <param name="tileId">The tile ID.</param>
    /// <returns>The tile definition or null if not found.</returns>
    public TileDefinition? GetTileDefinition(string tileId)
    {
        if (_tileDefinitions.TryGetValue(tileId, out var tile))
        {
            return tile;
        }

        return null;
    }

    /// <summary>
    /// Get an atlas source definition by ID.
    /// </summary>
    /// <param name="atlasId">The atlas source ID.</param>
    /// <returns>The atlas source definition or null if not found.</returns>
    public TileAtlasSourceDefinition? GetAtlasSource(string atlasId)
    {
        if (_atlasSources.TryGetValue(atlasId, out var atlas))
        {
            return atlas;
        }

        return null;
    }

    /// <summary>
    /// Get the TileSet source ID for an atlas source.
    /// </summary>
    /// <param name="atlasId">The atlas source ID.</param>
    /// <returns>The TileSet source ID or -1 if not found.</returns>
    public int GetTileSetSourceId(string atlasId)
    {
        if (_tilesetSourceIds.TryGetValue(atlasId, out var sourceId))
        {
            return sourceId;
        }

        return -1;
    }

    /// <summary>
    /// Process tile variants and return the final variant definition with merged attributes.
    /// </summary>
    /// <param name="tileDef">The tile definition.</param>
    /// <param name="materialId">The material ID to use.</param>
    /// <param name="variantName">The variant name to use (optional).</param>
    /// <param name="categoryName">The category name to use (optional, defaults to "Default").</param>
    /// <returns>A dictionary with the merged atlas information.</returns>
    public static Dictionary<string, object?> GetProcessedVariant(TileDefinition tileDef, string materialId, string? variantName = null, string? categoryName = null)
    {
        // Initialize result with base attributes from tile definition
        var result = new Dictionary<string, object?>
        {
            ["AtlasSource"] = tileDef.AtlasSource,
            ["AtlasCoords"] = tileDef.AtlasCoords,
            ["Tint"] = (string?)null
        };

        // If no category system is used, return legacy values
        if (tileDef.Categories == null || tileDef.Categories.Count == 0)
        {
            return result;
        }

        // Determine which category to use
        string targetCategory = categoryName ?? "Default";

        // Get the target category
        if (!tileDef.Categories.TryGetValue(targetCategory, out var category))
        {
            // If specified category doesn't exist, try Default category
            if (!tileDef.Categories.TryGetValue("Default", out category))
            {
                // If no Default category either, use the first available category
                category = tileDef.Categories.Values.FirstOrDefault();
                if (category == null)
                {
                    return result;
                }
            }
        }

        // 1. Merge global default variant if exists within the category
        if (category.Variants.TryGetValue("Default", out var defaultVariants))
        {
            if (defaultVariants.TryGetValue("Default", out var globalDefault))
            {
                if (globalDefault.AtlasSource != null)
                {
                    result["AtlasSource"] = globalDefault.AtlasSource;
                }

                if (globalDefault?.AtlasCoords != null)
                {
                    result["AtlasCoords"] = globalDefault.AtlasCoords;
                }

                if (!string.IsNullOrEmpty(globalDefault?.Tint))
                {
                    result["Tint"] = globalDefault.Tint;
                }
            }
        }

        // 2. Merge material-specific default if exists within the category
        if (!string.IsNullOrEmpty(materialId) && category.Variants.TryGetValue(materialId, out var materialVariants))
        {
            if (materialVariants.TryGetValue("Default", out var materialDefault))
            {
                if (materialDefault.AtlasSource != null)
                {
                    result["AtlasSource"] = materialDefault.AtlasSource;
                }

                if (materialDefault?.AtlasCoords != null)
                {
                    result["AtlasCoords"] = materialDefault.AtlasCoords;
                }

                if (!string.IsNullOrEmpty(materialDefault?.Tint))
                {
                    result["Tint"] = materialDefault.Tint;
                }
            }
        }

        // 3. Merge specific variant if specified and exists within the category
        if (!string.IsNullOrEmpty(variantName) && !string.IsNullOrEmpty(materialId) &&
            category.Variants.TryGetValue(materialId, out var materialVariants2))
        {
            if (materialVariants2.TryGetValue(variantName, out var specificVariant))
            {
                if (specificVariant.AtlasSource != null)
                {
                    result["AtlasSource"] = specificVariant.AtlasSource;
                }

                if (specificVariant?.AtlasCoords != null)
                {
                    result["AtlasCoords"] = specificVariant.AtlasCoords;
                }

                if (!string.IsNullOrEmpty(specificVariant?.Tint))
                {
                    result["Tint"] = specificVariant.Tint;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Create building tiles from a tile definition.
    /// </summary>
    /// <param name="tileId">The tile definition ID.</param>
    /// <param name="position">The position in the building.</param>
    /// <param name="parent">The parent building.</param>
    /// <param name="gridPosition">The absolute grid position.</param>
    /// <param name="materialId">The material ID to use (optional).</param>
    /// <param name="variantName">The variant name to use (optional).</param>
    /// <param name="categoryName">The category name to use (optional).</param>
    /// <returns>A BuildingTile instance.</returns>
    /// <exception cref="System.InvalidOperationException">Thrown when a required resource is not found.</exception>
    public BuildingTile CreateBuildingTile(string tileId, Vector2I position, Building parent, Vector2I gridPosition, string materialId, string? variantName = null, string? categoryName = null)
    {
        if (!_tileDefinitions.TryGetValue(tileId, out var tileDef))
        {
            var errorMessage = $"Tile definition not found: {tileId}";
            Log.Error(errorMessage);
            throw new System.InvalidOperationException(errorMessage);
        }

        // If no material was specified, use the default from the tile definition
        if (string.IsNullOrEmpty(materialId) && !string.IsNullOrEmpty(tileDef.DefaultMaterial))
        {
            materialId = tileDef.DefaultMaterial;
        }

        // If no variant specified, use "Default"
        if (string.IsNullOrEmpty(variantName))
        {
            variantName = "Default";
        }

        // Process variant information for merged atlas settings
        var processedVariant = GetProcessedVariant(tileDef, materialId, variantName, categoryName);

        string? atlasSource = (string?)processedVariant["AtlasSource"];
        var atlasCoords = (Vector2I?)processedVariant["AtlasCoords"];

        if (atlasSource == null || atlasCoords == null)
        {
            throw new InvalidOperationException("Atlas coords/source null");
        }

        // Get source ID from atlas name
        int sourceId = GetTileSetSourceId(atlasSource);
        if (sourceId == -1)
        {
            var errorMessage = $"Atlas source not found: {atlasSource}";
            Log.Error(errorMessage);
            throw new System.InvalidOperationException(errorMessage);
        }

        // Parse the tile type
        TileType tileType;
        if (!Enum.TryParse(tileDef.Type, out tileType))
        {
            var errorMessage = $"Invalid tile type: {tileDef.Type}";
            Log.Error(errorMessage);
            throw new System.InvalidOperationException(errorMessage);
        }

        // Get material properties if specified
        TileMaterialDefinition? material = null;
        if (!string.IsNullOrEmpty(materialId))
        {
            material = GetMaterial(materialId);
            if (material == null)
            {
                var errorMessage = $"Material definition not found: {materialId}";
                Log.Error(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }
        }

        // Adjust durability based on material
        int durability = tileDef.BaseDurability;
        if (material != null)
        {
            durability = (int)(durability * material.DurabilityModifier);
        }

        // Create the building tile
        var tile = new BuildingTile(
            tileType,
            materialId ?? "default",
            variantName,
            tileDef.IsWalkable,
            durability,
            (Vector2I)atlasCoords,
            sourceId,
            parent,
            gridPosition);

        // Apply custom detection difficulties based on tile definition and material
        foreach (SenseType senseType in Enum.GetValues<SenseType>())
        {
            float baseDifficulty = tileDef.GetDefaultSensoryDifficulty(senseType);
            float materialModifier = material?.GetSensoryModifier(senseType) ?? 1.0f;

            tile.DetectionDifficulties[senseType] = baseDifficulty * materialModifier;
        }

        // Populate tint cascade data from tile definition and variant
        tile.DefinitionDefaultTint = tileDef.DefaultTint;
        var resolvedVariantTint = (string?)processedVariant.GetValueOrDefault("Tint");
        tile.VariantTint = resolvedVariantTint;

        return tile;
    }
}
