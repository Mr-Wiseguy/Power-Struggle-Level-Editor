using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public class TilesetEntryContainer : CenterContainer
{
    private readonly Label tilenameLabel;
    private readonly ViewportContainer modelViewportContainer;
    private readonly Viewport modelViewport;
    private readonly Camera viewportCamera;
    private readonly HBoxContainer container;
    private readonly TilesetEntry entry;

    // Good number to get the ortho projection size to render something with a longest length of 1
    private static readonly float CameraScale = 1.75f;

    public TilesetEntry Entry { get => entry; }

    public TilesetEntryContainer()
    {

    }
    public TilesetEntryContainer(TilesetEntry entry, WorldEnvironment environment)
    {
        this.entry = entry;
        container = new HBoxContainer();
        container.RectMinSize = new Vector2(200, 0);
        AddChild(container);

        VBoxContainer buttonsVbox = new VBoxContainer();
        buttonsVbox.RectMinSize = new Vector2(50, 0);
        buttonsVbox.SizeFlagsHorizontal = (int)(SizeFlags.Fill);
        buttonsVbox.SizeFlagsVertical   = (int)(SizeFlags.ShrinkCenter);
        buttonsVbox.Set("custom_constants/separation", 0);
        container.AddChild(buttonsVbox);
        
        tilenameLabel = new Label();
        tilenameLabel.Text = entry.Name;
        tilenameLabel.RectMinSize = new Vector2(0, 20);
        tilenameLabel.Valign = Label.VAlign.Center;
        tilenameLabel.Align = Label.AlignEnum.Center;
        buttonsVbox.AddChild(tilenameLabel);

        Button changeModelButton = new Button();
        changeModelButton.Text = "Change Model";
        changeModelButton.RectSize = new Vector2(48, 0);
        changeModelButton.SizeFlagsHorizontal = (int)(0);
        buttonsVbox.AddChild(changeModelButton);

        Button deleteButton = new Button();
        deleteButton.Text = "Delete";
        deleteButton.RectSize = new Vector2(48, 0);
        deleteButton.SizeFlagsHorizontal = (int)(0);
        deleteButton.Connect("pressed", this, nameof(DeleteButtonPressed));
        buttonsVbox.AddChild(deleteButton);

        modelViewportContainer = new ViewportContainer();
        modelViewportContainer.RectMinSize = new Vector2(50, 50);
        modelViewportContainer.SizeFlagsHorizontal = (int)(SizeFlags.ExpandFill);
        modelViewportContainer.SizeFlagsVertical   = (int)(SizeFlags.Fill);
        modelViewportContainer.Stretch = true;
        modelViewportContainer.MouseFilter = MouseFilterEnum.Pass;
        container.AddChild(modelViewportContainer);

        modelViewport = new Viewport();
        modelViewport.OwnWorld = true;
        modelViewport.TransparentBg = true;
        modelViewport.Msaa = Viewport.MSAA.Msaa16x;
        modelViewport.AddChild(entry.TileMesh);
        modelViewportContainer.AddChild(modelViewport);

        AABB bounds = entry.TileMesh.GetAabb();
        // GD.Print("Bounds Size: ", bounds.Size, " Position: ", bounds.Position);
        Vector3 center = bounds.Size * 0.5f + bounds.Position;
        
        float longestSide = bounds.GetLongestAxisSize();
        viewportCamera = new Camera();
        viewportCamera.Projection = Camera.ProjectionEnum.Orthogonal;
        viewportCamera.Size = longestSide * CameraScale;
        viewportCamera.Transform = new Transform(new Quat(new Vector3(-Mathf.Pi / 4, Mathf.Pi / 4, 0)),
            center + new Vector3(5.0f * Mathf.Sqrt2, 10, 5.0f * Mathf.Sqrt2));
        modelViewport.AddChild(viewportCamera);

        modelViewport.AddChild(environment.Duplicate());
        modelViewport.RenderTargetUpdateMode = Viewport.UpdateMode.Once;

        // this.RectSize = container.RectSize;
        // this.RectMinSize = container.RectMinSize;
    }
    // public override void _Process(float delta)
    // {
    //     this.RectSize = container.RectSize;
    //     this.RectMinSize = container.RectMinSize;
    // }
    public float GetCameraSize()
    {
        return viewportCamera.Size;
    }
    public Transform GetCameraTransform()
    {
        return viewportCamera.Transform;
    }
    public override void _Ready()
    {
    }
    public delegate void SelectedHandler(object sender);

    public event SelectedHandler OnSelected;

    public override void _GuiInput(InputEvent eventInput)
    {
        if (eventInput is InputEventMouseButton eventMouseButton)
        {
            if (eventMouseButton.ButtonIndex == (int)ButtonList.Left && eventMouseButton.Pressed)
            {
                if (OnSelected != null)
                {
                    OnSelected(this);
                }
            }
        }
    }
    public delegate void DeletedPressedHandler(object sender);

    public event DeletedPressedHandler OnDeletedPressed;
    private void DeleteButtonPressed()
    {
        if (OnDeletedPressed != null)
        {
            OnDeletedPressed(this);
        }
    }

//  // Called every frame. 'delta' is the elapsed time since the previous frame.
//  public override void _Process(float delta)
//  {
//      
//  }
}
