// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.AssetEditors.Gizmos;
using SplineTest.GameStudioExt.StrideEditorExt;
using SplineTest.Rendering;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Gizmos;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Graphics.Meshes;
using Stride.Input;
using Stride.Rendering;
using System.Runtime.InteropServices;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;

[GizmoComponent(typeof(SplineComponent), isMainGizmo: true)]
public class SplineGizmo : BillboardingGizmo<SplineComponent>
{
    private const float GizmoDefaultSize = 48; // the default size of the gizmo on the screen in pixels.
    private static readonly Color BoundingBoxColor = Color.Orange;
    private static readonly Color CurveColor = Color.Aqua;

    internal const RenderGroup GizmoRenderGroup = DefaultGroup;

    private EditorGameSplineEditorGizmoService splineEditorGizmoService;
    private IStrideEditorMouseService editorMouseService;

    private bool isSplineChangedUpdateRequired = false;
    private Vector3 prevGizmoRootPosition = new Vector3(float.MinValue);

    private bool prevUpdateWasActiveSpline = false;
    private readonly List<SplineControlPointGizmo> controlPointGizmos = [];

    private SplineControlPointGizmo mouseHoverControlPointGizmo = null;
    private int mouseHoverControlPointIndex = -1;

    private Material boundingBoxMaterial;
    private Entity splineVisualizerRootEntity;
    private Entity splineVisualizerBoundingBoxEntity;
    private ModelComponent splineVisualizerBoundingBoxModelComponent;
    private readonly List<Vector3> splineSamplePoints = [];

    private readonly List<IDisposable> disposables = [];

    internal Spline Spline => Component.Spline;

    /// <summary>
    /// Gets the gizmo default scale in ratio of screen height ( 1 => full screen vertically )
    /// </summary>
    public float DefaultScale => GizmoDefaultSize / GraphicsDevice.Presenter.BackBuffer.Height;

    public SplineGizmo(EntityComponent component)
        : base(component, gizmoName: "Spline", GizmoResources.SplineGizmo)
    {
        RenderGroup = GizmoRenderGroup;     // Must keep this on DefaultGroup to prevent editor entity selection overriding our selection
    }

    public override void Initialize(IServiceRegistry services, Scene editorScene)
    {
        base.Initialize(services, editorScene);

        boundingBoxMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, BoundingBoxColor, 0.75f);

        splineEditorGizmoService = Game.EditorServices.Get<EditorGameSplineEditorGizmoService>();
        editorMouseService = StrideEditorMouseService.GetOrCreate(Game.Services);

        Component.SplinePropertyChanged += OnSplineChanged;
        Component.ControlPointsChanged += OnSplineChanged;
        OnSplineChanged(Component);
    }

    protected override void Destroy()
    {
        for (int i = 0; i < controlPointGizmos.Count; i++)
        {
            var controlPointGizmo = controlPointGizmos[i];
            controlPointGizmo.IsEnabled = false;
            controlPointGizmo.GizmoRootEntity?.SetParent(null);
            controlPointGizmo.Dispose();
        }
        controlPointGizmos.Clear();
        Component.SplinePropertyChanged -= OnSplineChanged;
        Component.ControlPointsChanged -= OnSplineChanged;

        foreach (var disp in disposables)
        {
            disp.Dispose();
        }
        disposables.Clear();

        base.Destroy();
    }

    private void OnSplineChanged(SplineComponent splineComponent)
    {
        isSplineChangedUpdateRequired = true;   // Enqueue change on next update
    }

    private void UpdateSplineControlPoints()
    {
        if (controlPointGizmos.Count < Component.Spline.Count)
        {
            // Populate new gizmos
            int controlPointStartIndex = controlPointGizmos.Count;
            int addControlPointCount = Component.Spline.Count - controlPointStartIndex;
            for (int i = 0; i < addControlPointCount; i++)
            {
                var controlPointGizmo = new SplineControlPointGizmo(controlPointStartIndex + i, Component);
                controlPointGizmo.Initialize(Services, EditorScene);
                controlPointGizmos.Add(controlPointGizmo);
                controlPointGizmo.IsEnabled = Component == splineEditorGizmoService.ActiveSplineComponent;
                if (controlPointGizmo.IsEnabled)
                {
                    controlPointGizmo.GizmoRootEntity?.SetParent(GizmoRootEntity);
                }
            }
        }
        else if (controlPointGizmos.Count > Component.Spline.Count)
        {
            // Remove excess gizmos
            int controlPointEndIndex = Component.Spline.Count;
            for (int i = controlPointGizmos.Count - 1; i >= controlPointEndIndex; i--)
            {
                var controlPointGizmo = controlPointGizmos[i];
                controlPointGizmo.IsEnabled = false;
                controlPointGizmo.GizmoRootEntity?.SetParent(null);
                controlPointGizmo.Dispose();
                if (mouseHoverControlPointGizmo == controlPointGizmo)
                {
                    mouseHoverControlPointGizmo = null;
                }

                controlPointGizmos.RemoveAt(i);
            }
        }
        RegenerateSplineVisualizer();
    }

    private void RegenerateSplineVisualizer()
    {
        if (splineVisualizerRootEntity is null)
        {
            splineVisualizerRootEntity = new Entity("SplineVisualizer");
            splineVisualizerRootEntity.SetParent(GizmoRootEntity);

            // Add bounding box entity
            var boundingBoxMesh = BoundingBoxMesh.CreateMesh(GraphicsDevice);
            disposables.Add(boundingBoxMesh);
            var boundingBoxMeshDraw = boundingBoxMesh.ToMeshDraw();
            splineVisualizerBoundingBoxModelComponent = new ModelComponent
            {
                Enabled = true,
                Model = new Model { boundingBoxMaterial, new Mesh { Draw = boundingBoxMeshDraw } },
                RenderGroup = RenderGroup,
                IsShadowCaster = false,
            };
            splineVisualizerBoundingBoxEntity = new Entity("SplineBoundingBox")
            {
                splineVisualizerBoundingBoxModelComponent
            };
            splineVisualizerBoundingBoxEntity.SetParent(splineVisualizerRootEntity);
        }

        splineSamplePoints.Clear();
        SplineExtensions.CollectSplineSamplePoints(Component.Spline, splineSamplePoints, sampleResolutionPerCurve: 64);

        // Bounding box
        splineVisualizerBoundingBoxModelComponent.Enabled = splineSamplePoints.Count > 1;
        if (splineVisualizerBoundingBoxModelComponent.Enabled)
        {
            var boundingBox = Component.Spline.CalculateBoundingBox();
            var boxLengths = boundingBox.Maximum - boundingBox.Minimum;
            splineVisualizerBoundingBoxEntity.Transform.Scale = boxLengths;
            splineVisualizerBoundingBoxEntity.Transform.Position = boundingBox.Center;
        }
    }

    public override bool HandlesComponentId(OpaqueComponentId pickedComponentId, out Entity selection)
    {
        if (base.HandlesComponentId(pickedComponentId, out selection))
        {
            return true;
        }
        for (int i = 0; i < controlPointGizmos.Count; i++)
        {
            var controlPointGizmo = controlPointGizmos[i];
            if (controlPointGizmo.HandlesComponentId(pickedComponentId, out selection))
            {
                return true;
            }
        }
        return false;
    }

    public override void Update()
    {
        base.Update();
        if (ContentEntity is null || GizmoRootEntity is null)
        {
            return;
        }

        bool wasSplineChanged = isSplineChangedUpdateRequired;
        if (isSplineChangedUpdateRequired)
        {
            UpdateSplineControlPoints();
            isSplineChangedUpdateRequired = false;
        }
        wasSplineChanged  = wasSplineChanged  || prevGizmoRootPosition != GizmoRootEntity.Transform.Position;
        prevGizmoRootPosition = GizmoRootEntity.Transform.Position;

        if (Component == splineEditorGizmoService.ActiveSplineComponent)
        {
            if (!prevUpdateWasActiveSpline)
            {
                // Enable control point gizmos
                for (int i = 0; i < controlPointGizmos.Count; i++)
                {
                    var controlPointGizmo = controlPointGizmos[i];
                    controlPointGizmo.IsEnabled = true;
                    if (controlPointGizmo.IsEnabled)
                    {
                        controlPointGizmo.GizmoRootEntity?.SetParent(GizmoRootEntity);
                    }
                }
            }
            UpdateMouseAction();

            for (int i = 0; i < controlPointGizmos.Count; i++)
            {
                var controlPointGizmo = controlPointGizmos[i];
                controlPointGizmo.Update();
            }
            prevUpdateWasActiveSpline = true;
        }
        else if (prevUpdateWasActiveSpline)
        {
            // Disable control point gizmos
            for (int i = 0; i < controlPointGizmos.Count; i++)
            {
                var controlPointGizmo = controlPointGizmos[i];
                controlPointGizmo.IsEnabled = false;
                controlPointGizmo.GizmoRootEntity?.SetParent(null);
            }
            prevUpdateWasActiveSpline = false;
        }

        if (wasSplineChanged)
        {
            UpdateSplineVisualizerTransformation();
        }
    }

    private IEditorGameComponentGizmoService gizmoService;
    private IEditorGameCameraService cameraService;
    private void UpdateMouseAction()
    {
        // Lazy get camera service for TryGetMouseRay
        cameraService ??= Game.EditorServices.Get<IEditorGameCameraService>();

        bool isAltKeyDown = Input.IsKeyDown(Keys.LeftAlt) || Input.IsKeyDown(Keys.RightAlt);
        bool isCtrlKeyDown = Input.IsKeyDown(Keys.LeftCtrl) || Input.IsKeyDown(Keys.RightCtrl);
        if (!Input.IsMouseButtonDown(MouseButton.Left))
        {
            SplineControlPointGizmo raycastHitControlPointGizmo = null;
            int raycastHitControlPointIndex = -1;
            var raycastHitControlPointEditingSelectionType = SplineControlPointEditingSelectionType.None;
            if (TryGetMouseRay(out var mouseRay))
            {
                var raycastFilterFlags = SplineControlPointRaycastFilterFlags.All;
                if (isAltKeyDown)
                {
                    raycastFilterFlags = SplineControlPointRaycastFilterFlags.ControlPoint;
                }
                else if (isCtrlKeyDown)
                {
                    raycastFilterFlags = SplineControlPointRaycastFilterFlags.Tangents;
                }
                bool raycastOnControlPointOnly = isAltKeyDown;
                float minHitDistance = float.PositiveInfinity;
                for (int i = 0; i < controlPointGizmos.Count; i++)
                {
                    var controlPointGizmo = controlPointGizmos[i];
                    if (controlPointGizmo.TryRaycastOnHandle(mouseRay, raycastFilterFlags, ref minHitDistance, ref raycastHitControlPointEditingSelectionType))
                    {
                        raycastHitControlPointGizmo = controlPointGizmo;
                        raycastHitControlPointIndex = i;
                    }
                }
            }

            if (mouseHoverControlPointGizmo != raycastHitControlPointGizmo)
            {
                mouseHoverControlPointGizmo?.EditingSelectionType = SplineControlPointEditingSelectionType.None;

                mouseHoverControlPointGizmo = raycastHitControlPointGizmo;
                mouseHoverControlPointIndex = raycastHitControlPointIndex;

                mouseHoverControlPointGizmo?.EditingSelectionType = raycastHitControlPointEditingSelectionType;
            }
        }

        bool isShiftKeyDown = Input.IsKeyDown(Keys.LeftShift) || Input.IsKeyDown(Keys.RightShift);
        bool canControlMouse = splineEditorGizmoService.ActiveSplineComponent == Component
            && editorMouseService.IsMouseAvailable;
        bool isControllingMouse = canControlMouse && (isShiftKeyDown || isAltKeyDown || isCtrlKeyDown);

        if (canControlMouse && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isShiftKeyDown)
            {
                if (TryGetMouseRay(out var mouseRay))
                {
                    // Plane = Ax + By + Cz + D = 0 => Positive Height = Negative D
                    Plane plane;
                    if (Spline.Count == 0)
                    {
                        // No existing control points, so just raycast on XZ plane where height is the same as the spline
                        plane = new Plane(Vector3.UnitY, d: -Component.Entity.Transform.Position.Y);
                    }
                    else
                    {
                        // Raycast on XZ plane where height is the same as the last control point's height
                        var controlPoint = Spline[Spline.Count - 1];
                        plane = new Plane(Vector3.UnitY, d: -controlPoint.Position.Y);
                    }
                    if (CollisionHelper.RayIntersectsPlane(in mouseRay, in plane, out Vector3 hitPoint))
                    {
                        splineEditorGizmoService.AddControlPoint(hitPoint);
                    }
                    isControllingMouse = true;
                }
            }
            else if (mouseHoverControlPointGizmo is not null)
            {
                splineEditorGizmoService.ActivateSplineControlPointEditing(Component, mouseHoverControlPointIndex, mouseHoverControlPointGizmo.EditingSelectionType);
                isControllingMouse = true;
            }
        }

        editorMouseService.SetIsControllingMouse(isControllingMouse, owner: this);
        System.Diagnostics.Debug.WriteLineIf(isControllingMouse, $"{GetType().Name} isControllingMouse");
    }

    private bool TryGetMouseRay(out Ray mouseRay)
    {
        // Calculate the ray in the gizmo space
        var gizmoMatrix = GizmoRootEntity.Transform.WorldMatrix;
        var gizmoViewInverse = Matrix.Invert(gizmoMatrix * cameraService.ViewMatrix);

        // Check if the inverted View Matrix is valid (since it will be use for mouse picking, check the translation vector only)
        if (float.IsNaN(gizmoViewInverse.TranslationVector.X)
            || float.IsNaN(gizmoViewInverse.TranslationVector.Y)
            || float.IsNaN(gizmoViewInverse.TranslationVector.Z))
        {
            mouseRay = default;
            return false;
        }

        mouseRay = EditorGameHelper.CalculateRayFromMousePosition(cameraService.Component, Input.MousePosition, gizmoViewInverse);
        return true;
    }


    private void UpdateSplineVisualizerTransformation()
    {
        var lineVisualizerComponent = splineVisualizerRootEntity.Get<LineVisualizerComponent>();
        if (lineVisualizerComponent is null)
        {
            lineVisualizerComponent = new LineVisualizerComponent();
            splineVisualizerRootEntity.Add(lineVisualizerComponent);
        }
        else
        {
            lineVisualizerComponent.LineSet.Segments.Clear();
        }

        var splineSamplePointsSpan = CollectionsMarshal.AsSpan(splineSamplePoints);
        for (int i = 0; i < splineSamplePointsSpan.Length - 1; i++)
        {
            var lineStartPos = splineSamplePointsSpan[i];
            var lineNextPos = splineSamplePointsSpan[i + 1];

            var instData = new LineSegment
            {
                StartPosition = lineStartPos,
                EndPosition = lineNextPos,
                StartColor = CurveColor.ToColor4(),
                EndColor = CurveColor.ToColor4(),
                LineThicknessPx = 3
            };
            lineVisualizerComponent.LineSet.Segments.Add(instData);
        }
    }
}
