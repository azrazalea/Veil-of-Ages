using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VeilOfAges.Entities.Sensory;

namespace VeilOfAges.Entities
{
    /// <summary>
    /// Manages tile-related resources including materials, tile definitions, and atlas sources
    /// </summary>
    public class TileResourceManager
    {
        // Singleton instance
        private static TileResourceManager _instance;
        public static TileResourceManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new TileResourceManager();
                }
                return _instance;
            }
        }

        // Resource collections
        private Dictionary<string, TileMaterialDefinition> _materials = new();
        private Dictionary<string, TileDefinition> _tileDefinitions = new();
        private Dictionary<string, TileAtlasSourceDefinition> _atlasSources = new();

        // Atlas source ID to tileset source ID mapping
        private Dictionary<string, int> _tilesetSourceIds = new();

        // Loaded texture resources
        private Dictionary<string, Texture2D> _loadedTextures = new();

        // Has this manager been initialized?
        private bool _initialized = false;

        // Private constructor to enforce singleton pattern
        private TileResourceManager() { }

        /// <summary>
        /// Initialize the resource manager by loading all resources
        /// </summary>
        public void Initialize()
        {
            if (_initialized) return;

            LoadAllMaterials();
            LoadAllAtlasSources();
            LoadAllTileDefinitions();

            _initialized = true;
            GD.Print($"TileResourceManager initialized with {_materials.Count} materials, {_atlasSources.Count} atlas sources, and {_tileDefinitions.Count} tile definitions");
        }

        /// <summary>
        /// Load all material definitions from the resources folder
        /// </summary>
        private void LoadAllMaterials()
        {
            string materialsPath = "res://resources/tiles/materials";
            string projectPath = ProjectSettings.GlobalizePath(materialsPath);

            if (!Directory.Exists(projectPath))
            {
                GD.PrintErr($"Materials directory not found: {projectPath}");
                return;
            }

            foreach (var file in Directory.GetFiles(projectPath, "*.json"))
            {
                var material = TileMaterialDefinition.LoadFromJson(file);
                if (material != null && material.Validate())
                {
                    _materials[material.Id] = material;
                    GD.Print($"Loaded material: {material.Id}");
                }
                else
                {
                    GD.PrintErr($"Failed to load material from: {file}");
                }
            }
        }

        /// <summary>
        /// Load all atlas source definitions from the resources folder
        /// </summary>
        private void LoadAllAtlasSources()
        {
            string atlasesPath = "res://resources/tiles/atlases";
            string projectPath = ProjectSettings.GlobalizePath(atlasesPath);

            if (!Directory.Exists(projectPath))
            {
                GD.PrintErr($"Atlas sources directory not found: {projectPath}");
                return;
            }

            foreach (var file in Directory.GetFiles(projectPath, "*.json"))
            {
                var atlasSource = TileAtlasSourceDefinition.LoadFromJson(file);
                if (atlasSource != null && atlasSource.Validate())
                {
                    _atlasSources[atlasSource.Id] = atlasSource;
                    GD.Print($"Loaded atlas source: {atlasSource.Id}");
                }
                else
                {
                    GD.PrintErr($"Failed to load atlas source from: {file}");
                }
            }
        }

        /// <summary>
        /// Load all tile definitions from the resources folder
        /// </summary>
        private void LoadAllTileDefinitions()
        {
            string tilesPath = "res://resources/tiles/definitions";
            string projectPath = ProjectSettings.GlobalizePath(tilesPath);

            if (!Directory.Exists(projectPath))
            {
                GD.PrintErr($"Tile definitions directory not found: {projectPath}");
                return;
            }

            foreach (var file in Directory.GetFiles(projectPath, "*.json"))
            {
                var tileDefinition = TileDefinition.LoadFromJson(file);
                if (tileDefinition != null && tileDefinition.Validate())
                {
                    // Verify that the referenced atlas source exists
                    if (_atlasSources.ContainsKey(tileDefinition.AtlasSource))
                    {
                        _tileDefinitions[tileDefinition.Id] = tileDefinition;
                        GD.Print($"Loaded tile definition: {tileDefinition.Id}");
                    }
                    else
                    {
                        GD.PrintErr($"Tile definition references unknown atlas source: {tileDefinition.AtlasSource}");
                    }
                }
                else
                {
                    GD.PrintErr($"Failed to load tile definition from: {file}");
                }
            }
        }

        /// <summary>
        /// Setup a TileSet with all required atlas sources for the given TileMap
        /// </summary>
        /// <param name="tileMap">The TileMap to configure</param>
        public void SetupTileSet(TileMap tileMap)
        {
            // Create a new TileSet if needed
            if (tileMap.TileSet == null)
            {
                tileMap.TileSet = new TileSet();
            }

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
                    Texture2D texture;
                    if (!_loadedTextures.TryGetValue(atlasSource.TexturePath, out texture))
                    {
                        texture = ResourceLoader.Load<Texture2D>(atlasSource.TexturePath);
                        if (texture != null)
                        {
                            _loadedTextures[atlasSource.TexturePath] = texture;
                        }
                        else
                        {
                            GD.PrintErr($"Failed to load texture: {atlasSource.TexturePath}");
                            continue;
                        }
                    }

                    // Create the atlas source
                    var tileSetAtlasSource = new TileSetAtlasSource();

                    // Set texture
                    tileSetAtlasSource.Texture = texture;

                    // Set tile size
                    tileSetAtlasSource.TextureRegionSize = atlasSource.TileSize;

                    // Set margin and separation
                    tileSetAtlasSource.Margins = atlasSource.Margin;
                    tileSetAtlasSource.Separation = atlasSource.Separation;


                    tileSetAtlasSource.ResourceName = atlasSource.Id;

                    // Add the source to the tileset
                    tileMap.TileSet.AddSource(tileSetAtlasSource, sourceId);

                    // Store the mapping
                    _tilesetSourceIds[atlasSource.Id] = sourceId;

                    GD.Print($"Added atlas source {atlasSource.Id} as source ID {sourceId}");
                    sourceId++;
                }
                catch (Exception e)
                {
                    GD.PrintErr($"Error adding atlas source {atlasSource.Id}: {e.Message}");
                }
            }
        }

        /// <summary>
        /// Get a material definition by ID
        /// </summary>
        /// <param name="materialId">The material ID</param>
        /// <returns>The material definition or null if not found</returns>
        public TileMaterialDefinition GetMaterial(string materialId)
        {
            if (_materials.TryGetValue(materialId, out var material))
            {
                return material;
            }
            return null;
        }

        /// <summary>
        /// Get a tile definition by ID
        /// </summary>
        /// <param name="tileId">The tile ID</param>
        /// <returns>The tile definition or null if not found</returns>
        public TileDefinition GetTileDefinition(string tileId)
        {
            if (_tileDefinitions.TryGetValue(tileId, out var tile))
            {
                return tile;
            }
            return null;
        }

        /// <summary>
        /// Get an atlas source definition by ID
        /// </summary>
        /// <param name="atlasId">The atlas source ID</param>
        /// <returns>The atlas source definition or null if not found</returns>
        public TileAtlasSourceDefinition GetAtlasSource(string atlasId)
        {
            if (_atlasSources.TryGetValue(atlasId, out var atlas))
            {
                return atlas;
            }
            return null;
        }

        /// <summary>
        /// Get the TileSet source ID for an atlas source
        /// </summary>
        /// <param name="atlasId">The atlas source ID</param>
        /// <returns>The TileSet source ID or -1 if not found</returns>
        public int GetTileSetSourceId(string atlasId)
        {
            if (_tilesetSourceIds.TryGetValue(atlasId, out var sourceId))
            {
                return sourceId;
            }
            return -1;
        }

        /// <summary>
        /// Create building tiles from a tile definition
        /// </summary>
        /// <param name="tileId">The tile definition ID</param>
        /// <param name="position">The position in the building</param>
        /// <param name="parent">The parent building</param>
        /// <param name="gridPosition">The absolute grid position</param>
        /// <param name="materialId">The material ID to use (optional)</param>
        /// <returns>A BuildingTile instance</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when a required resource is not found</exception>
        public BuildingTile CreateBuildingTile(string tileId, Vector2I position, Building parent, Vector2I gridPosition, string materialId = null)
        {
            if (!_tileDefinitions.TryGetValue(tileId, out var tileDef))
            {
                var errorMessage = $"Tile definition not found: {tileId}";
                GD.PrintErr(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }

            // Get source ID from atlas name
            int sourceId = GetTileSetSourceId(tileDef.AtlasSource);
            if (sourceId == -1)
            {
                var errorMessage = $"Atlas source not found: {tileDef.AtlasSource}";
                GD.PrintErr(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }

            // Parse the tile type
            TileType tileType;
            if (!Enum.TryParse(tileDef.Type, out tileType))
            {
                var errorMessage = $"Invalid tile type: {tileDef.Type}";
                GD.PrintErr(errorMessage);
                throw new System.InvalidOperationException(errorMessage);
            }

            // If no material was specified, use the default from the tile definition
            if (string.IsNullOrEmpty(materialId) && !string.IsNullOrEmpty(tileDef.DefaultMaterial))
            {
                materialId = tileDef.DefaultMaterial;
            }

            // Get material properties if specified
            TileMaterialDefinition material = null;
            if (!string.IsNullOrEmpty(materialId))
            {
                material = GetMaterial(materialId);
                if (material == null)
                {
                    var errorMessage = $"Material definition not found: {materialId}";
                    GD.PrintErr(errorMessage);
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
                tileDef.IsWalkable,
                durability,
                tileDef.AtlasCoords,
                sourceId,
                parent,
                gridPosition
            );

            // Apply custom detection difficulties based on tile definition and material
            foreach (SenseType senseType in Enum.GetValues(typeof(SenseType)))
            {
                float baseDifficulty = tileDef.GetDefaultSensoryDifficulty(senseType);
                float materialModifier = material?.GetSensoryModifier(senseType) ?? 1.0f;

                tile.DetectionDifficulties[senseType] = baseDifficulty * materialModifier;
            }

            return tile;
        }
    }
}
