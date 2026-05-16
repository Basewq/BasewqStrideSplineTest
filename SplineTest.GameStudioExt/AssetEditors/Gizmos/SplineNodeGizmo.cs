using SplineTest.Splines.Components;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Processors;
using Stride.Engine.Splines.Models;
using Stride.Extensions;
using Stride.Graphics.GeometricPrimitives;
using Stride.Rendering;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Spline;

public enum SplineNodeEditingSelectionType
{
    None,
    SplineNode,
    TangentIn,
    TangentOut
}

[Flags]
public enum SplineNodeRaycastFilterFlags
{
    None = 0,
    SplineNode = 1 << 0,
    Tangents = 1 << 1,
    All = SplineNode | Tangents
}

//[GizmoComponent(typeof(SplineComponent), isMainGizmo: false)]
public class SplineNodeGizmo : GizmoBase
{
    private const int LineTessellation = 16;
    private const float LineRadius = 0.025f;
    private const float HandleCubeSize = 0.35f;
    private const float HandleCubeHeightScale = 0.35f;
    private const float GizmoDefaultSize = 48; // the default size of the gizmo on the screen in pixels.

    private static readonly Color SplineNodeColor = Color.White;
    private static readonly Color TangentInHandleColor = Color.Aqua;
    private static readonly Color TangentOutHandleColor = Color.Goldenrod;
    private static readonly Color TangentLineColor = Color.WhiteSmoke;
    private static readonly Color SelectedColor = Color.Gold;

    private readonly Dictionary<int, ModelComponent> nodeGizmoModelComponents = new Dictionary<int, ModelComponent>();

    private Material splineNodeMaterial;
    private Material tangentInMaterial;
    private Material tangentOutMaterial;
    private Material tangentLineMaterial;
    private Material selectedMaterial;

    private IEditorGameCameraService cameraService;

    private int splineNodeIndex;
    private SplineComponent splineComponent;
    private Entity nodeRootGizmoEntity;
    private ModelComponent splineNodeModelComponent;
    private TangentEntityData tangentInEntityData;
    private TangentEntityData tangentOutEntityData;

    private List<GeometricPrimitive> disposableGeometricPrimitives = [];

    /// <summary>
    /// Gets the gizmo default scale in ratio of screen height ( 1 => full screen vertically )
    /// </summary>
    public float DefaultScale => GizmoDefaultSize / GraphicsDevice.Presenter.BackBuffer.Height;

    public SplineNodeGizmo(int splineNodeIndex, SplineComponent splineComponent) : base()
    {
        RenderGroup = SplineGizmo.GizmoRenderGroup;

        this.splineNodeIndex = splineNodeIndex;
        this.splineComponent = splineComponent;
    }

    private SplineNodeEditingSelectionType previousEditingSelectionType;
    public SplineNodeEditingSelectionType EditingSelectionType { get; set; }

    protected override Entity Create()
    {
        splineNodeMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, SplineNodeColor, 0.75f);
        tangentInMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentInHandleColor, 0.75f);
        tangentOutMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentOutHandleColor, 0.75f);
        tangentLineMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, TangentLineColor, 0.75f);
        selectedMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, SelectedColor);

        var splineNodeMesh = GeometricPrimitive.Cube.New(GraphicsDevice, size: HandleCubeSize);
        disposableGeometricPrimitives.Add(splineNodeMesh);
        var splineNodeMeshDraw = splineNodeMesh.ToMeshDraw();
        nodeRootGizmoEntity = new Entity($"SplineNode_{splineNodeIndex}");

        var splineNodeModelEntity = new Entity($"SplineNode_Model_{splineNodeIndex}");
        splineNodeModelEntity.SetParent(nodeRootGizmoEntity);
        splineNodeModelComponent = new ModelComponent
        {
            Enabled = true,
            Model = new Model { splineNodeMaterial, new Mesh { Draw = splineNodeMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        splineNodeModelEntity.Add(splineNodeModelComponent);

        var handleMesh = GeometricPrimitive.Cube.New(GraphicsDevice, size: HandleCubeSize);
        disposableGeometricPrimitives.Add(handleMesh);
        var handleMeshDraw = handleMesh.ToMeshDraw();
        const float LineLength = 1;     // The line's true length will be changed via the entity's scale
        var lineMesh = GizmoModelHelper.CreateLine(GraphicsDevice, LineLength, LineRadius, LineTessellation);
        disposableGeometricPrimitives.Add(lineMesh);
        var lineMeshDraw = lineMesh.ToMeshDraw();
        BuildTangentGizmoEntity("TangentIn", tangentInMaterial, tangentLineMaterial, handleMeshDraw, lineMeshDraw, out tangentInEntityData);
        BuildTangentGizmoEntity("TangentOut", tangentOutMaterial, tangentLineMaterial, handleMeshDraw, lineMeshDraw, out tangentOutEntityData);

        return nodeRootGizmoEntity;
    }

    private void BuildTangentGizmoEntity(string name, Material handleMaterial, Material lineMaterial, MeshDraw handleMeshDraw, MeshDraw lineMeshDraw, out TangentEntityData data)
    {
        data = new();
        // Three entities will be used to represent the tangent for easier management:
        // - TangentInRoot/TangentOutRoot: The 'container' entity for the rest of the entities.
        // - TangentInHandle/TangentOutHandle: The handle of the tangent.
        // - TangentInLineModel/TangentOutLineModel: The line model entity used to show the node to handle connection.
        data.RootEntity = new Entity($"{name}_Root_{splineNodeIndex}");
        // The handle model
        data.HandleModelComponent = new ModelComponent
        {
            Enabled = false,
            Model = new Model { handleMaterial, new Mesh { Draw = handleMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        data.HandleEntity = new Entity($"SplineNode_{name}_Handle_{splineNodeIndex}") { data.HandleModelComponent };
        data.HandleEntity.SetParent(data.RootEntity);

        // The line model
        data.LineModelComponent = new ModelComponent
        {
            Enabled = false,
            Model = new Model { lineMaterial, new Mesh { Draw = lineMeshDraw } },
            RenderGroup = RenderGroup,
            IsShadowCaster = false,
        };
        data.LineModelEntity = new Entity($"SplineNode_{name}_LineModel_{splineNodeIndex}")
        {
            data.LineModelComponent
        };
        data.LineModelEntity.SetParent(data.RootEntity);

        // Attach to root gizmo entity
        data.RootEntity.SetParent(nodeRootGizmoEntity);
    }

    public override void Initialize(IServiceRegistry services, Scene editorScene)
    {
        base.Initialize(services, editorScene);

        CollectComponentIds(nodeRootGizmoEntity);
    }

    private void CollectComponentIds(Entity entity)
    {
        foreach (var component in entity.Components)
        {
            if (component is ModelComponent modelComponent)
            {
                var id = RuntimeIdHelper.ToRuntimeId(modelComponent);
                nodeGizmoModelComponents.Add(id, modelComponent);
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
        bool isMatch = pickedComponentId.Match(nodeGizmoModelComponents, out _);
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
        if (splineEntity == null || splineNodeIndex >= spline.Count || spline.Count == 0)
        {
            return;
        }

        var splineNode = spline[splineNodeIndex];

        nodeRootGizmoEntity.Transform.Position = splineNode.Position;

        cameraService ??= Game.EditorServices.Get<IEditorGameCameraService>();
        float targetedScale = GetTargetedScale(cameraService);

        splineNodeModelComponent.Entity.Transform.Scale = new Vector3(targetedScale, targetedScale * HandleCubeHeightScale, targetedScale);

        var upVec = splineEntity.Transform.WorldMatrix.Up;
        UpdateTangentTransformation(tangentInEntityData, splineNode, splineNode.TangentInPosition, upVec, targetedScale);
        UpdateTangentTransformation(tangentOutEntityData, splineNode, splineNode.TangentOutPosition, upVec, targetedScale);

        // Check if updating color
        if (previousEditingSelectionType != EditingSelectionType)
        {
            if (previousEditingSelectionType == SplineNodeEditingSelectionType.SplineNode
                || EditingSelectionType == SplineNodeEditingSelectionType.SplineNode)
            {
                bool isSelected = EditingSelectionType == SplineNodeEditingSelectionType.SplineNode;
                splineNodeModelComponent.Model.Materials[0] = isSelected ? selectedMaterial : splineNodeMaterial;
            }
            if (previousEditingSelectionType == SplineNodeEditingSelectionType.TangentIn
                || EditingSelectionType == SplineNodeEditingSelectionType.TangentIn)
            {
                bool isSelected = EditingSelectionType == SplineNodeEditingSelectionType.TangentIn;
                tangentInEntityData.HandleModelComponent.Model.Materials[0] = isSelected ? selectedMaterial : tangentInMaterial;
            }
            if (previousEditingSelectionType == SplineNodeEditingSelectionType.TangentOut
                || EditingSelectionType == SplineNodeEditingSelectionType.TangentOut)
            {
                bool isSelected = EditingSelectionType == SplineNodeEditingSelectionType.TangentOut;
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

    private void UpdateTangentTransformation(in TangentEntityData tangentEntityData, in SplineNode splineNode, in Vector3 tangentPosition, in Vector3 upVec, float targetedScale)
    {
        var tangentHandleLocalPosition = tangentPosition - splineNode.Position;
        var nodeToHandleLength = tangentHandleLocalPosition.Length();
        if (MathUtil.IsZero(nodeToHandleLength))
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
        tangentEntityData.LineModelEntity.Transform.Scale = new Vector3(targetedScale, targetedScale, nodeToHandleLength);
    }

    internal bool TryRaycastOnHandle(Ray clickRay, SplineNodeRaycastFilterFlags raycastFilterFlags, ref float minHitDistance, ref SplineNodeEditingSelectionType newSelection)
    {
        var spline = splineComponent.Spline;
        if (splineNodeIndex >= spline.Count || spline.Count == 0)
        {
            return false;
        }
        var splineNode = spline[splineNodeIndex];

        var boxHalfSize = new Vector3(HandleCubeSize * 0.5f);

        bool isHit = false;

        var splineNodePos = splineNode.Position;
        var splineNodeBoxHalfSize = boxHalfSize * splineNodeModelComponent.Entity.Transform.Scale;
        var splineNodeBox = new BoundingBox(minimum: splineNodePos - splineNodeBoxHalfSize, maximum: splineNodePos + splineNodeBoxHalfSize);
        if ((raycastFilterFlags & SplineNodeRaycastFilterFlags.SplineNode) != 0
            && splineNodeBox.Intersects(ref clickRay, out float hitDistance))
        {
            if (hitDistance < minHitDistance)
            {
                minHitDistance = hitDistance;
                newSelection = SplineNodeEditingSelectionType.SplineNode;
                isHit = true;
            }
        }

        if ((raycastFilterFlags & SplineNodeRaycastFilterFlags.Tangents) != 0)
        {
            var tangentOutPos = tangentOutEntityData.HandleEntity.Transform.Position + splineNode.Position;
            var tangentOutBoxHalfSize = boxHalfSize * tangentOutEntityData.HandleEntity.Transform.Scale;
            var tangentOutBox = new BoundingBox(minimum: tangentOutPos - tangentOutBoxHalfSize, maximum: tangentOutPos + tangentOutBoxHalfSize);
            if (tangentOutBox.Intersects(ref clickRay, out hitDistance))
            {
                if (hitDistance < minHitDistance)
                {
                    minHitDistance = hitDistance;
                    newSelection = SplineNodeEditingSelectionType.TangentOut;
                    isHit = true;
                }
            }

            var tangentInPos = tangentInEntityData.HandleEntity.Transform.Position + splineNode.Position;
            var tangentInBoxHalfSize = boxHalfSize * tangentInEntityData.HandleEntity.Transform.Scale;
            var tangentInBox = new BoundingBox(minimum: tangentInPos - tangentInBoxHalfSize, maximum: tangentInPos + tangentInBoxHalfSize);
            if (tangentInBox.Intersects(ref clickRay, out hitDistance))
            {
                if (hitDistance < minHitDistance)
                {
                    minHitDistance = hitDistance;
                    newSelection = SplineNodeEditingSelectionType.TangentIn;
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
