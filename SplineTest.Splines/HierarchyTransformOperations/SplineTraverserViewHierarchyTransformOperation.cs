// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Components;

namespace Stride.Engine.Splines.HierarchyTransformOperations;

/// <summary>
/// Updates <see cref="Components.SplineTraverserComponent"/>.
/// </summary>
public class SplineTraverserViewHierarchyTransformOperation : TransformOperation
{
    public readonly SplineTraverserComponent SplineTraverserComponent;

    public SplineTraverserViewHierarchyTransformOperation(SplineTraverserComponent modelComponent)
    {
        SplineTraverserComponent = modelComponent;
    }

    /// <inheritdoc/>
    public override void Process(TransformComponent transformComponent)
    {
        SplineTraverserComponent.Update(transformComponent);
    }
}
