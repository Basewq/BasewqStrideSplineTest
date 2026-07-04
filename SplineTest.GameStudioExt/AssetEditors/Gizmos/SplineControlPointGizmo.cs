// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Graphics.GeometricPrimitives;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;

public enum SplineControlPointEditingSelectionType
{
    None,
    ControlPoint,
    TangentIn,
    TangentOut
}

[Flags]
public enum SplineControlPointRaycastFilterFlags
{
    None = 0,
    ControlPoint = 1 << 0,
    Tangents = 1 << 1,
    All = ControlPoint | Tangents
}

//[GizmoComponent(typeof(SplineComponent), isMainGizmo: false)]
public class SplineControlPointGizmo : GizmoBase
{
    private const float GizmoDefaultSize = 48; // the default size of the gizmo on the screen in pixels.

    private static readonly Color4 ControlPointFillColor = Color.White.ToColor4();
    private static readonly Color4 ControlPointOrientationLineColor = Color.Aqua.ToColor4();
    private static readonly Color4 ControlPointNormalLineColor = Color.Aqua.ToColor4();
    private static readonly Color4 TangentInHandleFillColor = Color.LightCoral.ToColor4();
    private static readonly Color4 TangentOutHandleFillColor = Color.LightGreen.ToColor4();
    private static readonly Color4 TangentLineColor = Color.WhiteSmoke.ToColor4();
    private static readonly Color4 SelectedFillColor = Color.Gold.ToColor4();
    private static readonly Color4 SelectedLineColor = Color.Gold.ToColor4();

    private readonly Dictionary<int, ModelComponent> controlPointGizmoModelComponents = [];

    private IEditorGameCameraService cameraService;

    private int controlPointIndex;
    private SplineComponent splineComponent;
    private Entity controlPointRootGizmoEntity;
    private LineVisualizerComponent controlPointLineVisualizerComponent;
    private GizmoMarkerSetComponent controlPointGizmoMarkerSetComponent;
    private bool isMarkersAndLinesUpdateRequired = true;
    private TangentEntityData tangentInEntityData;
    private TangentEntityData tangentOutEntityData;

    private readonly List<GeometricPrimitive> disposableGeometricPrimitives = [];

    /// <summary>
    /// Gets the gizmo default scale in ratio of screen height ( 1 => full screen vertically )
    /// </summary>
    public float DefaultScale => GizmoDefaultSize / GraphicsDevice.Presenter.BackBuffer.Height;

    public SplineControlPointGizmo(int controlPointIndex, SplineComponent splineComponent) : base()
    {
        RenderGroup = SplineGizmo.GizmoRenderGroup;

        this.controlPointIndex = controlPointIndex;
        this.splineComponent = splineComponent;
    }

    private SplineControlPointEditingSelectionType previousEditingSelectionType;
    public SplineControlPointEditingSelectionType EditingSelectionType { get; set; }

    protected override Entity Create()
    {
        controlPointRootGizmoEntity = new Entity($"SplineControlPoint_{controlPointIndex}");
        controlPointLineVisualizerComponent = new LineVisualizerComponent
        {
            RenderGroup = RenderGroup,
            DepthTest = false,
        };
        controlPointRootGizmoEntity.Add(controlPointLineVisualizerComponent);
        controlPointGizmoMarkerSetComponent = new GizmoMarkerSetComponent
        {
            RenderGroup = RenderGroup,
            DepthTest = false,
        };
        controlPointRootGizmoEntity.Add(controlPointGizmoMarkerSetComponent);

        var controlPointModelEntity = new Entity($"SplineControlPoint_Model_{controlPointIndex}");
        controlPointModelEntity.SetParent(controlPointRootGizmoEntity);

        tangentInEntityData = new TangentEntityData
        {
            CurrentLocalPosition = new Vector3(float.MinValue)
        };
        tangentOutEntityData = new TangentEntityData
        {
            CurrentLocalPosition = new Vector3(float.MinValue)
        };

        return controlPointRootGizmoEntity;
    }

    public override void Initialize(IServiceRegistry services, Scene editorScene)
    {
        base.Initialize(services, editorScene);

        CollectComponentIds(controlPointRootGizmoEntity);
    }

    private void CollectComponentIds(Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is ModelComponent modelComponent)
            {
                var id = RuntimeIdHelper.ToRuntimeId(modelComponent);
                controlPointGizmoModelComponents.Add(id, modelComponent);
            }
        }

        foreach (var child in entity.GetChildren())
        {
            CollectComponentIds(child);
        }
    }

    protected override void Destroy()
    {
        base.Destroy();

        foreach (var primitive in disposableGeometricPrimitives)
        {
            primitive.Dispose();
        }
        disposableGeometricPrimitives.Clear();
    }

    public override bool HandlesComponentId(OpaqueComponentId pickedComponentId, out Entity selection)
    {
        bool isMatch = pickedComponentId.Match(controlPointGizmoModelComponents, out _);
        if (isMatch)
        {
            selection = splineComponent.Entity;
            return true;
        }
        else
        {
            selection = null;
            return false;
        }
    }

    public void Update()
    {
        var splineEntity = splineComponent.Entity;
        var spline = splineComponent.Spline;
        if (splineEntity is null || controlPointIndex >= spline.Count || spline.Count == 0)
        {
            return;
        }

        var controlPoint = spline[controlPointIndex];

        controlPointRootGizmoEntity.Transform.Position = controlPoint.Position;

        cameraService ??= Game.EditorServices.Get<IEditorGameCameraService>();
        float targetedScale = GetTargetedScale(cameraService);

        var upVec = splineEntity.Transform.WorldMatrix.Up;
        UpdateTangentTransformation(ref tangentInEntityData, controlPoint, controlPoint.TangentInPosition, upVec);
        UpdateTangentTransformation(ref tangentOutEntityData, controlPoint, controlPoint.TangentOutPosition, upVec);

        // Check if updating color
        if (previousEditingSelectionType != EditingSelectionType)
        {
            isMarkersAndLinesUpdateRequired = true;
            previousEditingSelectionType = EditingSelectionType;
        }

        if (isMarkersAndLinesUpdateRequired)
        {
            const float ShapeSize = 13;
            const float OutlineWidthPx = 1f;
            const float ControlPointUpVectorThicknessPx = 2;
            const float TangentLineThicknessPx = 3;
            const float SelectedGlowWidthPx = 5;

            var markerSet = controlPointGizmoMarkerSetComponent.GizmoMarkerSet;
            markerSet.Markers.Clear();
            var ctrlPointMarker = new GizmoMarkerData
            {
                Shape = GizmoMarkerShape.Circle,
                OrientationMode = GizmoMarkerOrientationMode.Billboard,
                ScaleMode = GizmoMarkerScaleMode.FixedScreenSize,
                Position = Vector3.Zero,
                FillColor = ControlPointFillColor,
                SizePx = new Vector2(ShapeSize),
                OutlineWidthPx = OutlineWidthPx,
                OutlineColor = Color.Black.ToColor4(),
            };
            if (EditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint)
            {
                ctrlPointMarker.FillColor = SelectedFillColor;
                ctrlPointMarker.GlowColor = SelectedFillColor;
                ctrlPointMarker.GlowWidthPx = SelectedGlowWidthPx;
            }
            markerSet.Markers.Add(ctrlPointMarker);

            var lineSet = controlPointLineVisualizerComponent.LineSet;
            lineSet.Segments.Clear();
            // Control Point visuals
            var ctrlPointOrientationMarker = new GizmoMarkerData
            {
                Shape = GizmoMarkerShape.Circle,
                OrientationMode = GizmoMarkerOrientationMode.World,
                ScaleMode = GizmoMarkerScaleMode.FixedScreenSize,
                Position = Vector3.Zero,
                Rotation = Quaternion.RotationX(MathUtil.PiOverTwo),
                FillColor = Color.Transparent.ToColor4(),
                SizePx = new Vector2(60),
                OutlineWidthPx = 1.5f,
                OutlineColor = ControlPointOrientationLineColor,
            };
            if (EditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint)
            {
                ctrlPointOrientationMarker.FillColor = SelectedFillColor with { A = 0.25f };
                ctrlPointOrientationMarker.OutlineColor = SelectedLineColor;
            }
            markerSet.Markers.Add(ctrlPointOrientationMarker);

            var ctrlPointUpVec = Vector3.UnitY;             // TODO
            var ctrlPointUpVecColor = EditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint
                ? SelectedLineColor
                : ControlPointNormalLineColor;
            lineSet.AddViewScaledLengthLine(Vector3.Zero, ctrlPointUpVec, fixedLengthPx: 60, ctrlPointUpVecColor, ControlPointUpVectorThicknessPx);

            // Tangent In visuals
            lineSet.AddWorldLine(Vector3.Zero, tangentInEntityData.CurrentLocalPosition, TangentLineColor, TangentLineThicknessPx);
            var tangentInMarker = new GizmoMarkerData
            {
                Shape = GizmoMarkerShape.Diamond,
                OrientationMode = GizmoMarkerOrientationMode.Billboard,
                ScaleMode = GizmoMarkerScaleMode.FixedScreenSize,
                Position = tangentInEntityData.CurrentLocalPosition,
                FillColor = TangentInHandleFillColor,
                SizePx = new Vector2(ShapeSize),
                OutlineWidthPx = OutlineWidthPx,
                OutlineColor = Color.Black.ToColor4(),
            };
            if (EditingSelectionType == SplineControlPointEditingSelectionType.TangentIn)
            {
                tangentInMarker.FillColor = SelectedFillColor;
                tangentInMarker.GlowColor = SelectedFillColor;
                tangentInMarker.GlowWidthPx = SelectedGlowWidthPx;
            }
            markerSet.Markers.Add(tangentInMarker);

            // Tangent Out visuals
            lineSet.AddWorldLine(Vector3.Zero, tangentOutEntityData.CurrentLocalPosition, TangentLineColor, TangentLineThicknessPx);
            var tangentOutMarker = new GizmoMarkerData
            {
                Shape = GizmoMarkerShape.Diamond,
                OrientationMode = GizmoMarkerOrientationMode.Billboard,
                ScaleMode = GizmoMarkerScaleMode.FixedScreenSize,
                Position = tangentOutEntityData.CurrentLocalPosition,
                FillColor = TangentOutHandleFillColor,
                SizePx = new Vector2(ShapeSize),
                OutlineWidthPx = OutlineWidthPx,
                OutlineColor = Color.Black.ToColor4(),
            };
            if (EditingSelectionType == SplineControlPointEditingSelectionType.TangentOut)
            {
                tangentOutMarker.FillColor = SelectedFillColor;
                tangentOutMarker.GlowColor = SelectedFillColor;
                tangentOutMarker.GlowWidthPx = SelectedGlowWidthPx;
            }
            markerSet.Markers.Add(tangentOutMarker);

            isMarkersAndLinesUpdateRequired = false;
        }
    }

    private float GetTargetedScale(IEditorGameCameraService cameraService)
    {
        var splineEntity = splineComponent.Entity;
        if (splineEntity is not null
            && cameraService.Component.Projection == CameraProjectionMode.Perspective)
        {
            var distanceToSelectedEntity = MathF.Abs(Vector3.TransformCoordinate(splineEntity.Transform.WorldMatrix.TranslationVector, cameraService.ViewMatrix).Z);
            return SizeFactor * DefaultScale * 2f * MathF.Tan(MathUtil.DegreesToRadians(cameraService.VerticalFieldOfView / 2)) * distanceToSelectedEntity;
        }

        return SizeFactor * DefaultScale * cameraService.Component.OrthographicSize;
    }

    private void UpdateTangentTransformation(ref TangentEntityData tangentEntityData, in SplineControlPoint controlPoint, in Vector3 tangentPosition, in Vector3 upVec)
    {
        var tangentHandleLocalPosition = tangentPosition - controlPoint.Position;
        isMarkersAndLinesUpdateRequired = isMarkersAndLinesUpdateRequired || tangentEntityData.CurrentLocalPosition != tangentHandleLocalPosition;
        tangentEntityData.CurrentLocalPosition = tangentHandleLocalPosition;
    }

    internal bool TryRaycastOnHandle(Ray clickRay, SplineControlPointRaycastFilterFlags raycastFilterFlags, ref float minHitDistance, ref SplineControlPointEditingSelectionType newSelection)
    {
        var spline = splineComponent.Spline;
        if (controlPointIndex >= spline.Count || spline.Count == 0)
        {
            return false;
        }
        var controlPoint = spline[controlPointIndex];

        cameraService ??= Game.EditorServices.Get<IEditorGameCameraService>();
        float targetedScale = GetTargetedScale(cameraService);

        bool isHit = false;

        var controlPointPos = controlPoint.Position;
        var boxHalfSize = new Vector3(0.35f * 0.5f);
        var controlPointBoxHalfSize = boxHalfSize * targetedScale;  // TODO
        var controlPointBox = new BoundingBox(minimum: controlPointPos - controlPointBoxHalfSize, maximum: controlPointPos + controlPointBoxHalfSize);
        if ((raycastFilterFlags & SplineControlPointRaycastFilterFlags.ControlPoint) != 0
            && controlPointBox.Intersects(ref clickRay, out float hitDistance))
        {
            if (hitDistance < minHitDistance)
            {
                minHitDistance = hitDistance;
                newSelection = SplineControlPointEditingSelectionType.ControlPoint;
                isHit = true;
            }
        }

        if ((raycastFilterFlags & SplineControlPointRaycastFilterFlags.Tangents) != 0)
        {
            var tangentOutPos = tangentOutEntityData.CurrentLocalPosition + controlPoint.Position;
            var tangentOutBoxHalfSize = boxHalfSize * targetedScale;
            var tangentOutBox = new BoundingBox(minimum: tangentOutPos - tangentOutBoxHalfSize, maximum: tangentOutPos + tangentOutBoxHalfSize);
            if (tangentOutBox.Intersects(ref clickRay, out hitDistance))
            {
                if (hitDistance < minHitDistance)
                {
                    minHitDistance = hitDistance;
                    newSelection = SplineControlPointEditingSelectionType.TangentOut;
                    isHit = true;
                }
            }

            var tangentInPos = tangentInEntityData.CurrentLocalPosition + controlPoint.Position;
            var tangentInBoxHalfSize = boxHalfSize * targetedScale;
            var tangentInBox = new BoundingBox(minimum: tangentInPos - tangentInBoxHalfSize, maximum: tangentInPos + tangentInBoxHalfSize);
            if (tangentInBox.Intersects(ref clickRay, out hitDistance))
            {
                if (hitDistance < minHitDistance)
                {
                    minHitDistance = hitDistance;
                    newSelection = SplineControlPointEditingSelectionType.TangentIn;
                    isHit = true;
                }
            }
        }

        return isHit;
    }

    private struct TangentEntityData
    {
        public Vector3 CurrentLocalPosition;
    }
}
