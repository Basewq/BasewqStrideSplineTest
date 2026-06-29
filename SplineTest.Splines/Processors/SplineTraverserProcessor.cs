// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.HierarchyTransformOperations;
using Stride.Engine.Splines.Models;
using Stride.Games;

namespace Stride.Engine.Splines.Processors;

/// <summary>
/// The processor for <see cref="SplineTraverserComponent"/>.
/// </summary>
public class SplineTraverserProcessor : EntityProcessor<SplineTraverserComponent, SplineTraverserProcessor.AssociatedData>
{
    private HashSet<SplineTraverserComponent> splineTraverserComponents = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SplineProcessor"/> class.
    /// </summary>
    public SplineTraverserProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineTraverserComponent component)
    {
        var data = new AssociatedData(this, component)
        {
            TransformOperation = new SplineTraverserViewHierarchyTransformOperation(component)
        };

        return data;
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineTraverserComponent component, AssociatedData associatedData)
    {
        return component == associatedData.TransformOperation.SplineTraverserComponent;
    }

    protected override void OnEntityComponentAdding(Entity entity, SplineTraverserComponent component, AssociatedData data)
    {
        if (component.SplineComponent is not null)
        {
            component.SplineComponent.Spline.SplinePropertyChanged += data.OnSplinePropertyChangedAction;
            component.SplineComponent.Spline.ControlPointsChanged += data.OnSplineControlPointCollectionChangedAction;
        }

        splineTraverserComponents.Add(component);
        component.SplineTraverser.Spline = component.SplineComponent?.Spline;
        //component.SplineTraverser.Entity = entity;

        entity.Transform.PostOperations.Add(data.TransformOperation);
    }

    protected override void OnEntityComponentRemoved(Entity entity, SplineTraverserComponent component, AssociatedData data)
    {
        component.SplineComponent?.Spline.SplinePropertyChanged -= data.OnSplinePropertyChangedAction;
        component.SplineComponent?.Spline.ControlPointsChanged -= data.OnSplineControlPointCollectionChangedAction;
        entity.Transform.PostOperations.Remove(data.TransformOperation);
        splineTraverserComponents.Remove(component);
    }

    public class AssociatedData
    {
        public SplineTraverserViewHierarchyTransformOperation TransformOperation;
        public SplinePropertyChangedEventHandler OnSplinePropertyChangedAction;
        public SplineControlPointEventHandler<SplineControlPointsChangedEventArgs> OnSplineControlPointCollectionChangedAction;

        internal SplineSample PreviousSplineSample;
        internal bool AttachedToSpline;

        public AssociatedData(SplineTraverserProcessor processor, SplineTraverserComponent component)
        {
            OnSplinePropertyChangedAction = sender => Update(processor, component);
            OnSplineControlPointCollectionChangedAction = (sender, ref e) => Update(processor, component);
        }

        private void Update(SplineTraverserProcessor processor, SplineTraverserComponent component)
        {
            processor.splineTraverserComponents.Add(component);
            AttachedToSpline = false;
        }
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var (component, data) in ComponentDatas)
        {
            if (!IsValidComponent(component))
                continue;

            if (!data.AttachedToSpline)
            {
                DetermineOriginAndTarget(component, data);
            }

            if (!component.IsMoving || component.Speed == 0 || !data.AttachedToSpline)
            {
                continue;
            }

            float dt = (float)gameTime.Elapsed.TotalSeconds;
            component.SplineTraverser.Update(dt);
            var curSplineSample = component.SplineTraverser.CurrentSplineSample;
            //component.Entity.Transform.Position = curSplineSample.Position;
            //if (component.SplineTraverser.IsRotating)
            //{
            //    component.Entity.Transform.Rotation = curSplineSample.Rotation;
            //}

            data.PreviousSplineSample = curSplineSample;
        }
    }

    private static bool IsValidComponent(SplineTraverserComponent component)
    {
        if (component is { Entity: not null, SplineComponent.Spline: not null })
        {
            return true;
        }

        Console.WriteLine($"[Warning] Invalid component or missing Spline: {component}");
        return false;
    }

    private void DetermineOriginAndTarget(SplineTraverserComponent component, AssociatedData data)
    {
        if (component.Entity is null || !(component.SplineComponent.Spline?.Count > 1))
        {
            return;
        }

        var currentPositionOfTraverser = component.Entity.Transform.WorldMatrix.TranslationVector;
        component.SplineTraverser.SnapPositionToSpline(currentPositionOfTraverser);

        var curSplineSample = component.SplineTraverser.CurrentSplineSample;
        component.Entity.Transform.Position = curSplineSample.Position;
        if (component.SplineTraverser.IsRotating)
        {
            var forwardRotation = Quaternion.BetweenDirections(Vector3.UnitZ, curSplineSample.Tangent);
            component.Entity.Transform.Rotation = curSplineSample.Rotation * forwardRotation;
        }

        data.AttachedToSpline = true;
    }
}
