using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using GuiLabs.Undo;

partial class GridManager
{
    private GridMap gridMap;
    private MeshLibrary tileLibrary;
    private ActionManager gridActionManager;
    private Transaction currentTransaction;
    private List<TilesetEntry> tilesetEntries;
    private Dictionary<string, TilesetEntry> tilesetEntriesByPath;
    private Dictionary<int, TilesetEntry> tilesetEntriesById;
    private Dictionary<TilesetEntry, int> tilesetEntryIds;
    private int selectedTilesetId = -1;
    private TilesetEntry selectedTilesetEntry = null;

    public GridManager(GridMap map)
    {
        gridMap = map;
        if (map.MeshLibrary == null)
        {
            map.MeshLibrary = new MeshLibrary();
        }
        tileLibrary = map.MeshLibrary;
        gridActionManager = new ActionManager();
        tilesetEntries = new List<TilesetEntry>();
        tilesetEntriesByPath = new Dictionary<string, TilesetEntry>();
        tilesetEntriesById = new Dictionary<int, TilesetEntry>();
        tilesetEntryIds = new Dictionary<TilesetEntry, int>();
        objectsByPosition = new Dictionary<CellPos, GameObject>();
        InitTileLoader();
    }
    public void Clear()
    {
        gridMap.Clear();
        tileLibrary.Clear();
        if (currentTransaction != null)
        {
            currentTransaction.Dispose();
            currentTransaction = null;
        }
        gridActionManager.Clear();
        tilesetEntries.Clear();
        tilesetEntriesByPath.Clear();
        tilesetEntriesById.Clear();
        tilesetEntryIds.Clear();
        selectedTilesetId = -1;
        selectedTilesetEntry = null;
        selectedObjectEntry = null;
        foreach (var obj in objectsByPosition)
        {
            if (OnObjectRemoved != null)
            {
                OnObjectRemoved(this, obj.Value);
            }
        }
        objectsByPosition.Clear();
    }
    public void SetCell(int x, int y, int z, int rotation)
    {
        if (currentTransaction == null)
        {
            currentTransaction = Transaction.Create(gridActionManager, false);
        }
        gridActionManager.RecordAction(new ChangeCellAction(this, new CellPos(x, y, z), new CellContents(selectedTilesetId, rotation)));
    }
    public void SetCellObject(int x, int y, int z, int param)
    {
        if (currentTransaction == null)
        {
            currentTransaction = Transaction.Create(gridActionManager, false);
        }
        gridActionManager.RecordAction(new SetObjectAction(this, selectedObjectEntry, new CellPos(x, y, z), param));
    }
    public void CommitChanges()
    {
        if (currentTransaction != null)
        {
            currentTransaction.Dispose();
            currentTransaction = null;
        }
    }
    public void Redo()
    {
        gridActionManager.Redo();
    }
    public void Undo()
    {
        gridActionManager.Undo();
    }
    public bool CanRedo()
    {
        return gridActionManager.CanRedo;
    }
    public bool CanUndo()
    {
        return gridActionManager.CanUndo;
    }
    
    private CellContents ReplaceCell(CellPos pos, CellContents contents)
    {
        int oldId = gridMap.GetCellItem(pos.X, pos.Y, pos.Z);
        int oldOrientation = gridMap.GetCellItemOrientation(pos.X, pos.Y, pos.Z);
        int oldRotation = CellContents.orientationToRotation[oldOrientation];
        CellContents oldContents = new CellContents(oldId, oldRotation);
        gridMap.SetCellItem(pos.X, pos.Y, pos.Z, contents.Id, CellContents.rotationToOrientation[contents.Rotation]);
        return oldContents;
    }

    public void SelectTilesetEntry(TilesetEntry toSelect)
    {
        selectedTilesetEntry = toSelect;
        selectedTilesetId = tilesetEntryIds[toSelect];
    }
    public void DeselectTilesetEntry()
    {
        selectedTilesetEntry = null;
        selectedTilesetId = -1;
    }
    public TilesetEntry GetSelectedTilesetEntry()
    {
        return selectedTilesetEntry;
    }
    private void AddTilesetEntryImpl(TilesetEntry toAdd, int entryIndex, int id)
    {
        tileLibrary.CreateItem(id);
        tileLibrary.SetItemMesh(id, toAdd.TileMesh.Mesh);
        tileLibrary.SetItemName(id, toAdd.Name);
        tilesetEntries.Insert(entryIndex, toAdd);
        tilesetEntriesByPath[toAdd.Path] = toAdd;
        tilesetEntryIds[toAdd] = id;
    }

    private bool HasTileWithPath(string path)
    {
        return tilesetEntriesByPath.ContainsKey(path);
    }

    public void AddTilesetEntry(TilesetEntry toAdd)
    {
        // Commit any pending grid changes
        CommitChanges();
        gridActionManager.RecordAction(new AddTilesetEntryAction(toAdd, this));
    }
    private void RemoveTilesetEntryImpl(TilesetEntry toRemove, int entryIndex, int id)
    {
        tileLibrary.RemoveItem(id);
        tilesetEntries.RemoveAt(entryIndex);
        tilesetEntryIds.Remove(toRemove);
        tilesetEntriesByPath.Remove(toRemove.Path);
    }

    public void RemoveTilesetEntry(TilesetEntry toRemove)
    {
        // Commit any pending grid changes
        CommitChanges();
        using (Transaction.Create(gridActionManager, true))
        {
            var usedCells = gridMap.GetUsedCells().Cast<Vector3>();
            foreach (Vector3 cell in usedCells)
            {
                int x = (int)cell.x;
                int y = (int)cell.y;
                int z = (int)cell.z;
                if (gridMap.GetCellItem(x, y, z) == tilesetEntryIds[toRemove])
                {
                    gridActionManager.RecordAction(new ChangeCellAction(this, new CellPos(x, y, z), new CellContents(-1, 0)));
                }
            }
            gridActionManager.RecordAction(new RemoveTilesetEntryAction(toRemove, this));
        }
    }

    public IList<TilesetEntry> GetTilesetEntries()
    {
        return tilesetEntries.AsReadOnly();
    }
}