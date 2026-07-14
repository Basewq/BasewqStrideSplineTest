// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.HierarchyTransformOperations;
using Stride.Engine.Splines.Models.Mesh;
using Stride.Rendering;

namespace Stride.Engine.Splines.Processors;

/// <summary>
/// The processor for <see cref="SplineMeshComponent"/>.
/// </summary>
public class SplineMeshTransformProcessor : EntityProcessor<SplineMeshComponent, SplineMeshTransformProcessor.AssociatedData>
{
    private HashSet<SplineMeshComponent> splineMeshComponentsToUpdate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SplineMeshTransformProcessor"/> class.
    /// </summary>
    public SplineMeshTransformProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineMeshComponent component)
    {
        return new AssociatedData
        {
            TransformOperation = new SplineMeshViewHierarchyTransformOperation(component),
        };
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineMeshComponent component, AssociatedData associatedData)
    {
        return component == associatedData.TransformOperation.SplineMeshComponent;
    }

    protected override void OnEntityComponentAdding(Entity entity, SplineMeshComponent component, AssociatedData data)
    {
        component.OnMeshRequiresUpdate += EnqueueMeshComponentForUpdate;
        if (component.SplineComponent is not null && component.SplineMesh is not null)
        {
            EnqueueMeshComponentForUpdate(component);
        }

        entity.Transform.PostOperations.Add(data.TransformOperation);
    }

    private void EnqueueMeshComponentForUpdate(SplineMeshComponent component)
    {
        splineMeshComponentsToUpdate.Add(component);
    }

    protected override void OnEntityComponentRemoved(Entity entity, SplineMeshComponent component, AssociatedData data)
    {
        component.OnMeshRequiresUpdate -= EnqueueMeshComponentForUpdate;
        entity.Transform.PostOperations.Remove(data.TransformOperation);

        if (data.MeshEntity is not null)
        {
            data.MeshEntity.SetParent(null);
            data.MeshEntity.Scene = null;
        }
    }

    public override void Draw(RenderContext context)
    {
        foreach (var splineMeshComponent in splineMeshComponentsToUpdate)
        {
            if (!ComponentDatas.TryGetValue(splineMeshComponent, out var data))
            {
                continue;
            }

            if (splineMeshComponent.SplineComponent?.Spline is null || splineMeshComponent.SplineMesh is null)
            {
                data.MeshModelComponent?.Enabled = false;
                data.MeshModelComponent?.Model = null;
                data.ModelResource.ReleaseResources();
                continue;
            }

            if (data.MeshEntity is null)
            {
                // Create a new entity, with a model component which holds the generated splinemesh
                data.MeshEntity = new Entity("GeneratedSplineMeshEntity");
                //data.MeshEntity.Transform.Position -= splineMeshComponent.Entity.Transform.Position;
                data.MeshModelComponent = new ModelComponent();
                data.MeshEntity.Add(data.MeshModelComponent);

                data.MeshEntity.SetParent(splineMeshComponent.Entity);
            }

            if (splineMeshComponent.SplineMesh is SplineMeshShape splineMeshShape
                && splineMeshShape.ShapeSplineComponent?.Spline is null)
            {
                continue;
            }
            if (splineMeshComponent.SplineComponent.Spline.Count <= 1)
            {
                continue;
            }

            data.ModelResource.ReleaseResources();

            // Create a model and generate its mesh
            splineMeshComponent.SplineMesh.Spline = splineMeshComponent.SplineComponent.Spline;
            splineMeshComponent.SplineMesh.SplineEvaluator = splineMeshComponent.SplineComponent.SplineEvaluator;
            var model = new Model();
            splineMeshComponent.SplineMesh.Generate(Services, model);
            data.ModelResource.AddFromModel(model);
            data.MeshModelComponent.Model = model;
            data.MeshModelComponent.Enabled = true;
        }

        // Now that dirty splines meshes are updated, clear the collection
        splineMeshComponentsToUpdate.Clear();
    }

    public class AssociatedData
    {
        public SplineMeshViewHierarchyTransformOperation TransformOperation;

        public Entity? MeshEntity;
        public ModelComponent? MeshModelComponent;

        public readonly ModelResource ModelResource = new();
    }
}
