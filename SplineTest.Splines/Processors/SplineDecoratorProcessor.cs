// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Generic;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.HierarchyTransformOperations;
using Stride.Engine.Splines.Models.Decorators;
using Stride.Engine.Splines.Processors.DecoratorProcessors;
using Stride.Games;

namespace Stride.Engine.Splines.Processors;

/// <summary>
/// The processor for <see cref="SplineDecoratorComponent"/>.
/// </summary>
public class SplineDecoratorProcessor : EntityProcessor<SplineDecoratorComponent, SplineDecoratorProcessor.AssociatedData>
{
    private HashSet<SplineDecoratorComponent> splineDecoratorComponentsToUpdate = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SplineDecoratorProcessor"/> class.
    /// </summary>
    public SplineDecoratorProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineDecoratorComponent component)
    {
        return new AssociatedData
        {
            TransformOperation = new SplineDecoratorViewHierarchyTransformOperation(component),
        };
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineDecoratorComponent component, AssociatedData associatedData)
    {
        return component == associatedData.TransformOperation.SplineDecoratorComponent;
    }

    protected override void OnEntityComponentAdding(Entity entity, SplineDecoratorComponent component, AssociatedData data)
    {
        // Every time the spline decorator is marked as dirty, we want to re-decorate the spline
        component.OnSplineDecoratorDirty += OnSplineDecoratorDirty;

        splineDecoratorComponentsToUpdate.Add(component);

        entity.Transform.PostOperations.Add(data.TransformOperation);
    }

    private void OnSplineDecoratorDirty(SplineDecoratorComponent component)
    {
        if (ComponentDatas.TryGetValue(component, out var data))
        {
            data.Update(splineDecoratorComponentsToUpdate, component);
        }
    }

    protected override void OnEntityComponentRemoved(Entity entity, SplineDecoratorComponent component, AssociatedData data)
    {
        splineDecoratorComponentsToUpdate.Add(component);

        component.OnSplineDecoratorDirty -= OnSplineDecoratorDirty;

        entity.Transform.PostOperations.Remove(data.TransformOperation);
    }

    public override void Update(GameTime time)
    {
        foreach (var decoratorComponent in splineDecoratorComponentsToUpdate)
        {
            if (decoratorComponent?.DecoratorSettings is null)
            {
                continue;
            }

            decoratorComponent.ClearDecorationInstances();

            BaseSplineDecoratorProcessor baseSplineDecoratorProcessor = decoratorComponent.DecoratorSettings switch
            {
                SplineAmountDecoratorSettings => new AmountDecoratorProcessor(),
                SplineIntervalDecoratorSettings => new IntervalDecoratorProcessor(),
                _ => throw new InvalidOperationException($"Unsupported SplineDecoratorSettings type {decoratorComponent.DecoratorSettings}")
            };

            baseSplineDecoratorProcessor.Decorate(decoratorComponent);
        }

        // Now that dirty spline decorators have been updated, clear the collection
        splineDecoratorComponentsToUpdate.Clear();
    }

    public class AssociatedData
    {
        public SplineDecoratorViewHierarchyTransformOperation TransformOperation;

        public void Update(HashSet<SplineDecoratorComponent> splineDecoratorComponentsToUpdate, SplineDecoratorComponent component)
        {
            splineDecoratorComponentsToUpdate.Add(component);
        }
    }
}
