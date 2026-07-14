// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

[DataContract]
public class SplineEvaluator : ISplineEvaluator
{
    private readonly List<CurveTToDistanceTable> curveTToDistanceLookupTable = [];
    private readonly List<CurveRange> curveRangeTable = [];

    private bool isRebuildLookupTableRequired = true;

    private float maximumPositionError = 0.05f;
    [DefaultValue(0.05f)]
    public float MaximumPositionError
    {
        get => maximumPositionError;
        set => SetField(ref maximumPositionError, value);
    }

    private float maximumArcLengthError = 0.01f;
    [DefaultValue(0.01f)]
    public float MaximumArcLengthError
    {
        get => maximumArcLengthError;
        set => SetField(ref maximumArcLengthError, value);
    }

    private AngleSingle maximumAngleError = new AngleSingle(3f, AngleType.Degree);
    public AngleSingle MaximumAngleError
    {
        get => maximumAngleError;
        set => SetField(ref maximumAngleError, value);
    }

    private float maximumSegmentLength = 1;
    [DefaultValue(1f)]
    public float MaximumSegmentLength
    {
        get => maximumSegmentLength;
        set => SetField(ref maximumSegmentLength, value);
    }

    private float minimumSegmentLength = 0.01f;
    [DefaultValue(0.01f)]
    public float MinimumSegmentLength
    {
        get => minimumSegmentLength;
        set => SetField(ref minimumSegmentLength, value);
    }

    private int maximumSubdivisionDepth = 8;
    [DefaultValue(8)]
    public int MaximumSubdivisionDepth
    {
        get => maximumSubdivisionDepth;
        set => SetField(ref maximumSubdivisionDepth, value);
    }

    protected void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        isRebuildLookupTableRequired = true;
    }

    public Spline Spline { get; private set; }

    public SplineEvaluator() { }

    public SplineEvaluator(Spline spline)
    {
        RegisterSpline(spline);
    }

    public void RegisterSpline(Spline spline)
    {
        if (Spline == spline)
        {
            return;
        }
        if (Spline is not null)
        {
            UnregisterSpline();
        }
        Spline = spline;
        Spline.SplinePropertyChanged += OnSplinePropertyChanged;
        Spline.ControlPointsChanged += OnControlPointsChanged;
    }

    public void UnregisterSpline()
    {
        Spline?.SplinePropertyChanged -= OnSplinePropertyChanged;
        Spline?.ControlPointsChanged -= OnControlPointsChanged;
        Spline = null;
    }

    private void OnSplinePropertyChanged(object sender)
    {
        isRebuildLookupTableRequired = true;
    }

    private void OnControlPointsChanged(object sender, ref SplineControlPointsChangedEventArgs e)
    {
        isRebuildLookupTableRequired = true;
    }

    public float GetTotalDistance()
    {
        EnsureLookupTables();
        return curveTToDistanceLookupTable[^1].TotalSplineDistance;
    }

    public float GetDistanceFromT(float splineT)
    {
        float clampedSplineT = Math.Clamp(splineT, min: 0, max: 1);
        float totalDistance = GetTotalDistance();
        float distance = clampedSplineT * totalDistance;
        return distance;
    }

    public float GetTFromDistance(float distance)
    {
        float totalDistance = GetTotalDistance();
        float splineT = distance / totalDistance;
        return splineT;
    }

    public SplineSample Evaluate(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        ref readonly var nextTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        var orientation = Quaternion.Slerp(curTableValue.Orientation, nextTableValue.Orientation, lookupResult.LookupTableLocalT);
        var tangent = orientation * Vector3.UnitZ;
        return new SplineSample(position, orientation, tangent, splineT);
    }

    public SplineSample EvaluateFromDistance(float distance)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromDistance(curveTToDistanceLookupTableSpan, distance);

        ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        ref readonly var nextTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        var orientation = Quaternion.Slerp(curTableValue.Orientation, nextTableValue.Orientation, lookupResult.LookupTableLocalT);
        var tangent = orientation * Vector3.UnitZ;

        float splineT = GetTFromDistance(distance);
        return new SplineSample(position, orientation, tangent, splineT);
    }

    public SplineSample EvaluateFromCurve(int curveIndex, float curveLocalT = 0)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);

        if (curveLocalT == 0)
        {
            int tableIndex = FindTableValueStartIndexByControlPoint(curveTToDistanceLookupTableSpan, curveIndex, curveLocalT: 0);
            ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[tableIndex];

            var position = curTableValue.Position;
            var orientation = curTableValue.Orientation;
            var tangent = curTableValue.Tangent;

            float splineT = GetTFromDistance(curTableValue.TotalSplineDistance);
            return new SplineSample(position, orientation, tangent, splineT);
        }
        else
        {
            var lookupResult = FindTableValueFromControlPoint(curveTToDistanceLookupTableSpan, curveIndex, curveLocalT);

            ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
            ref readonly var nextTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

            var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
            var orientation = Quaternion.Slerp(curTableValue.Orientation, nextTableValue.Orientation, lookupResult.LookupTableLocalT);
            var tangent = orientation * Vector3.UnitZ;

            float distance = MathUtil.Lerp(curTableValue.TotalSplineDistance, nextTableValue.TotalSplineDistance, lookupResult.LookupTableLocalT);
            float splineT = GetTFromDistance(distance);
            return new SplineSample(position, orientation, tangent, splineT);
        }
    }

    public Vector3 EvaluatePosition(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        ref readonly var nextTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        return position;
    }

    public Quaternion EvaluateOrientation(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        ref readonly var curTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        ref readonly var nextTableValue = ref curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var orientation = Quaternion.Slerp(curTableValue.Orientation, nextTableValue.Orientation, lookupResult.LookupTableLocalT);
        return orientation;
    }

    public Vector3 EvaluateTangent(float splineT)
    {
        var orientation = EvaluateOrientation(splineT);
        var tangent = orientation * Vector3.UnitZ;
        return tangent;
    }

    public SplineClosestPositionInfo FindClosestPoint(Vector3 position)
    {
        if (Spline.ControlPoints.Count == 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }
        EnsureLookupTables();

        int closestSampleIndex = -1;
        float minDistSqrd = float.MaxValue;
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        // Find closest lookupTable table point for coarse estimate
        for (int i = 0; i < curveTToDistanceLookupTableSpan.Length - 1; i++)
        {
            var tablePosition = curveTToDistanceLookupTableSpan[i].Position;
            float distSqrd = Vector3.DistanceSquared(tablePosition, position);
            if (minDistSqrd > distSqrd)
            {
                closestSampleIndex = i;
                minDistSqrd = distSqrd;
            }
        }

        // Refine search by curve projection with neighboring points
        int prevSampleIndex = closestSampleIndex - 1;
        int nextSampleIndex = closestSampleIndex + 1;
        // Last sample point is the same point as first sample point when spline is closed loop so skip over it, otherwise clamp it
        if (prevSampleIndex < 0)
        {
            prevSampleIndex = Spline.IsClosedLoop ? curveTToDistanceLookupTableSpan.Length - 2 : 0;
        }
        if (nextSampleIndex >= curveTToDistanceLookupTableSpan.Length)
        {
            nextSampleIndex = Spline.IsClosedLoop ? 1 : curveTToDistanceLookupTableSpan.Length - 1;
        }

        ref var prevTableValue = ref curveTToDistanceLookupTableSpan[prevSampleIndex];
        ref var curTableValue = ref curveTToDistanceLookupTableSpan[closestSampleIndex];
        ref var nextTableValue = ref curveTToDistanceLookupTableSpan[nextSampleIndex];

        var prevTableProjection = ProjectPointOntoCurve(position, in prevTableValue, in curTableValue);
        var curTableProjection = ProjectPointOntoCurve(position, in curTableValue, in nextTableValue);
        float prevTableProjDistSqrd = (position - prevTableProjection.Position).LengthSquared();
        float curTableProjDistSqrd = (position - curTableProjection.Position).LengthSquared();

        float totalDistance = GetTotalDistance();
        int curveIndex;
        float curveLocalT;
        float splineDist;
        if (prevTableProjDistSqrd <= curTableProjDistSqrd)
        {
            curveIndex = prevTableValue.CurveIndex;
            curveLocalT = prevTableProjection.LocalT;
            if (prevSampleIndex > closestSampleIndex)
            {
                // Edge case: looped back
                splineDist = MathUtil.Lerp(prevTableValue.TotalSplineDistance, totalDistance, curveLocalT);
            }
            else
            {
                splineDist = MathUtil.Lerp(prevTableValue.TotalSplineDistance, curTableValue.TotalSplineDistance, curveLocalT);
            }
        }
        else
        {
            curveIndex = curTableValue.CurveIndex;
            curveLocalT = curTableProjection.LocalT;
            if (closestSampleIndex > nextSampleIndex)
            {
                // Edge case: looped back
                splineDist = MathUtil.Lerp(curTableValue.TotalSplineDistance, totalDistance, curveLocalT);
            }
            else
            {
                splineDist = MathUtil.Lerp(curTableValue.TotalSplineDistance, nextTableValue.TotalSplineDistance, curveLocalT);
            }
        }

        int startControlPointIndex = curveIndex;      // Curve tableIndex is the same as control point tableIndex
        int nextControlPointIndex = Spline.IsClosedLoop
            ? (startControlPointIndex + 1) % Spline.Count
            : Math.Max(startControlPointIndex + 1, Spline.Count - 1);
        var curve = Spline.GetCurve(curveIndex);
        var closestPosInfo = new SplineClosestPositionInfo
        {
            Position = curve.GetPosition(curveLocalT),
            SplineControlPointAIndex = startControlPointIndex,
            SplineControlPointBIndex = nextControlPointIndex,
            LocalT = curveLocalT,
            SplineDistance = Math.Clamp(splineDist, min: 0, max: totalDistance)
        };
        return closestPosInfo;

        static (Vector3 Position, float LocalT) ProjectPointOntoCurve(
            in Vector3 point,
            in CurveTToDistanceTable value1,
            in CurveTToDistanceTable value2)
        {
            var (closestPointOnLine, tableT) = SplineUtil.GetClosestPointOnLineSegment(point, value1.Position, value2.Position);
            float nextLocalT = value2.CurveLocalT;
            // Edge case check: second table value is the start of the next control point
            if (value1.CurveIndex != value2.CurveIndex)
            {
                nextLocalT = 1;
            }
            float curveLocalT = MathUtil.Lerp(value1.CurveLocalT, nextLocalT, tableT);
            return (closestPointOnLine, curveLocalT);
        }
    }

    public BoundingBox CalculateBoundingBox()
    {
        EnsureLookupTables();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        for (int i = 0; i < curveTToDistanceLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref curveTToDistanceLookupTableSpan[i];
            Vector3.Min(ref min, ref tableValue.Position, out min);
            Vector3.Max(ref max, ref tableValue.Position, out max);
        }

        return new BoundingBox(min, max);
    }

    private static int FindTableValueStartIndexByDistance(
        ReadOnlySpan<CurveTToDistanceTable> lookupTable,
        float distance)
    {
        var lookupValue = new CurveTToDistanceTable
        {
            TotalSplineDistance = distance,
            CurveIndex = int.MaxValue,                // Note: we priority the NEXT curve for the same distance
            CurveLocalT = float.MaxValue,
            // Defaulted fields - not used for binary search
            Position = default,
            Tangent = default,
            Orientation = default,
        };
        int index = lookupTable.BinarySearch(lookupValue, DistanceLookupComparer.Instance);
        if (index < 0)
        {
            int closestIndex = ~index;
            closestIndex = Math.Max(closestIndex - 1, 0);
            return closestIndex;
        }
        return index;
    }

    private static int FindTableValueStartIndexByControlPoint(
        ReadOnlySpan<CurveTToDistanceTable> lookupTable,
        int controlPointIndex, float curveLocalT)
    {
        var lookupValue = new CurveTToDistanceTable
        {
            CurveIndex = controlPointIndex,
            CurveLocalT = curveLocalT,
            // Defaulted fields - not used for binary search
            TotalSplineDistance = default,
            Position = default,
            Tangent = default,
            Orientation = default,
        };
        int index = lookupTable.BinarySearch(lookupValue, ControlPointLookupComparer.Instance);
        if (index < 0)
        {
            int closestIndex = ~index;
            closestIndex = Math.Max(closestIndex - 1, 0);
            return closestIndex;
        }
        return index;
    }

    private LookupTableResult FindTableValueFromT(
       ReadOnlySpan<CurveTToDistanceTable> curveTToDistanceLookupTable,
       float splineT)
    {
        float targetDistance = GetDistanceFromT(splineT);
        var result = FindTableValueFromDistance(curveTToDistanceLookupTable, targetDistance);
        return result;
    }

    private static LookupTableResult FindTableValueFromDistance(
        ReadOnlySpan<CurveTToDistanceTable> curveTToDistanceLookupTable,
        float targetDistance)
    {
        int tableIndex = FindTableValueStartIndexByDistance(curveTToDistanceLookupTable, targetDistance);

        ref readonly var curTableValue = ref curveTToDistanceLookupTable[tableIndex];
        int nextTableIndex = Math.Min(tableIndex + 1, curveTToDistanceLookupTable.Length - 1);
        ref readonly var nextTableValue = ref curveTToDistanceLookupTable[nextTableIndex];

        float curveLength = nextTableValue.TotalSplineDistance - curTableValue.TotalSplineDistance;
        if (MathUtil.IsZero(curveLength))
        {
            // Same point
            var result = new LookupTableResult
            {
                CurveIndex = curTableValue.CurveIndex,
                CurveLocalT = curTableValue.CurveLocalT,
                LookupTableIndex = tableIndex,
                NextLookupTableIndex = nextTableIndex,
                LookupTableLocalT = 0,
            };
            return result;
        }
        else
        {
            float lookupTableLocalT = (targetDistance - curTableValue.TotalSplineDistance) / curveLength;
            float nextLocalT = nextTableValue.CurveLocalT;
            // Edge case check: Next table value is actually start of next curve
            if (curTableValue.CurveIndex != nextTableValue.CurveIndex)
            {
                nextLocalT = 1;
            }
            var result = new LookupTableResult
            {
                CurveIndex = curTableValue.CurveIndex,
                CurveLocalT = MathUtil.Lerp(curTableValue.CurveLocalT, nextLocalT, lookupTableLocalT),
                LookupTableIndex = tableIndex,
                NextLookupTableIndex = nextTableIndex,
                LookupTableLocalT = lookupTableLocalT,
            };
            return result;
        }
    }

    private static LookupTableResult FindTableValueFromControlPoint(
        ReadOnlySpan<CurveTToDistanceTable> curveTToDistanceLookupTable,
        int controlPointIndex, float curveLocalT)
    {
        int tableIndex = FindTableValueStartIndexByControlPoint(curveTToDistanceLookupTable, controlPointIndex, curveLocalT);

        ref readonly var curTableValue = ref curveTToDistanceLookupTable[tableIndex];
        int nextTableIndex = Math.Min(tableIndex + 1, curveTToDistanceLookupTable.Length - 1);
        ref readonly var nextTableValue = ref curveTToDistanceLookupTable[nextTableIndex];

        float curveLength = nextTableValue.TotalSplineDistance - curTableValue.TotalSplineDistance;
        if (MathUtil.IsZero(curveLength))
        {
            // Same point
            var result = new LookupTableResult
            {
                CurveIndex = curTableValue.CurveIndex,
                CurveLocalT = curTableValue.CurveLocalT,
                LookupTableIndex = tableIndex,
                NextLookupTableIndex = nextTableIndex,
                LookupTableLocalT = 0,
            };
            return result;
        }
        else
        {
            float nextLocalT = nextTableValue.CurveLocalT;
            // Edge case check:
            if (curTableValue.CurveIndex != nextTableValue.CurveIndex)
            {
                nextLocalT = 1;
            }
            float lookupTableLocalT = MathUtil.InverseLerp(curTableValue.CurveLocalT, nextLocalT, curveLocalT);
            var result = new LookupTableResult
            {
                CurveIndex = curTableValue.CurveIndex,
                CurveLocalT = curveLocalT,
                LookupTableIndex = tableIndex,
                NextLookupTableIndex = nextTableIndex,
                LookupTableLocalT = lookupTableLocalT,
            };
            return result;
        }
    }

    public void CollectEncounteredControlPoints(float startSplineDistance, float endSplineDistance, List<int> controlPointIndicesEncounteredOutput)
    {
        EnsureLookupTables();
        float totalDistance = GetTotalDistance();
        if (totalDistance <= 0 || curveRangeTable.Count <= 0)
        {
            return;
        }

        if (!Spline.IsClosedLoop)
        {
            startSplineDistance = MathUtil.Clamp(startSplineDistance, min: 0, max: totalDistance);
            endSplineDistance = MathUtil.Clamp(endSplineDistance, min: 0, max: totalDistance);
        }

        bool isMovingForward = endSplineDistance > startSplineDistance;
        float splineDistance = startSplineDistance;
        if (!isMovingForward)
        {
            Utilities.Swap(ref startSplineDistance, ref endSplineDistance);
            while (startSplineDistance < 0)
            {
                // Keep incrementing until we sit within [0...totalDistance) range.
                startSplineDistance += totalDistance;
                endSplineDistance += totalDistance;
            }
        }

        int lastEncounteredControlPointIndex = -1;
        // Find initial curve this spline sits on
        var curveRangeTableSpan = CollectionsMarshal.AsSpan(curveRangeTable);
        int curveStartIndex = FindCurveRangeIndexFromDistance(curveRangeTableSpan, startSplineDistance);
        lastEncounteredControlPointIndex = curveStartIndex;    // Curve index is the same as the control point index

        // Collect all control points we passed
        float curEndSplineDistance = endSplineDistance;
        while (true)
        {
            // Find end curve this spline sits on
            for (int i = curveStartIndex; i < curveRangeTableSpan.Length; i++)
            {
                int controlPointIndex = i;      // Curve index is the same as the control point index
                if (lastEncounteredControlPointIndex != controlPointIndex)
                {
                    controlPointIndicesEncounteredOutput.Add(controlPointIndex);
                    lastEncounteredControlPointIndex = controlPointIndex;
                }
                if (curEndSplineDistance < curveRangeTableSpan[i].EndDistance)
                {
                    break;
                }
            }

            if (Spline.IsClosedLoop)
            {
                if (curEndSplineDistance > totalDistance)
                {
                    curEndSplineDistance -= totalDistance;
                    curveStartIndex = 0;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }

        if (!isMovingForward && controlPointIndicesEncounteredOutput.Count > 1)
        {
            controlPointIndicesEncounteredOutput.Reverse();
        }
    }

    private static int FindCurveRangeIndexFromDistance(
        ReadOnlySpan<CurveRange> curveRangeTable,
        float targetDistance)
    {
        var lookupValue = new CurveRange
        {
            StartDistance = targetDistance,
            EndDistance = targetDistance,
            // Defaulted fields - not used for binary search
            CurveIndex = default,
        };
        int index = curveRangeTable.BinarySearch(lookupValue, CurveRangeLookupComparer.Instance);
        if (index < 0)
        {
            int closestIndex = ~index;
            return closestIndex;
        }
        return index;
    }

    private void EnsureLookupTables()
    {
        if (!isRebuildLookupTableRequired)
        {
            return;
        }

        // Build CurveT to Distance table
        curveTToDistanceLookupTable.Clear();

        int totalCurveCount = Spline.CurveCount;
        if (totalCurveCount < 1)
        {
            isRebuildLookupTableRequired = false;
            return;     // Nothing to build
        }

        curveTToDistanceLookupTable.EnsureCapacity(1 << Math.Max(MaximumSubdivisionDepth - 1, 0));     // Estimate 2^(MaxSubdivision - 1)
        for (int curveIndex = 0; curveIndex < totalCurveCount; curveIndex++)
        {
            int tableValueStartIndex = curveTToDistanceLookupTable.Count;
            var curve = Spline.GetCurve(curveIndex);
            BuildTableValuesFromGeometry(curve, curveIndex, curveTToDistanceLookupTable);

            if (tableValueStartIndex > 0)
            {
                var startTableValue = curveTToDistanceLookupTable[tableValueStartIndex];
                var prevTableValue = curveTToDistanceLookupTable[tableValueStartIndex - 1];
                if (startTableValue.Position == prevTableValue.Position && startTableValue.Tangent == prevTableValue.Tangent)
                {
                    // Remove duplicate point as it is continuous
                    curveTToDistanceLookupTable.RemoveAt(tableValueStartIndex - 1);
                }
            }
        }
        // Note that we don't remove 'duplicate' point for closed loops since the final value also represents the full distance

        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        UpdateCurveDistances(curveTToDistanceLookupTableSpan);

        // Build orientations with Parallel Transport algorithm
        var previousTangentDir = curveTToDistanceLookupTableSpan[0].Tangent;
        var previousUpDir = Vector3.UnitY;
        {
            // Assign the correct initial 'up' direction
            var initialCtrlPoint = Spline[0];
            var initialTangentDir = previousTangentDir;
            var initialUpDir = previousUpDir;
            if (!initialCtrlPoint.OverrideUpDirection.Equals(Vector3.Zero))
            {
                initialUpDir = initialCtrlPoint.OverrideUpDirection;
            }
            else if (!Spline.InitialUpDirection.Equals(Vector3.Zero))
            {
                initialUpDir = Spline.InitialUpDirection;
            }
            initialUpDir = GetOrthogonalUpVector(initialUpDir, initialTangentDir);
            previousUpDir = initialUpDir;

            var initialUpDirWithRoll = ApplyRoll(initialTangentDir, initialUpDir, initialCtrlPoint.Roll);
            var initialOrientation = Quaternion.LookRotation(initialTangentDir, initialUpDirWithRoll);

            ref var tableValue = ref curveTToDistanceLookupTableSpan[0];
            tableValue.Orientation = initialOrientation;
        }
        for (int i = 1; i < curveTToDistanceLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref curveTToDistanceLookupTableSpan[i];

            float currentT = tableValue.CurveLocalT;
            int controlPointIndex = tableValue.CurveIndex;      // Curve index is the same as the control point index
            var currentCtrlPoint = Spline[controlPointIndex];

            var currentTangentDir = tableValue.Tangent;
            var curveRotation = GetOrientationRotation(previousTangentDir, currentTangentDir, previousUpDir);
            var currentUpDir = curveRotation * previousUpDir;

            var nextCtrlPoint = GetNextControlPoint(Spline, controlPointIndex);
            if (!nextCtrlPoint.OverrideUpDirection.Equals(Vector3.Zero))
            {
                var targetUp = GetOrthogonalUpVector(nextCtrlPoint.OverrideUpDirection, currentTangentDir);
                float blendWeight = MathUtil.SmoothStep(currentT);
                currentUpDir = GetVectorSlerp(currentUpDir, targetUp, blendWeight, -currentTangentDir);
            }
            currentUpDir = GetOrthogonalUpVector(currentUpDir, currentTangentDir);
            var currentUpDirWithRoll = currentUpDir;
            if (!MathUtil.IsZero(nextCtrlPoint.Roll.Radians) || !MathUtil.IsZero(currentCtrlPoint.Roll.Radians))
            {
                float blendedRollRadians = MathUtil.Lerp(currentCtrlPoint.Roll.Radians, nextCtrlPoint.Roll.Radians, currentT);
                currentUpDirWithRoll = ApplyRoll(currentTangentDir, currentUpDir, new AngleSingle(blendedRollRadians, AngleType.Radian));
            }
            var currentOrientation = Quaternion.LookRotation(currentTangentDir, currentUpDirWithRoll);
            tableValue.Orientation = currentOrientation;

            previousTangentDir = currentTangentDir;
            previousUpDir = currentUpDir;
        }

        if (Spline.IsClosedLoop)
        {
            // Holonomy loop correction - ensure the orientation at the end of the loop matches the start's
            ref var tableValueStart = ref curveTToDistanceLookupTableSpan[0];
            ref var tableValueEnd = ref curveTToDistanceLookupTableSpan[^1];

            var rotationDiff = tableValueStart.Orientation * Quaternion.Invert(tableValueEnd.Orientation);
            float angleDiff = rotationDiff.Angle;

            if (!MathUtil.IsZero(angleDiff))
            {
                // Ensure this is the shortest rotation
                if (angleDiff > MathUtil.Pi || angleDiff < -MathUtil.Pi)
                {
                    rotationDiff = -rotationDiff;
                }

                // If there are any user overridden Up directions, we need to start the correction after it
                int lastExplicitUpControlPointIndex = 0;
                for (int i = Spline.Count - 1; i > 0; i--)
                {
                    var ctrlPoint = Spline[i];
                    if (!ctrlPoint.OverrideUpDirection.Equals(Vector3.Zero))
                    {
                        lastExplicitUpControlPointIndex = i;
                        break;
                    }
                }

                int tableIndex = FindTableValueStartIndexByControlPoint(curveTToDistanceLookupTableSpan, lastExplicitUpControlPointIndex, curveLocalT: 0);
                int tableValueCorrectionStartIndex = tableIndex + 1;
                int tableValueCorrectionCount = curveTToDistanceLookupTableSpan.Length - tableValueCorrectionStartIndex;

                for (int i = 1; i <= tableValueCorrectionCount; i++)
                {
                    float slerpT = i / (float)tableValueCorrectionCount;
                    var correctionRotation = Quaternion.Slerp(Quaternion.Identity, rotationDiff, slerpT);

                    ref var tableValue = ref curveTToDistanceLookupTableSpan[i];
                    tableValue.Orientation = correctionRotation * tableValue.Orientation;
                }
            }
        }

        // Build Curve Range table
        curveRangeTable.Clear();
        var nextCurveRange = new CurveRange
        {
            CurveIndex = 0,
            StartDistance = 0,
        };
        for (int i = 0; i < curveTToDistanceLookupTableSpan.Length; i++)
        {
            int curCurveIndex = curveTToDistanceLookupTableSpan[i].CurveIndex;
            if (curCurveIndex != nextCurveRange.CurveIndex)
            {
                float curDist = curveTToDistanceLookupTableSpan[i].TotalSplineDistance;
                nextCurveRange.EndDistance = curDist;
                curveRangeTable.Add(nextCurveRange);
                nextCurveRange = new CurveRange
                {
                    CurveIndex = curCurveIndex,
                    StartDistance = curDist,
                };
            }
        }
        // Add last range
        nextCurveRange.EndDistance = curveTToDistanceLookupTableSpan[^1].TotalSplineDistance;
        curveRangeTable.Add(nextCurveRange);

        isRebuildLookupTableRequired = false;
    }

    private void BuildTableValuesFromGeometry(BezierCurve curve, int curveIndex, List<CurveTToDistanceTable> tableValuesOutput)
    {
        var settings = new TableSubdivisionSettings
        {
            MaximumPositionError = Math.Max(MaximumPositionError, MathUtil.ZeroTolerance),
            MinimumAngleCosineError = MathF.Cos(MaximumAngleError.Radians),
            MaximumSegmentLength = Math.Max(MaximumSegmentLength, MathUtil.ZeroTolerance),
            MinimumSegmentLength = Math.Max(MinimumSegmentLength, MathUtil.ZeroTolerance),
            MaximumSubdivisionDepth = Math.Max(MaximumSubdivisionDepth, 0),
        };

        var fallbackTangent = Vector3.UnitX;        // Arbitrary default, pick 'right' direction
        var firstTableValue = EvaluateCurveValue(curve, curveIndex, curveT: 0, fallbackTangent);
        var lastTableValue = EvaluateCurveValue(curve, curveIndex, curveT: 1, fallbackTangent);
        var context = new TableSubdivisionContext
        {
            CurveIndex = curveIndex,
            TableValue0 = firstTableValue,
            TableValue1 = lastTableValue,
            CurrentDepth = 0,
        };
        tableValuesOutput.EnsureCapacity(1 << Math.Max(settings.MaximumSubdivisionDepth - 1, 0));     // Estimate 2^(MaxSubdivision - 1)
        tableValuesOutput.Add(firstTableValue);

        CollectSubdividedTableValues(curve, settings, context, tableValuesOutput);
    }

    private static CurveTToDistanceTable EvaluateCurveValue(BezierCurve curve, int curveIndex, float curveT, Vector3 previousTangent)
    {
        var value = new CurveTToDistanceTable
        {
            CurveIndex = curveIndex,
            CurveLocalT = curveT,
            Position = curve.GetPosition(curveT),
            Tangent = curve.GetTangent(curveT),
            // Not set yet
            TotalSplineDistance = 0,
            Orientation = Quaternion.Identity,
        };
        if (value.Tangent.Equals(Vector3.Zero))
        {
            value.Tangent = previousTangent;
        }
        return value;
    }

    private static void CollectSubdividedTableValues(
        BezierCurve curve, in TableSubdivisionSettings subdivisionSettings, TableSubdivisionContext context,
        List<CurveTToDistanceTable> tableValuesOutput)
    {
        if (context.CurrentDepth >= subdivisionSettings.MaximumSubdivisionDepth)
        {
            // Can't subdivide further
            tableValuesOutput.Add(context.TableValue1);
            return;
        }

        var tableValue0 = context.TableValue0;
        var tableValue1 = context.TableValue1;
        ref readonly var p0 = ref tableValue0.Position;
        ref readonly var p1 = ref tableValue1.Position;
        float segmentLength = Vector3.Distance(p0, p1);
        if (segmentLength <= subdivisionSettings.MinimumSegmentLength)
        {
            // Can't subdivide further
            tableValuesOutput.Add(context.TableValue1);
            return;
        }

        float midT = (tableValue0.CurveLocalT + tableValue1.CurveLocalT) * 0.5f;
        var midTableValue = EvaluateCurveValue(curve, context.CurveIndex, midT, tableValue0.Tangent);

        bool isSplitRequired = false;
        if (segmentLength > subdivisionSettings.MaximumSegmentLength)
        {
            // Force subdivision on long segments
            isSplitRequired = true;
        }
        else
        {
            isSplitRequired = IsSplitOnMaxPositionError(subdivisionSettings, midTableValue.Position, p0, p1)
                || IsSplitOnMaxArcLengthError(subdivisionSettings, midTableValue.Position, p0, p1)
                || IsSplitOnMaxAngleError(subdivisionSettings, midTableValue.Tangent, Vector3.Normalize(p1 - p0));
        }

        if (isSplitRequired)
        {
            var leftContext = context with
            {
                TableValue1 = midTableValue,
                CurrentDepth = context.CurrentDepth + 1
            };
            CollectSubdividedTableValues(
                curve, subdivisionSettings, leftContext,
                tableValuesOutput);

            var rightContext = context with
            {
                TableValue0 = midTableValue,
                CurrentDepth = context.CurrentDepth + 1
            };
            CollectSubdividedTableValues(
                curve, subdivisionSettings, rightContext,
                tableValuesOutput);
        }
        else
        {
            tableValuesOutput.Add(tableValue1);
        }

        static bool IsSplitOnMaxPositionError(in TableSubdivisionSettings samplingSettings, in Vector3 midPointPosition, in Vector3 p0, in Vector3 p1)
        {
            var (closestPointOnLine, _) = SplineUtil.GetClosestPointOnLineSegment(midPointPosition, p0, p1);
            float positionDifference = Vector3.Distance(midPointPosition, closestPointOnLine);
            return positionDifference > samplingSettings.MaximumPositionError;
        }

        static bool IsSplitOnMaxArcLengthError(in TableSubdivisionSettings samplingSettings, in Vector3 midPointPosition, in Vector3 p0, in Vector3 p1)
        {
            float chordLength = Vector3.Distance(p0, p1);
            float curveLength = Vector3.Distance(p0, midPointPosition) + Vector3.Distance(midPointPosition, p1);

            float lengthDifference = curveLength - chordLength;
            return lengthDifference > samplingSettings.MaximumArcLengthError;
        }

        static bool IsSplitOnMaxAngleError(in TableSubdivisionSettings samplingSettings, in Vector3 midPointTangent, in Vector3 lineDirection)
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

    private static void UpdateCurveDistances(Span<CurveTToDistanceTable> curveTToDistanceLookupTableSpan)
    {
        if (curveTToDistanceLookupTableSpan.Length <= 0)
        {
            return;
        }

        curveTToDistanceLookupTableSpan[0].TotalSplineDistance = 0;

        var previousPos = curveTToDistanceLookupTableSpan[0].Position;
        float currentTotalCurveDistance = 0;
        for (int i = 1; i < curveTToDistanceLookupTableSpan.Length; i++)
        {
            var currentPos = curveTToDistanceLookupTableSpan[i].Position;
            currentTotalCurveDistance += Vector3.Distance(currentPos, previousPos);
            curveTToDistanceLookupTableSpan[i].TotalSplineDistance = currentTotalCurveDistance;

            previousPos = currentPos;
        }
    }

    private static Vector3 GetVectorSlerp(Vector3 fromDirection, Vector3 toDirection, float slerpT, in Vector3 upDirection)
    {
        float dotValue = Vector3.Dot(fromDirection, toDirection);

        if (MathUtil.IsZero(dotValue + 1f))
        {
            // The two vectors are in the opposite directions, so rotate 180 degrees around the given up-axis
            var rotation = Quaternion.RotationAxis(upDirection, MathUtil.Pi * slerpT);
            var rotatedVec = rotation * fromDirection;
            return rotatedVec;
        }
        else if (MathUtil.IsZero(dotValue - 1f))
        {
            // Same direction
            return toDirection;
        }

        float rotAngle = MathF.Acos(dotValue);
        float sinTheta = MathF.Sin(rotAngle);

        float a = MathF.Sin((1f - slerpT) * rotAngle) / sinTheta;
        float b = MathF.Sin(slerpT * rotAngle) / sinTheta;

        var slerpedVec = fromDirection * a + toDirection * b;
        return slerpedVec;
    }

    private static Quaternion GetOrientationRotation(Vector3 fromDirection, Vector3 toDirection, Vector3 upDirection)
    {
        float dotValue = Vector3.Dot(fromDirection, toDirection);

        if (MathUtil.IsZero(dotValue + 1f))
        {
            // The two vectors are in the opposite directions, so rotate 180 degrees around the given up-axis
            return Quaternion.RotationAxis(upDirection, MathUtil.Pi);
        }
        else if (MathUtil.IsZero(dotValue - 1f))
        {
            // Same direction
            return Quaternion.Identity;
        }

        float rotAngle = MathF.Acos(dotValue);
        var rotAxis = Vector3.Cross(fromDirection, toDirection);
        rotAxis.Normalize();
        return Quaternion.RotationAxis(rotAxis, rotAngle);
    }

    private static SplineControlPoint GetNextControlPoint(Spline spline, int controlPointIndex)
    {
        int nextCtrlPointIndex;
        if (spline.IsClosedLoop)
        {
            nextCtrlPointIndex = (controlPointIndex + 1) % spline.Count;
        }
        else
        {
            nextCtrlPointIndex = Math.Min(controlPointIndex + 1, spline.Count - 1);
        }
        return spline[nextCtrlPointIndex];
    }

    private static Vector3 GetOrthogonalUpVector(Vector3 upDir, Vector3 tangentDir)
    {
        // Ensure the up direction is orthogonal to the tangent by treating tangent direction as the normal of a plane
        // then make the up vector sit direction on the plane.
        var plane = new Plane(Vector3.Zero, tangentDir);
        var projectedUp = Plane.Project(plane, upDir);
        if (MathUtil.IsZero(projectedUp.LengthSquared()))
        {
            var defaultUpDir = GetDefaultUpDirection(tangentDir);
            return defaultUpDir;
        }
        projectedUp.Normalize();
        return projectedUp;
    }

    private static Vector3 GetDefaultUpDirection(Vector3 tangentDir)
    {
        if (MathUtil.IsZero(1 - Vector3.Dot(tangentDir, Vector3.UnitY)))
        {
            // If tangent is pointing up, then pick left direction
            return -Vector3.UnitX;
        }
        else
        {
            return Vector3.UnitY;
        }
    }

    private static Vector3 ApplyRoll(Vector3 forwardDir, Vector3 currentUpDir, AngleSingle roll)
    {
        if (MathUtil.IsZero(roll.Radians))
        {
            return currentUpDir;
        }

        var rotation = Quaternion.RotationAxis(forwardDir, roll.Radians);
        return rotation * currentUpDir;
    }

    private readonly struct TableSubdivisionSettings
    {
        public float MaximumPositionError { get; init; }
        public float MaximumArcLengthError { get; init; }
        public float MinimumAngleCosineError { get; init; }
        public float MaximumSegmentLength { get; init; }
        public float MinimumSegmentLength { get; init; }
        public int MaximumSubdivisionDepth { get; init; }
    }

    private struct TableSubdivisionContext
    {
        public int CurveIndex;
        public CurveTToDistanceTable TableValue0;
        public CurveTToDistanceTable TableValue1;
        public int CurrentDepth;
    }

    private struct CurveTToDistanceTable
    {
        /// <summary>
        /// The curve curve this belongs to.
        /// </summary>
        public required int CurveIndex;
        /// <summary>
        /// Value 0 to 1 on the curve.
        /// </summary>
        public required float CurveLocalT;
        /// <summary>
        /// Travel distance on the spline to this position.
        /// </summary>
        public required float TotalSplineDistance;
        /// <summary>
        /// Local position relative to the spline.
        /// </summary>
        public required Vector3 Position;
        public required Vector3 Tangent;    // Redundant field, but used for fast table building
        public required Quaternion Orientation;
    }

    private struct LookupTableResult
    {
        public int LookupTableIndex;
        public int NextLookupTableIndex;
        public float LookupTableLocalT;

        public int CurveIndex;
        public float CurveLocalT;
    }

    private struct ControlPointLookupComparer : IComparer<CurveTToDistanceTable>
    {
        public static ControlPointLookupComparer Instance => new();

        public readonly int Compare(CurveTToDistanceTable x, CurveTToDistanceTable y)
        {
            int compareValue = x.CurveIndex.CompareTo(y.CurveIndex);
            if (compareValue != 0)
            {
                return compareValue;
            }
            return x.CurveLocalT.CompareTo(y.CurveLocalT);
        }
    }

    private struct DistanceLookupComparer : IComparer<CurveTToDistanceTable>
    {
        public static DistanceLookupComparer Instance => new();

        public readonly int Compare(CurveTToDistanceTable x, CurveTToDistanceTable y)
        {
            int compareValue = x.TotalSplineDistance.CompareTo(y.TotalSplineDistance);
            if (compareValue != 0)
            {
                return compareValue;
            }
            compareValue = x.CurveIndex.CompareTo(y.CurveIndex);
            if (compareValue != 0)
            {
                return compareValue;
            }
            return x.CurveLocalT.CompareTo(y.CurveLocalT);
        }
    }

    private struct CurveRange
    {
        public int CurveIndex;
        public float StartDistance;
        public float EndDistance;
    }

    private struct CurveRangeLookupComparer : IComparer<CurveRange>
    {
        public static CurveRangeLookupComparer Instance => new();

        public readonly int Compare(CurveRange x, CurveRange y)
        {
            return x.EndDistance.CompareTo(y.EndDistance);
        }
    }
}
