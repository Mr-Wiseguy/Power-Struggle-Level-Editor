using System.Collections.Generic;
using System.Linq;
using System.IO;
using Godot;
using Newtonsoft.Json;
using System;

partial class GridManager
{
    class CellEntry
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public int TileIndex { get; set; }
        public int Rotation { get; set; }
    }
    class OutputObjectDefinition
    {
        public int ObjectClass;
        public int ObjectType;
        public int ObjectSubtype;
        public static readonly Dictionary<OutputObjectDefinition, ObjectDefinition> DefinitionMapping = 
            ObjectDefinition.AllEntries.ToDictionary(x => new OutputObjectDefinition() {
                    ObjectClass = (int)x.EntryClass,
                    ObjectType = (int)x.Type,
                    ObjectSubtype = (int)x.Subtype
            }, x => x);

        public override bool Equals(object obj)
        {
            return obj is OutputObjectDefinition definition &&
                   ObjectClass == definition.ObjectClass &&
                   ObjectType == definition.ObjectType &&
                   ObjectSubtype == definition.ObjectSubtype;
        }

        public override int GetHashCode()
        {
            int hashCode = -2057830444;
            hashCode = hashCode * -1521134295 + ObjectClass.GetHashCode();
            hashCode = hashCode * -1521134295 + ObjectType.GetHashCode();
            hashCode = hashCode * -1521134295 + ObjectSubtype.GetHashCode();
            return hashCode;
        }
    }
    class OutputObject
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public OutputObjectDefinition Definition { get; set; }
        public int ObjectParam { get; set; }
    }
    class GridContents
    {
        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MinZ { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
        public int MaxZ { get; set; }
        public string[] TilePaths { get; set; }
        public CellEntry[] Cells { get; set; }
        public OutputObject[] Objects { get; set; }
    }
    public void Save(string path)
    {
        var usedCellArray = gridMap.GetUsedCells();
        var usedCellPositions = usedCellArray.Cast<Vector3>();
        GD.Print("Used cell count: ", usedCellArray.Count);

        GridContents output = new GridContents() {
            TilePaths = new string[tilesetEntries.Count],
            Cells = new CellEntry[usedCellArray.Count],
            Objects = new OutputObject[objectsByPosition.Count]
        };

        var idToTileIndex = new Dictionary<int, int>();
        string outputFolder = System.IO.Path.GetDirectoryName(path);
        for (int i = 0; i < tilesetEntries.Count; i++)
        {
            var entry = tilesetEntries[i];
            idToTileIndex[tilesetEntryIds[entry]] = i;
            output.TilePaths[i] = PathUtils.GetRelativePath(outputFolder, entry.Path).Replace('\\','/');
        }

        GD.Print("Cell count: ", output.Cells.Length);
        int cellIndex = 0;
        foreach (Vector3 cellPos in usedCellPositions)
        {
            // GD.Print("Writing cell ", cellIndex);
            int x = (int)cellPos.x;
            int y = (int)cellPos.y;
            int z = (int)cellPos.z;
            if (x < output.MinX) output.MinX = x;
            if (y < output.MinY) output.MinY = y;
            if (z < output.MinZ) output.MinZ = z;
            if (x > output.MaxX) output.MaxX = x;
            if (y > output.MaxY) output.MaxY = y;
            if (z > output.MaxZ) output.MaxZ = z;
            int tileIndex = idToTileIndex[gridMap.GetCellItem(x, y, z)];
            int orientation = gridMap.GetCellItemOrientation(x, y, z);
            int rotation = CellContents.orientationToRotation[orientation];
            output.Cells[cellIndex] = new CellEntry() {
                X = x,
                Y = y,
                Z = z,
                TileIndex = tileIndex,
                Rotation = rotation,
            };
            cellIndex++;
        }

        GD.Print("Object count: ", output.Objects.Length);
        int objectIndex = 0;
        foreach (var curObjectPair in objectsByPosition)
        {
            var curPos = curObjectPair.Key;
            var curObject = curObjectPair.Value;
            output.Objects[objectIndex] = new OutputObject() {
                X = curPos.X,
                Y = curPos.Y,
                Z = curPos.Z,
                Definition = new OutputObjectDefinition() {
                    ObjectClass = (int)curObject.Definition.EntryClass,
                    ObjectType = (int)curObject.Definition.Type,
                    ObjectSubtype = (int)curObject.Definition.Subtype
                },
                ObjectParam = (int)curObject.Param
            };
            objectIndex++;
        }

        StreamWriter writer = new StreamWriter(path);
        string json = JsonConvert.SerializeObject(output, Formatting.Indented);
        writer.Write(json);
        writer.Close();
    }

    public bool Load(string path)
    {
        StreamReader reader = new StreamReader(path);
        string fileContents = reader.ReadToEnd();
        reader.Close();
        try
        {
            GridContents contents = JsonConvert.DeserializeObject<GridContents>(fileContents);
            this.Clear();
            string basePath = System.IO.Path.GetDirectoryName(path);
            foreach (string tilePath in contents.TilePaths)
            {
                LoadTile(System.IO.Path.Combine(basePath, tilePath), false);
            }
            foreach (CellEntry cell in contents.Cells)
            {
                gridMap.SetCellItem(cell.X, cell.Y, cell.Z, cell.TileIndex, CellContents.rotationToOrientation[cell.Rotation]);
            }
            foreach (OutputObject obj in contents.Objects)
            {
                ObjectDefinition curDefinition = OutputObjectDefinition.DefinitionMapping[obj.Definition];
                AddObjectImpl(new CellPos(obj.X, obj.Y, obj.Z), new GameObject(curDefinition, obj.ObjectParam));
            }
        }
        catch (Exception)
        {
            return false;
        }
        return true;
    }
}
//   /home/aaron/powerstruggle/assets/levels