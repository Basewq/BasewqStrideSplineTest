// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

public static class SplineExtensions
{
    /// <summary>
    /// Collect sample points at fixed distance.
    /// </summary>
    /// <remarks>For closed loop, the final sample point will overlap with the first sample point in the list.</remarks>
    public static void CollectSplineSamples(Spline spline, List<SplineSample> splineSamplesOutput, float sampleStepDistance)
    {
        float totalDistance = spline.GetTotalDistance();
        int sampleCount = (int)Math.Floor(totalDistance / sampleStepDistance);
        splineSamplesOutput.EnsureCapacity(sampleCount + 1);
        for (int i = 0; i < sampleCount; i++)
        {
            float sampleDistance = sampleStepDistance * i;
            var sample = spline.EvaluateFromDistance(sampleDistance);
            splineSamplesOutput.Add(sample);
        }
        float lastSampledDistance = sampleStepDistance * (sampleCount - 1);
        if (lastSampledDistance < totalDistance)
        {
            var sample = spline.EvaluateFromDistance(totalDistance);
            splineSamplesOutput.Add(sample);
        }
    }

    /// <summary>
    /// Collect sample positions at fixed resolution per curve.
    /// </summary>
    /// <remarks>For closed loop, the final point will be the same as the first point in the list.</remarks>
    public static void CollectSplineSamplePoints(Spline spline, List<Vector3> splineSamplePointsOutput, int sampleResolutionPerCurve = 32)
    {
        int splineCurveCount = spline.CurveCount;
        if (splineCurveCount <= 0)
        {
            return;
        }

        splineSamplePointsOutput.EnsureCapacity(splineCurveCount * sampleResolutionPerCurve + 1);
        for (int curveIdx = 0; curveIdx < splineCurveCount; curveIdx++)
        {
            var curve = spline.GetCurve(curveIdx);
            // Note: we can skip the point at t = 1 because the next curve will be at the same position
            for (int i = 0; i < sampleResolutionPerCurve; i++)
            {
                float curveT = i / (float)sampleResolutionPerCurve;
                var position = curve.GetPosition(curveT);
                splineSamplePointsOutput.Add(position);
            }
        }

        var lastCurve = spline.GetCurve(splineCurveCount - 1);
        var lastPos = lastCurve.GetPosition(t: 1);
        splineSamplePointsOutput.Add(lastPos);
    }

    public static void ForEachSamplePoint(Spline spline, Action<Vector3> action, int sampleResolutionPerCurve = 32)
    {
        int splineCurveCount = spline.CurveCount;
        if (splineCurveCount <= 0)
        {
            return;
        }

        for (int curveIdx = 0; curveIdx < splineCurveCount; curveIdx++)
        {
            var curve = spline.GetCurve(curveIdx);
            // Note: we can skip the point at t = 1 because the next curve will be at the same position
            for (int i = 0; i < sampleResolutionPerCurve; i++)
            {
                float curveT = i / (float)sampleResolutionPerCurve;
                var position = curve.GetPosition(curveT);
                action(position);
            }
        }

        var lastCurve = spline.GetCurve(splineCurveCount - 1);
        var lastPos = lastCurve.GetPosition(t: 1);
        action(lastPos);
    }
}
