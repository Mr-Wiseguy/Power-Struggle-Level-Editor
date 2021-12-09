using System.Collections.Generic;
using Godot;
using GuiLabs.Undo;

public struct CellPos
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public CellPos(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }
}
class CellContents
{
    public static readonly List<int> rotationToOrientation = new List<int>() {
        0, 22, 10, 16
    };
    public static readonly Dictionary<int, int> orientationToRotation = new Dictionary<int, int>() {
        { -1, 0 },
        { 0,  0 },
        { 22, 1 },
        { 10, 2 },
        { 16, 3 },
    };
    public readonly int Id;
    public readonly int Rotation;
    public static readonly CellContents Empty = new CellContents(-1, 0);

    public CellContents(int id, int rotation)
    {
        Id = id;
        Rotation = rotation;
    }
}
partial class GridManager 
{
    class ChangeCellAction : AbstractAction
    {
        protected GridManager manager;
        protected CellPos pos;
        private CellContents oldCell, newCell;
        public ChangeCellAction(GridManager manager, CellPos pos, CellContents newCell)
        {
            this.manager = manager;
            this.pos = pos;
            this.newCell = newCell;
        }

        protected override void ExecuteCore()
        {
            oldCell = manager.ReplaceCell(pos, newCell);
        }

        protected override void UnExecuteCore()
        {
            manager.ReplaceCell(pos, oldCell);
        }
    }
}
