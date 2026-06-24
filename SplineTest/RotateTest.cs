using Stride.Engine;
using Stride.Engine.Splines.Components;

namespace SplineTools
{
    public class RotateTest : StartupScript
    {
        public Entity RotateToEntity;
        public SplineComponent Spline;

        public override void Start()
        {
            foreach (var splineNode in Spline?.Spline?.SplineNodes)
            {
                var points = splineNode.GetBezierPoints();
                for (int i = 0; i < points.Length-1; i++)
                {
                    var instance = RotateToEntity.Clone();
                    instance.Transform.Parent = Entity.Transform;
                    instance.Transform.Position = Entity.Transform.WorldToLocal(points[i].Position);
                    instance.Transform.Rotation = points[i].Rotation;
                }
            }
        }
    }
}
