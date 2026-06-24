// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Components;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Engine.Splines.Models;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

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
    private const int LineTessellation = 16;
    private const float LineRadius = 0.025f;
    private const float HandleCubeSize = 0.35f;
    private const float HandleCubeHeightScale = 0.35f;
    private const float GizmoDefaultSize = 48; // the default size of the gizmo on the screen in pixels.

    private static readonly Color ControlPointColor = Color.White;
    private static readonly Color TangentInHandleColor = Color.Aqua;
    private static readonly Color TangentOutHandleColor = Color.Goldenrod;
    private static readonly Color TangentLineColor = Color.WhiteSmoke;
    private static readonly Color SelectedColor = Color.Gold;

    private readonly Dictionary<int, ModelComponent> controlPointGizmoModelComponents = new Dictionary<int, ModelComponent>();

    private Material controlPointMaterial;
    private Material tangentInMaterial;
    private Material tangentOutMaterial;
    private Material tangentLineMaterial;
    private Material selectedMaterial;

    private IEditorGameCameraService cameraService;

    private int controlPointIndex;
    private SplineComponent splineComponent;
    private Entity controlPointRootGizmoEntity;
    private ModelComponent controlPointModelComponent;
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
        controlPointMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, ControlPointColor, 0.75f);
        tangentInMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentInHandleColor, 0.75f);
        tangentOutMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentOutHandleColor, 0.75f);
        tangentLineMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentLineColor, 0.75f);
        selectedMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, SelectedColor);

        var controlPointMesh = GeometricPrimitive.Cube.New(GraphicsDevice, size: HandleCubeSize);
        disposableGeometricPrimitives.Add(controlPointMesh);
        var controlPointMeshDraw = controlPointMesh.ToMeshDraw();
        controlPointRootGizmoEntity = new Entity($"SplineControlPoint_{controlPointIndex}");

        var controlPointModelEntity = new Entity($"SplineControlPoint_Model_{controlPointIndex}");
        controlPointModelEntity.SetParent(controlPointRootGizmoEntity);
        controlPointModelComponent = new ModelComponent
        {
            Enabled = true,
            Model = new Model { controlPointMaterial, new Mesh { Draw = controlPointMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        controlPointModelEntity.Add(controlPointModelComponent);

        var handleMesh = GeometricPrimitive.Cube.New(GraphicsDevice, size: HandleCubeSize);
        disposableGeometricPrimitives.Add(handleMesh);
        var handleMeshDraw = handleMesh.ToMeshDraw();
        const float LineLength = 1;     // The line's true length will be changed via the entity's scale
        var lineMesh = GizmoModelHelper.CreateLine(GraphicsDevice, LineLength, LineRadius, LineTessellation);
        disposableGeometricPrimitives.Add(lineMesh);
        var lineMeshDraw = lineMesh.ToMeshDraw();
        BuildTangentGizmoEntity("TangentIn", tangentInMaterial, tangentLineMaterial, handleMeshDraw, lineMeshDraw, out tangentInEntityData);
        BuildTangentGizmoEntity("TangentOut", tangentOutMaterial, tangentLineMaterial, handleMeshDraw, lineMeshDraw, out tangentOutEntityData);

        return controlPointRootGizmoEntity;
    }

    private void BuildTangentGizmoEntity(string name, Material handleMaterial, Material lineMaterial, MeshDraw handleMeshDraw, MeshDraw lineMeshDraw, out TangentEntityData data)
    {
        data = new();
        // Three entities will be used to represent the tangent for easier management:
        // - TangentInRoot/TangentOutRoot: The 'container' entity for the rest of the entities.
        // - TangentInHandle/TangentOutHandle: The handle of the tangent.
        // - TangentInLineModel/TangentOutLineModel: The line model entity used to show the control point to handle connection.
        data.RootEntity = new Entity($"{name}_Root_{controlPointIndex}");
        // The handle model
        data.HandleModelComponent = new ModelComponent
        {
            Enabled = false,
            Model = new Model { handleMaterial, new Mesh { Draw = handleMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        data.HandleEntity = new Entity($"SplineControlPoint_{name}_Handle_{controlPointIndex}") { data.HandleModelComponent };
        data.HandleEntity.SetParent(data.RootEntity);

        // The line model
        data.LineModelComponent = new ModelComponent
        {
            Enabled = false,
            Model = new Model { lineMaterial, new Mesh { Draw = lineMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        data.LineModelEntity = new Entity($"SplineControlPoint_{name}_LineModel_{controlPointIndex}")
        {
            data.LineModelComponent
        };
        data.LineModelEntity.SetParent(data.RootEntity);

        // Attach to root gizmo entity
        data.RootEntity.SetParent(controlPointRootGizmoEntity);
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

        controlPointModelComponent.Entity.Transform.Scale = new Vector3(targetedScale, targetedScale * HandleCubeHeightScale, targetedScale);

        var upVec = splineEntity.Transform.WorldMatrix.Up;
        UpdateTangentTransformation(tangentInEntityData, controlPoint, controlPoint.TangentInPosition, upVec, targetedScale);
        UpdateTangentTransformation(tangentOutEntityData, controlPoint, controlPoint.TangentOutPosition, upVec, targetedScale);

        // Check if updating color
        if (previousEditingSelectionType != EditingSelectionType)
        {
            if (previousEditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint
                || EditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint)
            {
                bool isSelected = EditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint;
                controlPointModelComponent.Model.Materials[0] = isSelected ? selectedMaterial : controlPointMaterial;
            }
            if (previousEditingSelectionType == SplineControlPointEditingSelectionType.TangentIn
                || EditingSelectionType == SplineControlPointEditingSelectionType.TangentIn)
            {
                bool isSelected = EditingSelectionType == SplineControlPointEditingSelectionType.TangentIn;
                tangentInEntityData.HandleModelComponent.Model.Materials[0] = isSelected ? selectedMaterial : tangentInMaterial;
            }
            if (previousEditingSelectionType == SplineControlPointEditingSelectionType.TangentOut
                || EditingSelectionType == SplineControlPointEditingSelectionType.TangentOut)
            {
                bool isSelected = EditingSelectionType == SplineControlPointEditingSelectionType.TangentOut;
                tangentOutEntityData.HandleModelComponent.Model.Materials[0] = isSelected ? selectedMaterial : tangentOutMaterial;
            }
            previousEditingSelectionType = EditingSelectionType;
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

    private void UpdateTangentTransformation(in TangentEntityData tangentEntityData, in SplineControlPoint controlPoint, in Vector3 tangentPosition, in Vector3 upVec, float targetedScale)
    {
        var tangentHandleLocalPosition = tangentPosition - controlPoint.Position;
        var controlPointToHandleLength = tangentHandleLocalPosition.Length();
        if (MathUtil.IsZero(controlPointToHandleLength))
        {
            tangentEntityData.HandleModelComponent.Enabled = false;
            tangentEntityData.LineModelComponent.Enabled = false;
            return;
        }

        tangentEntityData.HandleModelComponent.Enabled = true;
        tangentEntityData.LineModelComponent.Enabled = true;
        // Update handle position
        tangentEntityData.HandleEntity.Transform.Position = tangentHandleLocalPosition;
        tangentEntityData.HandleEntity.Transform.Scale = new Vector3(targetedScale, targetedScale * HandleCubeHeightScale, targetedScale);
        // Make line point in the correct direction and rescale the mesh
        var lineRotation = Quaternion.LookRotation(forward: Vector3.Normalize(tangentHandleLocalPosition), upVec);
        tangentEntityData.LineModelEntity.Transform.Rotation = lineRotation;
        tangentEntityData.LineModelEntity.Transform.Scale = new Vector3(targetedScale, targetedScale, controlPointToHandleLength);
    }

    internal bool TryRaycastOnHandle(Ray clickRay, SplineControlPointRaycastFilterFlags raycastFilterFlags, ref float minHitDistance, ref SplineControlPointEditingSelectionType newSelection)
    {
        var spline = splineComponent.Spline;
        if (controlPointIndex >= spline.Count || spline.Count == 0)
        {
            return false;
        }
        var controlPoint = spline[controlPointIndex];

        var boxHalfSize = new Vector3(HandleCubeSize * 0.5f);

        bool isHit = false;

        var controlPointPos = controlPoint.Position;
        var controlPointBoxHalfSize = boxHalfSize * controlPointModelComponent.Entity.Transform.Scale;
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
            var tangentOutPos = tangentOutEntityData.HandleEntity.Transform.Position + controlPoint.Position;
            var tangentOutBoxHalfSize = boxHalfSize * tangentOutEntityData.HandleEntity.Transform.Scale;
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

            var tangentInPos = tangentInEntityData.HandleEntity.Transform.Position + controlPoint.Position;
            var tangentInBoxHalfSize = boxHalfSize * tangentInEntityData.HandleEntity.Transform.Scale;
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
        public Entity RootEntity;

        public Entity HandleEntity;
        public ModelComponent HandleModelComponent;

        public Entity LineModelEntity;
        public ModelComponent LineModelComponent;
    }
}
