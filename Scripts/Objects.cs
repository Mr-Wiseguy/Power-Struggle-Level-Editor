using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using GuiLabs.Undo;

public enum ObjectClass
{
    Enemy = 0,
    Interactable = 1,
}
public enum EnemyType
{
    Shot, // e.g. Grease-E
    Slash, // e.g. Mend-E
    Spinner, // e.g. Harv-E
    Ram, // e.g. Till-R
    Bomb, // e.g. Herb-E
    Beam, // e.g. Zap-E
    Multishot, // e.g. Gas-E
    Jet, // e.g. Wave-E
    Stab, // e.g. Drill-R
    Slam, // e.g. Smash-O
    Mortar, // e.g. Blast-E
    Flame, // e.g. Heat-O
}
public enum InteractableType
{
    LoadTrigger,
    Key,
    Door
}
public class ObjectDefinition
{
    public ObjectClass EntryClass;
    public int Type;
    public int Subtype;
    public string Name;
    public ObjectDefinition(ObjectClass entryClass, EnemyType type, int subtype, string name)
    {
        EntryClass = entryClass;
        Type = (int)type;
        Subtype = subtype;
        Name = name;
    }
    public ObjectDefinition(ObjectClass entryClass, InteractableType type, int subtype, string name)
    {
        EntryClass = entryClass;
        Type = (int)type;
        Subtype = subtype;
        Name = name;
    }
    public ObjectDefinition(ObjectClass entryClass, int type, int subtype, string name)
    {
        EntryClass = entryClass;
        Type = type;
        Subtype = subtype;
        Name = name;
    }

    public static readonly ObjectDefinition[] AllEntries = {
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Shot, 0, "Grease-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Shot, 1, "Slick-O"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Shot, 2, "Peat-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Shot, 3, "Steam-O"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Slash, 0, "Mend-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Slash, 1, "Serv-O"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Spinner, 0, "Harv-E"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Ram, 0, "Till-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Bomb, 0, "Herb-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Bomb, 1, "Pest-O"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Beam, 0, "Zap-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Beam, 1, "Shock-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Multishot, 0, "Gas-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Multishot, 1, "Fume-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Jet, 0, "Wave-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Jet, 1, "Tide-O"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Stab, 0, "Drill-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Slam, 0, "Smash-O"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Slam, 1, "Mash-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Flame, 0, "Heat-O"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Flame, 1, "Burn-R"),

        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Mortar, 0, "Blast-E"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Mortar, 1, "Burst-O"),
        new ObjectDefinition(ObjectClass.Enemy, EnemyType.Mortar, 2, "Boom-R"),
        
        new ObjectDefinition(ObjectClass.Interactable, InteractableType.LoadTrigger, 0, "Load Trigger"),
        new ObjectDefinition(ObjectClass.Interactable, InteractableType.Key, 0, "Key"),
        new ObjectDefinition(ObjectClass.Interactable, InteractableType.Door, 0, "Door"),
    };
}
public class GameObject
{
    public ObjectDefinition Definition;
    public int Param;

    public GameObject(ObjectDefinition entry, int param)
    {
        Definition = entry;
        Param = param;
    }
}
partial class GridManager
{
    public Dictionary<CellPos, GameObject> objectsByPosition;
    public delegate void ObjectRemovedHandler(object sender, GameObject gameObject);
    public delegate void ObjectAddedHandler(object sender, GameObject gameObject, CellPos pos);

    public event ObjectRemovedHandler OnObjectRemoved;
    public event ObjectAddedHandler OnObjectAdded;
    private ObjectDefinition selectedObjectEntry = null;
    public ObjectDefinition GetSelectedObjectEntry()
    {
        return selectedObjectEntry;
    }
    public void SelectObjectEntry(int entryIndex)
    {
        selectedObjectEntry = ObjectDefinition.AllEntries[entryIndex];
    }
    public void DeselectObjectEntry()
    {
        selectedObjectEntry = null;
    }
    void RemoveObjectImpl(CellPos pos)
    {
        if (objectsByPosition.TryGetValue(pos, out GameObject removed))
        {
            if (OnObjectRemoved != null)
            {
                OnObjectRemoved(this, removed);
            }
            objectsByPosition.Remove(pos);
        }
    }
    void AddObjectImpl(CellPos pos, GameObject newObject)
    {  
        if (OnObjectAdded != null)
        {
            OnObjectAdded(this, newObject, pos);
        }
        objectsByPosition.Add(pos, newObject);
    }
    class SetObjectAction : AbstractAction
    {
        private GameObject newObject;
        private GameObject oldObject;
        private CellPos pos;
        private GridManager manager;
        public SetObjectAction(GridManager manager, ObjectDefinition entry, CellPos pos, int param)
        {
            this.manager = manager;
            this.pos = pos;
            this.newObject = new GameObject(entry, param);
            this.oldObject = null;
        }
        protected override void ExecuteCore()
        {
            if (manager.objectsByPosition.TryGetValue(pos, out oldObject))
            {
                manager.RemoveObjectImpl(pos);
            }
            else
            {
                oldObject = null;
            }
            if (newObject.Definition != null)
            {
                manager.AddObjectImpl(pos, newObject);
            }
            else
            {
                manager.RemoveObjectImpl(pos);
            }
        }

        protected override void UnExecuteCore()
        {
            if (newObject.Definition != null)
            {
                manager.RemoveObjectImpl(pos);
            }
            if (oldObject != null)
            {
                manager.AddObjectImpl(pos, oldObject);
            }
        }
    }

    class RemoveObjectAction : AbstractAction
    {
        private GameObject oldObject;
        private CellPos pos;
        private GridManager manager;
        public RemoveObjectAction(GridManager manager, CellPos pos)
        {
            this.manager = manager;
            this.pos = pos;
            this.oldObject = null;
        }
        protected override void ExecuteCore()
        {
            if (manager.objectsByPosition.TryGetValue(pos, out oldObject))
            {
                manager.RemoveObjectImpl(pos);
            }
            else
            {
                oldObject = null;
            }
        }

        protected override void UnExecuteCore()
        {
            if (oldObject != null)
            {
                manager.AddObjectImpl(pos, oldObject);
            }
        }
    }
}
