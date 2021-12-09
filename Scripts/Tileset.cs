using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using GuiLabs.Undo;

public class TilesetEntry
{
    public readonly MeshInstance TileMesh;
    public readonly string Path;
    public readonly string Name;

    public TilesetEntry(MeshInstance tileMesh, string path, string name)
    {
        TileMesh = tileMesh;
        Path = path;
        Name = name;
    }

    public override bool Equals(object obj)
    {
        // If paths are equal then everything else will effectively be as well
        return obj is TilesetEntry other && other.Path == Path;
    }
    public override int GetHashCode()
    {
        return Path.GetHashCode();
    }
}
public enum TileLoadResult
{
    Success = 0,
    FailedToOpenFile,
    FileContainedZeroOrMultipleMeshes,
    FileAlreadyPresent
}
partial class GridManager
{
    class AddTilesetEntryAction : AbstractAction
    {
        private TilesetEntry entry;
        private GridManager manager;
        private int libraryId;
        private int entryIdx;
        public AddTilesetEntryAction(TilesetEntry entry, GridManager manager)
        {
            this.entry = entry;
            this.manager = manager;
            libraryId = -1;
            entryIdx = -1;
        }
        protected override void ExecuteCore()
        {
            if (libraryId == -1)
                libraryId = manager.tileLibrary.GetLastUnusedItemId();
            if (entryIdx == -1)
                entryIdx = manager.tilesetEntries.Count;
            manager.AddTilesetEntryImpl(entry, entryIdx, libraryId);
        }

        protected override void UnExecuteCore()
        {
            manager.RemoveTilesetEntryImpl(entry, entryIdx, libraryId);
        }
    }

    class RemoveTilesetEntryAction : AbstractAction
    {
        private TilesetEntry entry;
        private GridManager manager;
        private int libraryId;
        private int entryIdx;
        public RemoveTilesetEntryAction(TilesetEntry entry, GridManager manager)
        {
            this.entry = entry;
            this.manager = manager;
            libraryId = -1;
            entryIdx = -1;
        }
        protected override void ExecuteCore()
        {
            if (libraryId == -1)
                libraryId = manager.tileLibrary.FindItemByName(entry.Name);
            if (entryIdx == -1)
                entryIdx = manager.tilesetEntries.IndexOf(entry);
            manager.RemoveTilesetEntryImpl(entry, entryIdx, libraryId);
        }

        protected override void UnExecuteCore()
        {
            manager.AddTilesetEntryImpl(entry, entryIdx, libraryId);
        }
    }
    private static MeshInstance GetMeshFromNode(Node cur_node)
    {
        MeshInstance ret = null;
        if (cur_node is MeshInstance mesh)
        {
            ret = mesh;
        }
        foreach (Node child in cur_node.GetChildren())
        {
            MeshInstance childMesh = GetMeshFromNode(child);
            if (childMesh != null)
            {
                if (ret != null)
                {
                    // More than one mesh found, so return none
                    return null;
                }
                ret = childMesh;
            }
        }
        return ret;
    }
    // glTF loader GDNative module from https://github.com/godotengine/godot/issues/24768
    private NativeScript gltfStateScript;
    private PackedScene packedSceneGltf;
    private void InitTileLoader()
    {
        // Load the GLTFState script
        gltfStateScript = GD.Load("res://addons/godot_gltf/GLTFState.gdns") as NativeScript;
        // Load the gltf importer module
        NativeScript packedSceneGltfScript = GD.Load("res://addons/godot_gltf/PackedSceneGLTF.gdns") as NativeScript;
        packedSceneGltf = packedSceneGltfScript.New(new object[] {}) as PackedScene;
    }
    private Spatial OpenTileFile(string path)
    {
        try
        {
            Resource gltfState = gltfStateScript.New(new object[] {}) as Resource;
            Spatial ret = packedSceneGltf.Call("import_gltf_scene", path, 0, 1000.0f, gltfState) as Spatial;
            var json = gltfState.Get("json") as Godot.Collections.Dictionary;
            // foreach (var item in json)
            // {
            //     if (item != null)
            //     {
            //         var entry = (System.Collections.DictionaryEntry)item;
            //         GD.Print(entry.Key, " : ", entry.Value);
            //     }
            //     // GD.Print(entry.ToString());
            // }
            List<Image> images = new List<Image>();
            if (json.Contains("images"))
            {
                foreach (var image in json["images"] as Godot.Collections.Array)
                {
                    if (image != null)
                    {
                        string uri = ((Godot.Collections.Dictionary)image)["uri"] as string;
                        string imagePath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), uri);
                        Image newImage = new Image();
                        newImage.Load(imagePath);
                        images.Add(newImage);
                    }
                    else
                    {
                        images.Add(null);
                    }
                }
            }
            Dictionary<string, int> materialTextures = new Dictionary<string, int>();
            if (json.Contains("materials"))
            {
                foreach (var material in json["materials"] as Godot.Collections.Array)
                {
                    // GD.Print(material);
                    if (material != null)
                    {
                        var matDict = ((Godot.Collections.Dictionary)material);
                        // Could grab the combiner by checking for N64_materials_gltf64 extension here
                        string matName = matDict["name"] as string;
                        if (matDict.Contains("pbrMetallicRoughness"))
                        {
                            var pbrMetallicRoughness = matDict["pbrMetallicRoughness"];
                            if (pbrMetallicRoughness != null)
                            {
                                var pbrDict = ((Godot.Collections.Dictionary)pbrMetallicRoughness);
                                if (pbrDict.Contains("baseColorTexture"))
                                {
                                    var baseColorTexture = pbrDict["baseColorTexture"];
                                    if (baseColorTexture != null)
                                    {
                                        var baseColorTextureDict = ((Godot.Collections.Dictionary)baseColorTexture);
                                        if (baseColorTextureDict.Contains("index"))
                                        {
                                            var textureIdxStr = (System.Single)baseColorTextureDict["index"];
                                            materialTextures[matName] = ((int)textureIdxStr);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            Dictionary<int, ImageTexture> imageTextures = new Dictionary<int, ImageTexture>();
            var textures = gltfState.Get("textures") as Godot.Collections.Array;
            int textureCount = 0;
            foreach (var texture in textures.Cast<Resource>())
            {
                ImageTexture newTex = new ImageTexture();
                newTex.CreateFromImage(images[(int)texture.Get("src_image")]);
                imageTextures[textureCount] = newTex;
                textureCount++;
            }

            var materials = gltfState.Get("materials") as Godot.Collections.Array;
            foreach (var material in materials.Cast<SpatialMaterial>())
            {
                if (material != null)
                {
                    if (materialTextures.TryGetValue(material.ResourceName, out int textureIdx))
                    {
                        if (imageTextures.TryGetValue(textureIdx, out ImageTexture imageTex))
                        {
                            material.AlbedoTexture = imageTex;
                        }
                    }
                }
            }
            return ret;
        }
        catch (Exception e)
        {
            GD.Print("Exception loading model: ", e.Message, "\n", e.StackTrace);
            return null;
        }
    }
    public TileLoadResult LoadTile(string path, bool recordAction = true)
    {
        path = System.IO.Path.GetFullPath(path);
        if (HasTileWithPath(path))
        {
            return TileLoadResult.FileAlreadyPresent;
        }
        string modelName = System.IO.Path.GetFileNameWithoutExtension(path);
        Spatial model = OpenTileFile(path);
        if (model == null)
        {
            return TileLoadResult.FailedToOpenFile;
        }
        else
        {
            MeshInstance mesh = GetMeshFromNode(model);
            if (mesh != null)
            {
                TilesetEntry newEntry = new TilesetEntry(mesh.Duplicate() as MeshInstance, path, modelName);
                model.QueueFree();
                if (recordAction)
                {
                    AddTilesetEntry(newEntry);
                }
                else
                {
                    int libraryId = tileLibrary.GetLastUnusedItemId();
                    int entryIdx = tilesetEntries.Count;
                    AddTilesetEntryImpl(newEntry, entryIdx, libraryId);
                }
            }
            else
            {
                return TileLoadResult.FileContainedZeroOrMultipleMeshes;
            }
        }
        return TileLoadResult.Success;
    }

}