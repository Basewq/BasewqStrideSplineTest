// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Components;
using Stride.Games;

namespace Stride.Engine.Splines.Processors;

public class SplineEditorProcessor : EntityProcessor<SplineComponent, SplineEditorProcessor.AssociatedData>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplineTransformProcessor"/> class.
    /// </summary>
    public SplineEditorProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineComponent component)
    {
        return new AssociatedData
        {
            PreviousEditState = component.Control.EditSplineState
        };
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineComponent component, AssociatedData associatedData)
    {
        return true;
    }

    protected override void OnEntityComponentAdding(Entity entity, SplineComponent component, AssociatedData data)
    {
    }

    protected override void OnEntityComponentRemoved(Entity entity, SplineComponent component, AssociatedData data)
    {
    }

    public override void Update(GameTime time)
    {
        foreach (var (splineComp, data) in ComponentDatas)
        {
            if (data.PreviousEditState != splineComp.Control.EditSplineState)
            {
                data.PreviousEditState = splineComp.Control.EditSplineState;
            }
        }
    }

    public class AssociatedData
    {
        public EditSplineState PreviousEditState;
    }
}
