// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.AssetEditors.Gizmos;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Gizmos;
using Stride.Engine.Processors;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Input;
using Stride.Rendering;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;

[GizmoComponent(typeof(SplineComponent), isMainGizmo: true)]
public class SplineGizmo : BillboardingGizmo<SplineComponent>
{
    private const int SegmentLineTessellation = 16;
    private const float SegmentLineRadius = 0.025f;
    private const float GizmoDefaultSize = 48; // the default size of the gizmo on the screen in pixels.
    private static readonly Color BoundingBoxColor = Color.Orange;
    private static readonly Color SegmentLineColor = Color.Aqua;

    internal const RenderGroup GizmoRenderGroup = DefaultGroup;

    private EditorGameSplineEditorGizmoService splineEditorGizmoService;
    private IStrideEditorMouseService editorMouseService;

    private bool isSplineChangedUpdateRequired = false;

    private bool prevUpdateWasActiveSpline = false;
    private readonly List<SplineControlPointGizmo> controlPointGizmos = [];

    private SplineControlPointGizmo mouseHoverControlPointGizmo = null;
    private int mouseHoverControlPointIndex = -1;

    private Material boundingBoxMaterial;
    private Material segmentLineMaterial;
    private Entity splineVisualizerRootEntity;
    private Entity splineVisualizerBoundingBoxEntity;
    private ModelComponent splineVisualizerBoundingBoxModelComponent;
    private readonly List<SplineVisualizerSegmentEntityData> splineVisualizerSegmentEntityDataList = [];
    private readonly List<SplineSample> splineSamples = [];

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
        segmentLineMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, SegmentLineColor, 0.75f);

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

        foreach (var data in splineVisualizerSegmentEntityDataList)
        {
            data.LineModelEntity.SetParent(null);
            data.LineModelEntity.Scene = null;
            data.GeometricPrimitive.Dispose();
        }
        splineVisualizerSegmentEntityDataList.Clear();

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
            var boundingBoxMesh = GizmoBoundingBoxMesh.CreateMesh(GraphicsDevice);
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

        splineSamples.Clear();
        Component.Spline.CollectSplineSamples(splineSamples);

        // Bounding box
        splineVisualizerBoundingBoxModelComponent.Enabled = splineSamples.Count > 1;
        if (splineVisualizerBoundingBoxModelComponent.Enabled)
        {
            var boundingBox = Component.Spline.CalculateBoundingBox();
            var boxLengths = boundingBox.Maximum - boundingBox.Minimum;
            splineVisualizerBoundingBoxEntity.Transform.Scale = boxLengths;
            splineVisualizerBoundingBoxEntity.Transform.Position = boundingBox.Center;
        }

        if (splineSamples.Count == 0)
        {
            return;
        }

        int splineSegmentCount = splineSamples.Count - 1;
        if (Component.Spline.IsClosedLoop)
        {
            splineSegmentCount++;
        }

        if (splineVisualizerSegmentEntityDataList.Count < splineSegmentCount)
        {
            // Populate new segments

            int segmentStartIndex = splineVisualizerSegmentEntityDataList.Count;
            int addSegmentCount = splineSegmentCount - segmentStartIndex;
            for (int i = 0; i < addSegmentCount; i++)
            {
                int splineSegmentIndex = segmentStartIndex + i;

                // The line model
                const float LineLength = 1;     // The line's true length will be changed via the entity's scale
                var lineMesh = GizmoModelHelper.CreateLine(GraphicsDevice, LineLength, SegmentLineRadius, SegmentLineTessellation);
                var lineMeshDraw = lineMesh.ToMeshDraw();
                var lineModelComponent = new ModelComponent
                {
                    Enabled = true,
                    Model = new Model { segmentLineMaterial, new Mesh { Draw = lineMeshDraw } },
                    RenderGroup = RenderGroup,
                    IsShadowCaster = false,
                };
                var lineModelEntity = new Entity($"SplineSegment_LineModel_{splineSegmentIndex}")
                {
                    lineModelComponent
                };
                // Don't need to update line position/rotation here, wait until UpdateSplineVisualizerTransformation is called

                // Attach to root visualizer entity
                lineModelEntity.SetParent(splineVisualizerRootEntity);

                var segmentEntityData = new SplineVisualizerSegmentEntityData
                {
                    LineModelEntity = lineModelEntity,
                    LineModelComponent = lineModelComponent,
                    GeometricPrimitive = lineMesh,
                };
                splineVisualizerSegmentEntityDataList.Add(segmentEntityData);
            }
        }
        else if (splineVisualizerSegmentEntityDataList.Count > splineSegmentCount)
        {
            // Remove excess segments
            int segmentEndIndex = splineSegmentCount;
            for (int i = splineVisualizerSegmentEntityDataList.Count - 1; i >= segmentEndIndex; i--)
            {
                var data = splineVisualizerSegmentEntityDataList[i];
                data.LineModelEntity.SetParent(null);
                data.LineModelEntity.Scene = null;
                data.GeometricPrimitive.Dispose();

                splineVisualizerSegmentEntityDataList.RemoveAt(i);
            }
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

        if (isSplineChangedUpdateRequired)
        {
            UpdateSplineControlPoints();
            isSplineChangedUpdateRequired = false;
        }

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

        UpdateSplineVisualizerTransformation();
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
        // TODO Only check if camera has moved or segment placements changed
        cameraService ??= Game.EditorServices.Get<IEditorGameCameraService>();
        float targetedScale = GetTargetedScale(cameraService);

        for (int i = 0; i < splineVisualizerSegmentEntityDataList.Count; i++)
        {
            var data = splineVisualizerSegmentEntityDataList[i];
            // Make line point in the correct direction and rescale the overall mesh
            var segmentStartPos = splineSamples[i].Position;
            var nextSegmentPos = GetNextSegmentPosition(i);
            var segmentToNextSegment = nextSegmentPos - segmentStartPos;
            Quaternion lineRotation;
            if (MathUtil.IsZero(segmentToNextSegment.LengthSquared()))
            {
                lineRotation = Quaternion.Identity;
            }
            else
            {
                var upVec = Vector3.UnitY;  // TODO?
                lineRotation = Quaternion.LookRotation(forward: Vector3.Normalize(segmentToNextSegment), upVec);
            }
            data.LineModelEntity.Transform.Position = segmentStartPos;
            data.LineModelEntity.Transform.Rotation = lineRotation;
            float lineLength = segmentToNextSegment.Length();
            data.LineModelEntity.Transform.Scale = new Vector3(targetedScale, targetedScale, lineLength);
        }

        Vector3 GetNextSegmentPosition(int segmentIndex)
        {
            int nextSegmentIndex = segmentIndex + 1;
            if (nextSegmentIndex >= splineSamples.Count)
            {
                nextSegmentIndex = 0;   // Loop back
            }
            var nextSegmentPos = splineSamples[nextSegmentIndex].Position;
            return nextSegmentPos;
        }
    }

    private float GetTargetedScale(IEditorGameCameraService cameraService)
    {
        var splineEntity = Component.Entity;
        if (splineEntity is not null
            && cameraService.Component.Projection == CameraProjectionMode.Perspective)
        {
            var distanceToSelectedEntity = MathF.Abs(Vector3.TransformCoordinate(splineEntity.Transform.WorldMatrix.TranslationVector, cameraService.ViewMatrix).Z);
            return SizeFactor * DefaultScale * 2f * MathF.Tan(MathUtil.DegreesToRadians(cameraService.VerticalFieldOfView / 2)) * distanceToSelectedEntity;
        }

        return SizeFactor * DefaultScale * cameraService.Component.OrthographicSize;
    }

    private struct SplineVisualizerSegmentEntityData
    {
        public Entity LineModelEntity;
        public ModelComponent LineModelComponent;
        public GeometricPrimitive GeometricPrimitive;
    }
}
