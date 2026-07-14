// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using System.Diagnostics;

namespace Stride.Engine.Splines.Models;

public static class SplineExtensions
{
    private readonly struct InternalSamplingSettings
    {
        public float MaximumPositionError { get; init; }
        public float MinimumAngleCosineError { get; init; }
        public float MaximumSegmentLength { get; init; }
        public float MinimumSegmentLength { get; init; }
        public int MaximumSubdivisionDepth { get; init; }
    }

    private readonly struct InternalSamplingContext
    {
        public SplineSample Sample0 { get; init; }
        public SplineSample Sample1 { get; init; }
        public int CurrentDepth { get; init; }
        public float TotalSplineDistance { get; init; }
    }

    /// <summary>
    /// Collect sample points adpatively.
    /// </summary>
    /// <remarks>For closed loop, the final sample point will overlap with the first sample point in the list.</remarks>
    public static void CollectSplineSamples(
        this ISplineEvaluator splineEvaluator, SplineSamplingSettings samplingSettings,
        List<SplineSample> splineSamplesOutput)
    {
        var spline = splineEvaluator.Spline;
        int splineCurveCount = spline.CurveCount;
        if (splineCurveCount <= 0)
        {
            return;
        }

        var settings = new InternalSamplingSettings
        {
            MaximumPositionError = Math.Max(samplingSettings.MaximumPositionError, MathUtil.ZeroTolerance),
            MinimumAngleCosineError = MathF.Cos(samplingSettings.MaximumAngleError.Radians),
            MaximumSegmentLength = Math.Max(samplingSettings.MaximumSegmentLength, MathUtil.ZeroTolerance),
            MinimumSegmentLength = Math.Max(samplingSettings.MinimumSegmentLength, MathUtil.ZeroTolerance),
            MaximumSubdivisionDepth = Math.Max(samplingSettings.MaximumSubdivisionDepth, 0),
        };

        var firstSample = splineEvaluator.Evaluate(splineT: 0);
        var lastSample = splineEvaluator.Evaluate(splineT: 1);
        var context = new InternalSamplingContext
        {
            Sample0 = firstSample,
            Sample1 = lastSample,
            CurrentDepth = 0,
            TotalSplineDistance = splineEvaluator.GetTotalDistance(),
        };
        splineSamplesOutput.EnsureCapacity(1 << Math.Max(settings.MaximumSubdivisionDepth - 1, 0));     // Estimate 2^(MaxSubdivision - 1)
        splineSamplesOutput.Add(firstSample);

        CollectSubdividedSamples(splineEvaluator, settings, context, splineSamplesOutput);
        PrintSamples(splineSamplesOutput);
    }

    [Conditional("DEBUG")]
    private static void PrintSamples(List<SplineSample> splineSamples)
    {
        Debug.WriteLine($"Spline\tSplineT\tLength\tX\tY\tZ");
        var prevPos = splineSamples.Count > 0 ? splineSamples[0].Position : Vector3.Zero;
        for (int i = 0; i < splineSamples.Count; i++)
        {
            var curPos = splineSamples[i].Position;
            float len = Vector3.Distance(prevPos, curPos);
            Debug.WriteLine($"{i}\t{splineSamples[i].SplineT}\t{len}\t{curPos.X}\t{curPos.Y}\t{curPos.Z}");
            prevPos = splineSamples[i].Position;
        }
        Debug.WriteLine($"Spline Output End");
    }

    private static void CollectSubdividedSamples(
        ISplineEvaluator splineEvaluator, in InternalSamplingSettings samplingSettings, InternalSamplingContext context,
        List<SplineSample> splineSamplesOutput)
    {
        if (context.CurrentDepth >= samplingSettings.MaximumSubdivisionDepth)
        {
            // Can't subdivide further
            splineSamplesOutput.Add(context.Sample1);
            return;
        }

        var sample0 = context.Sample0;
        var sample1 = context.Sample1;
        ref readonly var p0 = ref sample0.Position;
        ref readonly var p1 = ref sample1.Position;
        float segmentLength = (sample1.SplineT - sample0.SplineT) * context.TotalSplineDistance;
        if (segmentLength <= samplingSettings.MinimumSegmentLength)
        {
            // Can't subdivide further
            splineSamplesOutput.Add(context.Sample1);
            return;
        }

        float midT = (sample0.SplineT + sample1.SplineT) * 0.5f;
        var midSample = splineEvaluator.Evaluate(midT);

        bool isSplitRequired = false;
        if (segmentLength > samplingSettings.MaximumSegmentLength)
        {
            // Force subdivision on long segments
            isSplitRequired = true;
        }
        else
        {
            isSplitRequired = IsSplitOnMaxPositionError(samplingSettings, midSample.Position, p0, p1)
                || IsSplitOnMaxAngleError(samplingSettings, sample0.Tangent, sample1.Tangent);
        }

        if (isSplitRequired)
        {
            var leftContext = context with
            {
                Sample1 = midSample,
                CurrentDepth = context.CurrentDepth + 1
            };
            CollectSubdividedSamples(
                splineEvaluator, samplingSettings, leftContext,
                splineSamplesOutput);

            var rightContext = context with
            {
                Sample0 = midSample,
                CurrentDepth = context.CurrentDepth + 1
            };
            CollectSubdividedSamples(
                splineEvaluator, samplingSettings, rightContext,
                splineSamplesOutput);
        }
        else
        {
            splineSamplesOutput.Add(sample1);
        }

        static bool IsSplitOnMaxPositionError(in InternalSamplingSettings samplingSettings, in Vector3 midPointPosition, in Vector3 p0, in Vector3 p1)
        {
            var (closestPointOnLine, _) = SplineUtil.GetClosestPointOnLineSegment(midPointPosition, p0, p1);
            float positionDifference = Vector3.Distance(midPointPosition, closestPointOnLine);
            return positionDifference > samplingSettings.MaximumPositionError;
        }

        static bool IsSplitOnMaxAngleError(in InternalSamplingSettings samplingSettings, in Vector3 midPointTangent, in Vector3 lineDirection)
        {
            if (samplingSettings.MinimumAngleCosineError == 0)
            {
                // Not set
                return false;
            }
            float dotValue = Vector3.Dot(midPointTangent, lineDirection);
            float cosine = Math.Clamp(dotValue, min: -1, max: 1);
            return cosine < samplingSettings.MinimumAngleCosineError;
        }
    }

    /// <summary>
    /// Collect sample points at fixed distance.
    /// </summary>
    /// <remarks>For closed loop, the final sample point will overlap with the first sample point in the list.</remarks>
    public static void CollectSplineSamplesByDistance(this ISplineEvaluator splineEvaluator, List<SplineSample> splineSamplesOutput, float sampleStepDistance)
    {
        float totalDistance = splineEvaluator.GetTotalDistance();
        int sampleCount = (int)Math.Floor(totalDistance / sampleStepDistance);
        splineSamplesOutput.EnsureCapacity(sampleCount + 1);
        for (int i = 0; i < sampleCount; i++)
        {
            float sampleDistance = sampleStepDistance * i;
            var sample = splineEvaluator.EvaluateFromDistance(sampleDistance);
            splineSamplesOutput.Add(sample);
        }
        float lastSampledDistance = sampleStepDistance * (sampleCount - 1);
        if (lastSampledDistance < totalDistance)
        {
            var sample = splineEvaluator.EvaluateFromDistance(totalDistance);
            splineSamplesOutput.Add(sample);
        }
    }

    /// <summary>
    /// Collect sample points at fixed resolution per curve.
    /// </summary>
    /// <remarks>For closed loop, the final sample point will overlap with the first sample point in the list.</remarks>
    public static void CollectSplineSamplesByResolution(this ISplineEvaluator splineEvaluator, List<SplineSample> splineSamplesOutput, int sampleResolutionPerCurve = 32)
    {
        var spline = splineEvaluator.Spline;
        int splineCurveCount = spline.CurveCount;
        if (splineCurveCount <= 0)
        {
            return;
        }

        splineSamplesOutput.EnsureCapacity(splineCurveCount * sampleResolutionPerCurve + 1);
        for (int curveIdx = 0; curveIdx < splineCurveCount; curveIdx++)
        {
            // Note: we can skip the point at t = 1 because the next curve will be at the same position
            for (int i = 0; i < sampleResolutionPerCurve; i++)
            {
                float curveT = i / (float)sampleResolutionPerCurve;
                var sample = splineEvaluator.EvaluateFromCurve(curveIdx, curveT);
                splineSamplesOutput.Add(sample);
            }
        }

        int lastCurveIdx = splineCurveCount - 1;
        var lastSample = splineEvaluator.EvaluateFromCurve(lastCurveIdx, curveLocalT: 1);
        splineSamplesOutput.Add(lastSample);
    }

    /// <summary>
    /// Collect sample positions at fixed resolution per curve.
    /// </summary>
    /// <remarks>For closed loop, the final point will be the same as the first point in the list.</remarks>
    public static void CollectSplineSamplePositionsByResolution(this Spline spline, List<Vector3> splineSamplePositionsOutput, int sampleResolutionPerCurve = 32)
    {
        int splineCurveCount = spline.CurveCount;
        if (splineCurveCount <= 0)
        {
            return;
        }

        splineSamplePositionsOutput.EnsureCapacity(splineCurveCount * sampleResolutionPerCurve + 1);
        for (int curveIdx = 0; curveIdx < splineCurveCount; curveIdx++)
        {
            var curve = spline.GetCurve(curveIdx);
            // Note: we can skip the point at t = 1 because the next curve will be at the same position
            for (int i = 0; i < sampleResolutionPerCurve; i++)
            {
                float curveT = i / (float)sampleResolutionPerCurve;
                var position = curve.GetPosition(curveT);
                splineSamplePositionsOutput.Add(position);
            }
        }

        var lastCurve = spline.GetCurve(splineCurveCount - 1);
        var lastPos = lastCurve.GetPosition(t: 1);
        splineSamplePositionsOutput.Add(lastPos);
    }

    public static void ForEachSamplePoint(this Spline spline, Action<Vector3> action, int sampleResolutionPerCurve = 32)
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

    public static bool TryGetPreviousControlPointIndex(this Spline spline, int currentIndex, out int previousIndex)
    {
        if (spline.IsClosedLoop)
        {
            previousIndex = currentIndex - 1;
            while (previousIndex < 0)
            {
                previousIndex += spline.Count;
            }
            return true;
        }

        previousIndex = currentIndex - 1;
        if (previousIndex >= 0)
        {
            return true;
        }

        previousIndex = -1;
        return false;
    }

    public static bool TryGetNextControlPointIndex(this Spline spline, int currentIndex, out int nextIndex)
    {
        if (spline.IsClosedLoop)
        {
            nextIndex = (currentIndex + 1) % spline.Count;
            return true;
        }

        nextIndex = currentIndex + 1;
        if (nextIndex < spline.Count)
        {
            return true;
        }

        nextIndex = -1;
        return false;
    }
}
