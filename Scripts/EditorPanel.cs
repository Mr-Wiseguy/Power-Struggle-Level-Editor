using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GuiLabs.Undo;

public class EditorPanel : Panel
{
    enum EditMode
    {
        Tiles,
        Objects
    }
    private EditMode editMode;

    [Export]
    public NodePath MenuButtonsPath;
    private MenuButton fileButton;
    private MenuButton editButton;

    [Export]
    public NodePath WorldPath;
    private Spatial worldRoot;
    private Camera worldCamera;
    private Vector3 worldCameraFocus = new Vector3(0, 0, 0);
    enum WorldCameraMode
    {
        Normal = 0,
        TopDown = 1
    }
    private WorldCameraMode worldCameraMode = WorldCameraMode.Normal;
    private float worldCameraDistance = 10.0f;
    private float worldCameraYaw = 0.0f;
    [Export]
    public float WorldCameraPanSpeed = 10.0f;
    [Export]
    public float WorldCameraRotateSpeed = 10.0f;
    [Export]
    public float WorldCameraZoomDelta = 5.0f;
    [Export]
    public float WorldCameraMinDistance = 30.0f;
    [Export]
    public float WorldCameraMaxDistance = 150.0f;
    [Export]
    public float WorldCameraMinSize = 1.0f;
    [Export]
    public float WorldCameraMaxSize = 10.0f;
    [Export]
    public NodePath WorldViewportPath;
    private Viewport worldViewport;
    private ViewportContainer worldViewportContainer;
    private GridMap grid;
    private GridManager gridManager;
    private MeshInstance hoverMesh;
    private MeshInstance selectionMesh;
    [Export]
    public NodePath TilesetContainerPath;
    private VBoxContainer tilesetContainer;
    private TilesetEntryContainer selectedContainer = null;
    private ReferenceRect selectedTileRect;
    private Dictionary<TilesetEntry, TilesetEntryContainer> tilesetEntryContainers;
    [Export]
    public NodePath ImportTileButtonPath;
    private Button importTileButton;
    private FileDialog importTileDialog;
    private FileDialog saveLevelDialog;
    private FileDialog openLevelDialog;
    private string savePath = null;

    // Collision shape to use for all tiles
    private static BoxShape CubeShape;
    private static Godot.Collections.Array CubeShapeArray;
    [Export]
    public NodePath PreviewMeshInstancePath;
    [Export]
    public NodePath PreviewCameraPath;
    [Export]
    public NodePath DeleteTilesButtonPath;
    [Export]
    public NodePath DeleteObjectsButtonPath;
    private Button deleteTilesButton;
    private Button deleteObjectsButton;
    [Export]
    public NodePath HeightLabelPath;
    private Label heightLabel;
    [Export]
    public NodePath ModeLabelPath;
    private Label modeLabel;
    [Export]
    public NodePath ObjectTemplatePath;
    private Spatial ObjectTemplate;
    [Export]
    public NodePath ObjectsOptionButtonPath;
    private OptionButton objectsOptionButton;
    [Export]
    public NodePath ParamSpinBoxPath;
    private SpinBox paramSpinBox;
    static EditorPanel()
    {
        CubeShape = new BoxShape();
        CubeShape.Extents = new Vector3(0.5f, 0.5f, 0.5f);
        CubeShapeArray = new Godot.Collections.Array(new object[] { CubeShape, Transform.Identity });
        GuiLabs.Undo.ActionManager manager = new ActionManager();
    }

    private Dictionary<int, TilesetEntry> tilesetIds = new Dictionary<int, TilesetEntry>();


    enum FileMenuId {
        New,
        Open,
        Save,
        SaveAs,
    }

    private static readonly Dictionary<FileMenuId, string> FileMenuNames = new Dictionary<FileMenuId, string>()
    {
        { FileMenuId.New,    "New Level" },
        { FileMenuId.Open,   "Open Level..."},
        { FileMenuId.Save,   "Save Level"},
        { FileMenuId.SaveAs, "Save Level As..."},
    };
    private static readonly Dictionary<FileMenuId, uint> FileMenuAccelerators = new Dictionary<FileMenuId, uint>()
    {
        { FileMenuId.New,    (uint)KeyModifierMask.MaskCtrl | (uint)KeyList.N},
        { FileMenuId.Open,   (uint)KeyModifierMask.MaskCtrl | (uint)KeyList.O},
        { FileMenuId.Save,   (uint)KeyModifierMask.MaskCtrl | (uint)KeyList.S},
        { FileMenuId.SaveAs, (uint)KeyModifierMask.MaskCtrl | (uint)KeyModifierMask.MaskShift | (uint)KeyList.S}
    };
    private void OpenLevelSelected(string path)
    {
        if (gridManager.Load(path))
        {
            savePath = path;
            SelectionPlaneY = 0;
            SelectionPlaneHeight = SelectionPlaneY + 0.5f;
            selectionMesh.Visible = false;
            RebuildTilesetPanel();
            ResetCameraAngle();
            ResetCameraPosition();
        }
    }
    private void SaveLevelSelected(string path)
    {
        savePath = path;
        gridManager.Save(path);
    }
    private void FileButtonHandler(int id_int)
    {
        FileMenuId id = (FileMenuId)id_int;
        switch (id)
        {
            case FileMenuId.New:
                gridManager.Clear();
                RebuildTilesetPanel();
                SelectionPlaneY = 0;
                SelectionPlaneHeight = SelectionPlaneY + 0.5f;
                CurrentRotation = 0;
                selectionMesh.Visible = false;
                break;
            case FileMenuId.Open:
                openLevelDialog.PopupCentered(this.RectSize - new Vector2(40, 40));
                break;
            case FileMenuId.Save:
                if (savePath == null)
                {
                    saveLevelDialog.PopupCentered(this.RectSize - new Vector2(40, 40));
                }
                else
                {
                    gridManager.Save(savePath);
                }
                break;
            case FileMenuId.SaveAs:
                saveLevelDialog.PopupCentered(this.RectSize - new Vector2(40, 40));
                break;
        }
        // GD.Print("Pressed: ", FileMenuNames[id]);
    }

    enum EditMenuId {
        Undo,
        Redo,
    }
    private static readonly Dictionary<EditMenuId, string> EditMenuNames = new Dictionary<EditMenuId, string>()
    {
        { EditMenuId.Undo,   "Undo"},
        { EditMenuId.Redo,   "Redo"},
    };
    private static readonly Dictionary<EditMenuId, uint> EditMenuAccelerators = new Dictionary<EditMenuId, uint>()
    {
        { EditMenuId.Undo,   (uint)KeyModifierMask.MaskCtrl | (uint)KeyList.Z},
        { EditMenuId.Redo,   (uint)KeyModifierMask.MaskCtrl | (uint)KeyList.Y}
    };
    private static readonly Dictionary<EditMenuId, List<ShortCut>> EditMenuShortcuts = new Dictionary<EditMenuId, List<ShortCut>>()
    {
        // Redo: Ctrl+Y, Ctrl+Shift+Z
        { EditMenuId.Redo, new List<ShortCut> {
            new ShortCut() {Shortcut = new InputEventKey() {Control = true, Shift = true, Scancode = (uint)KeyList.Z}},
        }},
    };
    private void EditButtonHandler(int id_int)
    {
        switch ((EditMenuId)(id_int))
        {
            case EditMenuId.Undo:
                gridManager.Undo();
                RebuildTilesetPanel();
                break;
            case EditMenuId.Redo:
                gridManager.Redo();
                RebuildTilesetPanel();
                break;
        }
    }
    public void MoveUpPressed()
    {
        SelectionPlaneY++;
        SelectionPlaneHeight = SelectionPlaneY + 0.5f;
    }
    public void MoveDownPressed()
    {
        SelectionPlaneY--;
        SelectionPlaneHeight = SelectionPlaneY + 0.5f;
    }
    private int PositiveModulo(int numerator, int denominator)
    {
        int r = numerator % denominator;
        return r < 0 ? r + denominator : r;
    }
    public void RotateLeftPressed()
    {
        CurrentRotation = PositiveModulo(CurrentRotation + 1, 4);
    }
    public void RotateRightPressed()
    {
        CurrentRotation = PositiveModulo(CurrentRotation - 1, 4);
    }
    public void ResetCameraAnglePressed()
    {
        ResetCameraAngle();
    }
    public void SwapCameraModePressed()
    {
        worldCameraMode = (WorldCameraMode)((int)worldCameraMode ^ 1);
    }
    public void DeleteModePressed()
    {
        gridManager.DeselectTilesetEntry();
        gridManager.DeselectObjectEntry();
        previewMeshInstance.Visible = false;
        selectedTileRect.Visible = false;
        editMode = EditMode.Tiles;
        objectsOptionButton.Select(ObjectDefinition.AllEntries.Count());
    }

    private void SetupMenuButtons()
    {
        Node menuButtonsParent = GetNode(MenuButtonsPath);
        
        // File button
        fileButton = menuButtonsParent.GetNode<MenuButton>("FileButton");
        PopupMenu filePopup = fileButton.GetPopup();
        filePopup.Connect("id_pressed", this, nameof(FileButtonHandler));
        foreach (var pair in FileMenuNames)
        {
            filePopup.AddItem(pair.Value, (int)pair.Key);
            if (FileMenuAccelerators.TryGetValue(pair.Key, out uint accel))
            {
                filePopup.SetItemAccelerator((int)pair.Key, accel);
            }
        }

        // Edit button
        editButton = menuButtonsParent.GetNode<MenuButton>("EditButton");
        PopupMenu editPopup = editButton.GetPopup();
        editPopup.Connect("id_pressed", this, nameof(EditButtonHandler));
        foreach (var pair in EditMenuNames)
        {
            editPopup.AddItem(pair.Value, (int)pair.Key);
            if (EditMenuAccelerators.TryGetValue(pair.Key, out uint accel))
            {
                editPopup.SetItemAccelerator((int)pair.Key, accel);
            }
            if (EditMenuShortcuts.TryGetValue(pair.Key, out var curShortcuts))
            {
                foreach (var curShortcut in curShortcuts)
                {
                    editPopup.AddShortcut(curShortcut, (int)pair.Key);
                }
            }
        }

        // Object entry dropdown
        objectsOptionButton = GetNode<OptionButton>(ObjectsOptionButtonPath);
        foreach (var objEntry in ObjectDefinition.AllEntries)
        {
            objectsOptionButton.AddItem(objEntry.Name);
        }
        objectsOptionButton.AddItem("");
        objectsOptionButton.Select(ObjectDefinition.AllEntries.Count());

        // Save level dialog
        saveLevelDialog = GetNode<FileDialog>("SaveLevelDialog");
        saveLevelDialog.Connect("file_selected", this, nameof(SaveLevelSelected));
        openLevelDialog = GetNode<FileDialog>("OpenLevelDialog");
        openLevelDialog.Connect("file_selected", this, nameof(OpenLevelSelected));
    }
    private Dictionary<GameObject, Spatial> objectSpatials;

    private void SetupWorld()
    {
        worldRoot = GetNode<Spatial>(WorldPath);
        worldViewport = GetNode<Viewport>(WorldViewportPath); //worldRoot.GetParent<Viewport>();
        worldCamera = worldViewport.GetNode<Camera>("Camera");
        worldViewportContainer = worldViewport.GetParent<ViewportContainer>();
        grid = worldRoot.GetNode<GridMap>("GridMap");
        gridManager = new GridManager(grid);
        
        gridManager.OnObjectAdded += GridObjectAdded;
        gridManager.OnObjectRemoved += GridObjectRemoved;
        objectSpatials = new Dictionary<GameObject, Spatial>();
        ObjectTemplate = GetNode<Spatial>(ObjectTemplatePath);
        
        hoverMesh = worldRoot.GetNode<MeshInstance>("HoverMesh");
        selectionMesh = worldRoot.GetNode<MeshInstance>("SelectionMesh");
        heightLabel = GetNode<Label>(HeightLabelPath);
        modeLabel = GetNode<Label>(ModeLabelPath);
        grid.Clear();
    }

    private void GridObjectAdded(object sender, GameObject addedObject, CellPos pos)
    {
        Spatial newObjectSpatial = ObjectTemplate.Duplicate() as Spatial;
        newObjectSpatial.Translation = new Vector3(pos.X + 0.5f, pos.Y, pos.Z + 0.5f);
        worldRoot.AddChild(newObjectSpatial);
        Sprite3D newObjectNametag = newObjectSpatial.GetNode<Sprite3D>("Nametag");
        Viewport newObjectNameViewport = newObjectSpatial.GetNode<Viewport>("Viewport");
        Label newObjectNameLabel = newObjectNameViewport.GetNode<Label>("NameLabel");
        newObjectNameLabel.Text = addedObject.Definition.Name;
        newObjectNametag.Texture = newObjectNameViewport.GetTexture();
        objectSpatials.Add(addedObject, newObjectSpatial);
        newObjectSpatial.Visible = true;
    }
    private void GridObjectRemoved(object sender, GameObject deletedObject)
    {
        Spatial deletedObjectSpatial = objectSpatials[deletedObject];
        objectSpatials.Remove(deletedObject);
        deletedObjectSpatial.QueueFree();
    }

    private void ImportTilePressed()
    {
        importTileDialog.PopupCentered(this.RectSize - new Vector2(40, 40));
    }
    private void RebuildTilesetPanel()
    {
        IList<TilesetEntry> tilesetEntries = gridManager.GetTilesetEntries();
        foreach (Node child in tilesetContainer.GetChildren())
        {
            tilesetContainer.RemoveChild(child);
        }
        for (int entryIdx = 0; entryIdx < tilesetEntries.Count; entryIdx++)
        {
            TilesetEntry entry = tilesetEntries[entryIdx];
            TilesetEntryContainer container;
            if (!tilesetEntryContainers.TryGetValue(entry, out container))
            {
                container = new TilesetEntryContainer(entry, worldRoot.GetNode<WorldEnvironment>("WorldEnvironment"));
                container.OnDeletedPressed += (o) => 
                    {
                        TilesetEntryContainer localContainer = o as TilesetEntryContainer;
                        if (localContainer == selectedContainer)
                        {
                            gridManager.DeselectTilesetEntry();
                            selectedTileRect.Visible = false;
                            previewMeshInstance.Visible = false;
                        }
                        gridManager.RemoveTilesetEntry(localContainer.Entry);
                        RebuildTilesetPanel();
                    };
                container.OnSelected += (o) =>
                    {
                        TilesetEntryContainer localContainer = o as TilesetEntryContainer;
                        gridManager.SelectTilesetEntry(localContainer.Entry);
                        gridManager.DeselectObjectEntry();
                        // selectedTileRect.Visible = true;
                        // selectedTileRect.RectPosition = localContainer.RectPosition + new Vector2(1, 0);
                        // selectedTileRect.RectSize = localContainer.RectSize - new Vector2(1, 0);
                        // selectedTileRect.GetParent().RemoveChild(selectedTileRect);
                        // localContainer.AddChild(selectedTileRect);
                        // selectedContainer = localContainer;
                        // selectedTileRect.AnchorLeft = 0;
                        // selectedTileRect.AnchorTop = 0;
                        // selectedTileRect.AnchorRight = 1;
                        // selectedTileRect.AnchorBottom = 1;
                        // selectedTileRect.MarginTop = 0;
                        // selectedTileRect.MarginLeft = 1;
                        // selectedTileRect.MarginRight = 1;
                        // selectedTileRect.MarginBottom = 1;
                        previewMeshInstance.Scale = new Vector3(1.0f, 1.0f, 1.0f) / localContainer.GetCameraSize();
                        previewMeshInstance.Mesh = localContainer.Entry.TileMesh.Mesh;
                        previewMeshInstance.Visible = true;
                        editMode = EditMode.Tiles;
                    };
                tilesetEntryContainers[entry] = container;
            }
            tilesetContainer.AddChild(container);
            if (entryIdx != tilesetEntries.Count - 1)
            {
                HSeparator separator = new HSeparator();
                separator.SizeFlagsHorizontal = (int)(SizeFlags.Fill | SizeFlags.Expand);
                separator.Set("custom_constants/separation", 0);
                tilesetContainer.AddChild(separator);
            }
        }
    }


    private void TileFilesSelected(Godot.Collections.Array paths)
    {
        List<string> failedPaths = new List<string>(paths.Count);
        List<string> alreadyPresentPaths = new List<string>(paths.Count);
        List<string> zeroOrMultipleMeshPaths = new List<string>(paths.Count);
        foreach (string path in paths.Cast<string>())
        {
            var result = gridManager.LoadTile(path);
            switch (result)
            {
                case TileLoadResult.FailedToOpenFile:
                    failedPaths.Add(path);
                    break;
                case TileLoadResult.FileAlreadyPresent:
                    alreadyPresentPaths.Add(path);
                    break;
                case TileLoadResult.FileContainedZeroOrMultipleMeshes:
                    zeroOrMultipleMeshPaths.Add(path);
                    break;
            }
        }
        if (zeroOrMultipleMeshPaths.Count > 0)
        {
            // TODO error message
            GD.Print("Model ", alreadyPresentPaths.Count > 1 ? "s" : "", " contained more than 1 mesh, or no meshes at all: \n", string.Join("\n", zeroOrMultipleMeshPaths));
        }
        if (failedPaths.Count > 0)
        {
            // TODO error message
            GD.Print("Failed to open: \n", string.Join("\n", failedPaths));
        }
        if (alreadyPresentPaths.Count > 0)
        {
            // TODO warning message
            GD.Print("Model", alreadyPresentPaths.Count > 1 ? "s" : "", " already present in tileset: \n", string.Join("\n", alreadyPresentPaths));
        }
        RebuildTilesetPanel();
    }

    private void SetupTileList()
    {
        tilesetContainer = GetNode<VBoxContainer>(TilesetContainerPath);
        selectedTileRect = tilesetContainer.GetParent().GetParent().GetParent().GetNode<Panel>("ReferenceRect").GetNode<ReferenceRect>("SelectedTileRect");
        importTileButton = GetNode<Button>(ImportTileButtonPath);
        deleteTilesButton = GetNode<Button>(DeleteTilesButtonPath);
        deleteObjectsButton = GetNode<Button>(DeleteObjectsButtonPath);
        paramSpinBox = GetNode<SpinBox>(ParamSpinBoxPath);

        importTileButton.Connect("pressed", this, nameof(ImportTilePressed));

        importTileDialog = GetNode<FileDialog>("ImportTileDialog");
        importTileDialog.Connect("files_selected", this, nameof(TileFilesSelected));

        tilesetEntryContainers = new Dictionary<TilesetEntry, TilesetEntryContainer>();
        selectedTileRect.Visible = false;
    }
    private MeshInstance previewMeshInstance;
    private Camera previewCamera;
    private void SetupPreview()
    {
        previewMeshInstance = GetNode<MeshInstance>(PreviewMeshInstancePath);
        previewCamera = GetNode<Camera>(PreviewCameraPath);
        previewMeshInstance.Visible = false;
    }

    public override void _Ready()
    {
        SetupWorld();
        SetupMenuButtons();
        SetupTileList();
        SetupPreview();
        string curDir = System.IO.Directory.GetCurrentDirectory();
        openLevelDialog.CurrentDir = openLevelDialog.CurrentPath = curDir;
        saveLevelDialog.CurrentDir = saveLevelDialog.CurrentPath = curDir;
        importTileDialog.CurrentDir = importTileDialog.CurrentPath = curDir;
        
        worldViewportContainer.Connect("mouse_entered", this, nameof(ViewportMouseEntered));
        worldViewportContainer.Connect("mouse_exited", this, nameof(ViewportMouseExited));
    }

    bool inViewport = false;

    void ViewportMouseEntered()
    {
        inViewport = true;
    }
    void ViewportMouseExited()
    {
        inViewport = false;
    }

    public override void _Notification(int what)
    {
        if (what == MainLoop.NotificationWmMouseExit)
        {
            inViewport = false;
        }
    }

    float SelectionPlaneHeight = 0.5f;
    int SelectionPlaneY = 0;
    int CurrentRotation = 0;
    int GridMousePosX = 0;
    int GridMousePosZ = 0;
    int GridSelectedPosX = 0;
    int GridSelectedPosY = 0;
    int GridSelectedPosZ = 0;
    private void SetGridMousePos(Vector3 worldPos)
    {
        Vector3 gridPos = grid.WorldToMap(worldPos);
        GridMousePosX = (int)gridPos.x;
        GridMousePosZ = (int)gridPos.z;
        Vector3 cellPos = grid.MapToWorld(GridMousePosX, (int)gridPos.y, GridMousePosZ);
        hoverMesh.Visible = true;
        hoverMesh.Translation = cellPos;
    }
    private void SelectCurrentGridPos()
    {
        Vector3 cellPos = grid.MapToWorld(GridMousePosX, SelectionPlaneY, GridMousePosZ);
        // selectionMesh.Visible = true;
        selectionMesh.Translation = cellPos;
        GridSelectedPosX = GridMousePosX;
        GridSelectedPosY = SelectionPlaneY;
        GridSelectedPosZ = GridMousePosZ;
    }

    void UpdateGridMousePos()
    {
        Vector2 mousePos = worldViewport.GetMousePosition();
        // GD.Print("Camera: ", worldCamera);
        // GD.Print("Mouse pos: ", mousePos);
        Vector3 from = worldCamera.ProjectRayOrigin(mousePos);
        Vector3 dir = worldCamera.ProjectRayNormal(mousePos);
        /*var space = grid.GetWorld().DirectSpaceState;
        var hitDict = space.IntersectRay(from, from + dir * 100);
        // GD.Print("Mouse: ", mousePos, " From: ", from, " Dir: ", dir, " To: ", from + dir * 100);
        if (hitDict.Count > 0)
        {
            Vector3 hitPos = (Vector3)hitDict["position"];
            Vector3 hitNorm = (Vector3)hitDict["normal"];
            Vector3 hitAdjusted = hitPos - hitNorm * 0.05f;
            SetGridMousePos(hitAdjusted);
        }
        else */if (dir.y != SelectionPlaneHeight)
        {
            // Intersect ray with infinite plane at y=0.5
            float rayDist = (SelectionPlaneHeight - from.y)/dir.y;
            Vector3 hitPos = from + dir * rayDist;
            SetGridMousePos(hitPos);
        }
        else
        {
            hoverMesh.Visible = false;
        }
    }
    [Export]
    public Vector3 PreviewCameraOffset;
    [Export]
    public float PreviewCameraDistance;

    private void UpdatePreview()
    {
        previewCamera.Rotation = worldCamera.Rotation;
        previewCamera.Translation = previewCamera.Transform.basis.Xform(new Vector3(0.0f, 0.0f, PreviewCameraDistance)) + PreviewCameraOffset;
        previewMeshInstance.Rotation = new Vector3(0.0f, -Mathf.Pi * 2.0f / 4.0f * CurrentRotation, 0.0f);
    }
    private void ResetCameraAngle()
    {
        worldCameraYaw = 0.0f;
        worldCameraDistance = 10.0f;
    }
    private void ResetCameraPosition()
    {
        worldCameraFocus = new Vector3(0, 0, 0);
    }
    private float Map(float x, float oldMin, float oldMax, float newMin, float newMax)
    {
        return (x - oldMin) * (newMax - newMin) / (oldMax - oldMin) + (newMin);
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(float delta)
    {
        if (inViewport)
        {
            UpdateGridMousePos();
        }
        int rotateY = 0;
        int panX = 0;
        int panZ = 0;
        int zoomChange = 0;
        if (inViewport)
        {
            if (Input.IsActionPressed("RotateLeft"))
            {
                rotateY -= 1;
            }
            if (Input.IsActionPressed("RotateRight"))
            {
                rotateY += 1;
            }
            if (Input.IsActionPressed("PanLeft"))
            {
                panX -= 1;
            }
            if (Input.IsActionPressed("PanRight"))
            {
                panX += 1;
            }
            if (Input.IsActionPressed("PanForward"))
            {
                panZ -= 1;
            }
            if (Input.IsActionPressed("PanBackward"))
            {
                panZ += 1;
            }
            if (Input.IsActionJustReleased("ZoomIn"))
            {
                zoomChange -= 1;
                GD.Print("Zoom In");
            }
            if (Input.IsActionJustReleased("ZoomOut"))
            {
                zoomChange += 1;
                GD.Print("Zoom Out");
            }
        }
        worldCameraDistance += WorldCameraZoomDelta * zoomChange;
        if (worldCameraDistance < WorldCameraMinDistance)
        {
            worldCameraDistance = WorldCameraMinDistance;
        }
        if (worldCameraDistance > WorldCameraMaxDistance)
        {
            worldCameraDistance = WorldCameraMaxDistance;
        }
        worldCamera.Size = Map(worldCameraDistance, WorldCameraMinDistance, WorldCameraMaxDistance, WorldCameraMinSize, WorldCameraMaxSize);
        worldCameraYaw += WorldCameraRotateSpeed * delta * rotateY;
        switch (worldCameraMode)
        {
            case WorldCameraMode.Normal:
                worldCamera.RotationDegrees = new Vector3(-45, worldCameraYaw, 0);
                worldCamera.Projection = Camera.ProjectionEnum.Perspective;
                break;
            case WorldCameraMode.TopDown:
                worldCamera.RotationDegrees = new Vector3(-90, worldCameraYaw, 0);
                worldCamera.Projection = Camera.ProjectionEnum.Orthogonal;
                break;
        }
        worldCameraFocus.y = SelectionPlaneHeight;
        float yawRad = Mathf.Deg2Rad(worldCameraYaw);
        worldCameraFocus += WorldCameraPanSpeed * delta * new Vector3(panX, 0, panZ).Rotated(new Vector3(0, 1, 0), yawRad);
        worldCamera.Translation = worldCamera.Transform.basis.z * worldCameraDistance + worldCameraFocus;
        UpdatePreview();
        deleteTilesButton.Disabled = gridManager.GetSelectedTilesetEntry() == null && editMode == EditMode.Tiles;
        deleteObjectsButton.Disabled = gridManager.GetSelectedObjectEntry() == null && editMode == EditMode.Objects;
        heightLabel.Text = "Height: " + SelectionPlaneY.ToString();
        if (editMode == EditMode.Tiles)
        {
            modeLabel.Text = (gridManager.GetSelectedTilesetEntry() != null ? "Placing " : "Deleting ") + "Tiles";
        }
        else
        {
            modeLabel.Text = (gridManager.GetSelectedObjectEntry() != null ? "Placing " : "Deleting ") + "Objects";
        }
    }
    public override void _GuiInput(InputEvent eventInput)
    {
        if (eventInput is InputEventMouseMotion eventMouseMotion)
        {
            Vector2 mouseViewportPos = eventMouseMotion.GlobalPosition - worldViewportContainer.RectGlobalPosition;
            if (worldViewport.GetVisibleRect().HasPoint(mouseViewportPos))
            {
                inViewport = true;
            }
            if (inViewport && ((eventMouseMotion.ButtonMask & (int)ButtonList.MaskLeft) != 0))
            {
                int oldX = GridSelectedPosX;
                int oldZ = GridSelectedPosZ;
                UpdateGridMousePos();
                SelectCurrentGridPos();
                if (oldX != GridSelectedPosX || oldZ != GridSelectedPosZ)
                {
                    switch (editMode)
                    {
                        case EditMode.Tiles:
                            gridManager.SetCell(GridSelectedPosX, GridSelectedPosY, GridSelectedPosZ, CurrentRotation);
                            break;
                        case EditMode.Objects:
                            gridManager.SetCellObject(GridSelectedPosX, GridSelectedPosY, GridSelectedPosZ, (int)paramSpinBox.Value);
                            break;
                    }
                }
            }
        }
        else if (eventInput is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.Pressed == true)
            {
                if (eventMouseButton.ButtonIndex == (int)ButtonList.Left)
                {
                    if (inViewport)
                    {
                        SelectCurrentGridPos();
                        switch (editMode)
                        {
                            case EditMode.Tiles:
                                gridManager.SetCell(GridSelectedPosX, GridSelectedPosY, GridSelectedPosZ, CurrentRotation);
                                break;
                            case EditMode.Objects:
                                gridManager.SetCellObject(GridSelectedPosX, GridSelectedPosY, GridSelectedPosZ, (int)paramSpinBox.Value);
                                break;
                        }
                    }
                }
                if (eventMouseButton.ButtonIndex == (int)ButtonList.Right)
                {
                    if (inViewport)
                    {
                        SelectCurrentGridPos();
                    }
                }
            }
            else
            {
                if (eventMouseButton.ButtonIndex == (int)ButtonList.Left)
                {
                    gridManager.CommitChanges();
                }
            }
        }
    }

    public void ObjectEntrySelected(int entryIndex)
    {
        editMode = EditMode.Objects;
        gridManager.DeselectTilesetEntry();
        if (entryIndex < ObjectDefinition.AllEntries.Count())
        {
            gridManager.SelectObjectEntry(entryIndex);
        }
        else
        {
            gridManager.DeselectObjectEntry();
        }
    }
    public void DeleteObjectsPressed()
    {
        gridManager.DeselectTilesetEntry();
        gridManager.DeselectObjectEntry();
        previewMeshInstance.Visible = false;
        selectedTileRect.Visible = false;
        editMode = EditMode.Objects;
        objectsOptionButton.Select(ObjectDefinition.AllEntries.Count());
    }
}
