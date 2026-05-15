using SplineTest.Splines.Components;
using Stride.Games;

namespace SplineTest.Splines.Processors;

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
