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
public class TileResourceManager
{
    // Singleton instance
    private static TileResourceManager? _instance;
    public static TileResourceManager Instance
    {
        get
        {
            _instance ??= new TileResourceManager();
            return _instance;
        }
    }

    // Resource collections
    private readonly Dictionary<string, TileMaterialDefinition> _materials = new ();
    private readonly Dictionary<string, TileDefinition> _tileDefinitions = new ();
    private readonly Dictionary<string, TileAtlasSourceDefinition> _atlasSources = new ();

    // Atlas source ID to tileset source ID mapping
    private readonly Dictionary<string, int> _tilesetSourceIds = new ();

    // Loaded texture resources
    private readonly Dictionary<string, Texture2D> _loadedTextures = new ();

    // Has this manager been initialized?
    private bool _initialized;

    // Private constructor to enforce singleton pattern
    private TileResourceManager()
    {
    }

    /// <summary>
    /// Initialize the resource manager by loading all resources.
    /// </summary>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        LoadAllMaterials();
        LoadAllAtlasSources();
        LoadAllTileDefinitions();

        _initialized = true;
        Log.Print($"TileResourceManager initialized with {_materials.Count} materials, {_atlasSources.Count} atlas sources, and {_tileDefinitions.Count} tile definitions");
    }

    /// <summary>
    /// Load all material definitions from the resources folder.
    /// </summary>
    private void LoadAllMaterials()
    {
        string materialsPath = "res://resources/tiles/materials";
        string projectPath = ProjectSettings.GlobalizePath(materialsPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Error($"Materials directory not found: {projectPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            var material = TileMaterialDefinition.LoadFromJson(file);
            if (material != null && material.Validate())
            {
                _materials[material.Id] = material;
                Log.Print($"Loaded material: {material.Id}");
            }
            else
            {
                Log.Error($"Failed to load material from: {file}");
            }
        }
    }

    /// <summary>
    /// Load all atlas source definitions from the resources folder.
    /// </summary>
    private void LoadAllAtlasSources()
    {
        string atlasesPath = "res://resources/tiles/atlases";
        string projectPath = ProjectSettings.GlobalizePath(atlasesPath);

        if (!Directory.Exists(projectPath))
        {
            Log.Error($"Atlas sources directory not found: {projectPath}");
            return;
        }

        foreach (var file in Directory.GetFiles(projectPath, "*.json"))
        {
            var atlasSource = TileAtlasSourceDefinition.LoadFromJson(file);
            if (atlasSource != null && atlasSource.Validate())
            {
                _atlasSources[atlasSource.Id] = atlasSource;
                Log.Print($"Loaded atlas source: {atlasSource.Id}");
            }
            else
            {
                Log.Error($"Failed to load atlas source from: {file}");
            }
        }
    }

    /// <summary>
    /// Load all tile definitions from the resources folder.
    /// </summary>
    private void LoadAllTileDefinitions()
    {
        string tilesPath = "res://resources/tiles/definitions";
        string projectPath = ProjectSettings.GlobalizePath(tilesPath);

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

    /// <summary>
    /// Setup a TileSet with all required atlas sources for the given TileMap.
    /// </summary>
    /// <param name="tileMap">The TileMap to configure.</param>
    public void SetupTileSet(TileMapLayer tileMap)
    {
        // Create a new TileSet if needed
        tileMap.TileSet ??= new TileSet
        {
            TileSize = new Vector2I(8, 8)
        };

        // Clear existing sources
        // Careful: This could break existing references
        // tileMap.TileSet.Clear();
        _tilesetSourceIds.Clear();

        // Add each atlas source
        int sourceId = 0;
        foreach (var atlasSource in _atlasSources.Values)
        {
            try
            {
                // Load the texture
                Texture2D? texture;
                if (!_loadedTextures.TryGetValue(atlasSource.TexturePath, out texture))
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
                    // Set texture
                    Texture = texture,

                    // Set tile size
                    TextureRegionSize = atlasSource.TileSize,

                    // Set margin and separation
                    Margins = atlasSource.Margin,
                    Separation = atlasSource.Separation,

                    ResourceName = atlasSource.Id
                };

                // Add the source to the tileset
                tileMap.TileSet.AddSource(tileSetAtlasSource, sourceId);

                // Store the mapping
                _tilesetSourceIds[atlasSource.Id] = sourceId;

                Log.Print($"Added atlas source {atlasSource.Id} as source ID {sourceId} with texture ${texture.GetSize()}");
                sourceId++;
            }
            catch (Exception e)
            {
                Log.Error($"Error adding atlas source {atlasSource.Id}: {e.Message}");
            }
        }
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
            ["AtlasCoords"] = tileDef.AtlasCoords
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

        return tile;
    }
}
