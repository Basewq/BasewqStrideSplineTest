using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Input;

namespace SplineTools;

public class SplineByMeshDemo : SyncScript
{
    public override void Update()
    {
        const int HelpTextStartX = 800;
        DebugText.Print($"Press H to toggle spline line", new Int2(HelpTextStartX, 20));

        if (Input.IsKeyPressed(Keys.H))
        {
            foreach (var entity in Entity.EntityManager)
            {
                var splineComp = entity.Get<SplineComponent>();
                if (splineComp is not null)
                {
                    splineComp.DebugRenderSettings.ShowCurves = !splineComp.DebugRenderSettings.ShowCurves;
                }
            }
        }
    }
}
