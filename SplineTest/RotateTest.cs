using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SplineTools;

public class RotateTest : StartupScript
{
    public Entity RotateToEntity;
    public SplineComponent Spline;

    public override void Start()
    {
        var spline = Spline?.Spline;
        if (spline is not null)
        {
            var splineSamples = new List<SplineSample>();
            spline.CollectSplineSamples(splineSamples);
            var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
            for (int i = 0; i < splineSamplesSpan.Length - 1; i++)
            {
                var instance = RotateToEntity.Clone();
                instance.Transform.Parent = Entity.Transform;
                instance.Transform.Position = Entity.Transform.WorldToLocal(splineSamplesSpan[i].Position);
                instance.Transform.Rotation = splineSamplesSpan[i].Rotation;
            }
            //foreach (var splineControlPoint in spline)
            //{
            //    var points = splineControlPoint.GetBezierPoints();
            //    for (int i = 0; i < points.Length - 1; i++)
            //    {
            //        var instance = RotateToEntity.Clone();
            //        instance.Transform.Parent = Entity.Transform;
            //        instance.Transform.Position = Entity.Transform.WorldToLocal(points[i].Position);
            //        instance.Transform.Rotation = points[i].Rotation;
            //    }
            //}
        }
    }
}
