using Stride.Engine;
using Stride.Engine.Splines.Components;

namespace SplineTools
{
    public class MoveOnStart : StartupScript
    {

        public override void Start()
        {

            Entity.Get<SplineTraverserComponent>().IsMoving = true;
        }
    }
}
