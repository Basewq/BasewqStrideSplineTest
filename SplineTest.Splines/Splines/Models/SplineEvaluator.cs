// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

public class SplineEvaluator : ISplineEvaluator
{
    private readonly List<CurveTToDistanceTable> curveTToDistanceLookupTable = [];
    private readonly List<CurveRange> curveRangeTable = [];

    private bool isRebuildLookupTableRequired = true;

    public int SampleResolutionPerCurve { get; set; } = 64;
    public float MinimumSampleSpacing { get; set; } = 0.25f;

    public Spline Spline { get; private set; }

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
        Spline.SplinePropertyChanged -= OnSplinePropertyChanged;
        Spline.ControlPointsChanged -= OnControlPointsChanged;
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
        float splineT = distance /  totalDistance;
        return splineT;
    }

    public SplineSample Evaluate(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        var curTableValue = curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        var nextTableValue = curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        var rotation = CalculateRotation(lookupResult.CurveIndex, lookupResult.CurveLocalT);            // Too expensive?
        var curve = Spline.GetCurve(lookupResult.CurveIndex);
        var tangent = curve.GetTangent(lookupResult.CurveLocalT);     // Too expensive?

        return new SplineSample(position, rotation, tangent, splineT);
    }

    public SplineSample EvaluateFromDistance(float distance)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromDistance(curveTToDistanceLookupTableSpan, distance);

        var curTableValue = curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        var nextTableValue = curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        var rotation = CalculateRotation(lookupResult.CurveIndex, lookupResult.CurveLocalT);            // Too expensive?
        var curve = Spline.GetCurve(lookupResult.CurveIndex);
        var tangent = curve.GetTangent(lookupResult.CurveLocalT);     // Too expensive?

        float splineT = GetTFromDistance(distance);
        return new SplineSample(position, rotation, tangent, splineT);
    }

    private Quaternion CalculateRotation(int controlPointStartIndex, float curveT)
    {
        // TODO?
        var controlPoint1 = Spline[controlPointStartIndex];
        if (curveT == 0)
        {
            return controlPoint1.Rotation;
        }

        int controlPointEndIndex = controlPointStartIndex + 1;
        if (Spline.IsClosedLoop)
        {
            if (controlPointEndIndex >= Spline.Count)
            {
                controlPointEndIndex -= Spline.Count;
            }
        }
        else
        {
            controlPointEndIndex = Math.Min(controlPointEndIndex, Spline.Count - 1);
        }
        var controlPoint2 = Spline[controlPointEndIndex];
        if (curveT == 1)
        {
            return controlPoint2.Rotation;
        }

        var rotation = Quaternion.Slerp(controlPoint1.Rotation, controlPoint2.Rotation, curveT);
        return rotation;
    }

    public Vector3 EvaluatePosition(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        var curTableValue = curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        var nextTableValue = curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var position = Vector3.Lerp(curTableValue.Position, nextTableValue.Position, lookupResult.LookupTableLocalT);
        return position;
    }

    public Quaternion EvaluateRotation(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        var curTableValue = curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        var nextTableValue = curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var rotation = CalculateRotation(lookupResult.CurveIndex, lookupResult.CurveLocalT);            // Too expensive?
        return rotation;
    }

    public Vector3 EvaluateTangent(float splineT)
    {
        EnsureLookupTables();
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
        var lookupResult = FindTableValueFromT(curveTToDistanceLookupTableSpan, splineT);

        var curTableValue = curveTToDistanceLookupTableSpan[lookupResult.LookupTableIndex];
        var nextTableValue = curveTToDistanceLookupTableSpan[lookupResult.NextLookupTableIndex];

        var curve = Spline.GetCurve(lookupResult.CurveIndex);
        var tangent = curve.GetTangent(lookupResult.CurveLocalT);     // Too expensive?
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

        int startControlPointIndex = curveIndex;      // Curve tableIndex is the same as Control point tableIndex
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
            Vector3 point,
            in CurveTToDistanceTable value1,
            in CurveTToDistanceTable value2)
        {
            // Vector projection of [point to table1 point] onto [table2 point to table1 point]
            // to determine how much 'point' lies on the line [table2 point to table1 point]
            var curveLine = value2.Position - value1.Position;
            var pointVector = point - value1.Position;

            float denom = Math.Max(curveLine.LengthSquared(), MathUtil.ZeroTolerance);    // Avoid division by zero
            float tableT = Vector3.Dot(pointVector, curveLine) / denom;
            tableT = MathUtil.Clamp(tableT, min: 0, max: 1);

            var closestPointOnLine = value1.Position + (curveLine * tableT);
            float nextLocalT = value2.CurveLocalT;
            // Edge case check:
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

    private static int FindTableValueIndexByDistance(
        ReadOnlySpan<CurveTToDistanceTable> lookupTable,
        float distance)
    {
        var lookupValue = new CurveTToDistanceTable
        {
            TotalSplineDistance = distance,
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
        int tableIndex = FindTableValueIndexByDistance(curveTToDistanceLookupTable, targetDistance);

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
            // Edge case check:
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
        lastEncounteredControlPointIndex = curveStartIndex;    // Curve index is the same as the same as the control point index

        // Collect all control points we passed
        float curEndSplineDistance = endSplineDistance;
        while (true)
        {
            // Find end curve this spline sits on
            for (int i = curveStartIndex; i < curveRangeTableSpan.Length; i++)
            {
                int controlPointIndex = i;      // Curve index is the same as the same as the control point index
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
            CurveIndex = -1,    // Doesn'tableT matter
            StartDistance = targetDistance,
            EndDistance = targetDistance,
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

        var totalControlPointCount = Spline.ControlPoints.Count;
        if (totalControlPointCount <= 1)
        {
            isRebuildLookupTableRequired = false;
            return;     // Nothing to build
        }

        // TODO add additional sampling thresholds (eg. angle change)?
        curveTToDistanceLookupTable.EnsureCapacity(totalControlPointCount * SampleResolutionPerCurve);
        float totalCurveDistance = 0;
        var previousPos = Spline.GetCurve(0).StartPosition;
        int totalCurveCount = Spline.CurveCount;
        for (int curveIndex = 0; curveIndex < totalCurveCount; curveIndex++)
        {
            var curve = Spline.GetCurve(curveIndex);
            totalCurveDistance += (curve.StartPosition - previousPos).Length();
            previousPos = curve.StartPosition;
            // First position is always just the initial value of the curve
            curveTToDistanceLookupTable.Add(new CurveTToDistanceTable
            {
                CurveIndex = curveIndex,
                CurveLocalT = 0,
                TotalSplineDistance = totalCurveDistance,
                Position = curve.StartPosition
            });

            float nextSampleDistanceOffset = MinimumSampleSpacing;
            float nextSampleDistanceThreshold = totalCurveDistance + nextSampleDistanceOffset;
            int sampleStepCount = SampleResolutionPerCurve;
            for (int i = 1; i < sampleStepCount; i++)
            {
                float currentT = i / (float)sampleStepCount;
                var currentPos = curve.GetPosition(currentT);
                totalCurveDistance += (currentPos - previousPos).Length();
                previousPos = currentPos;

                if (totalCurveDistance >= nextSampleDistanceThreshold)
                {
                    curveTToDistanceLookupTable.Add(new CurveTToDistanceTable
                    {
                        CurveIndex = curveIndex,
                        CurveLocalT = currentT,
                        TotalSplineDistance = totalCurveDistance,
                        Position = currentPos
                    });

                    nextSampleDistanceThreshold = totalCurveDistance + nextSampleDistanceOffset;
                }
            }
        }

        // Add the final position (this is included even when Spline.IsClosedLoop = true, because it contains the full TotalCurveDistance)
        int lastCurveIndex = totalCurveCount - 1;
        var finalPos = Spline.GetCurve(lastCurveIndex).EndPosition;
        totalCurveDistance += (finalPos - previousPos).Length();
        curveTToDistanceLookupTable.Add(new CurveTToDistanceTable
        {
            CurveIndex = lastCurveIndex,
            CurveLocalT = 1,
            TotalSplineDistance = totalCurveDistance,
            Position = finalPos
        });

        // Build Curve Range table
        curveRangeTable.Clear();
        var nextCurveRange = new CurveRange
        {
            CurveIndex = 0,
            StartDistance = 0,
        };
        var curveTToDistanceLookupTableSpan = CollectionsMarshal.AsSpan(curveTToDistanceLookupTable);
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
        nextCurveRange.EndDistance = totalCurveDistance;
        curveRangeTable.Add(nextCurveRange);

        isRebuildLookupTableRequired = false;
    }

    private struct CurveTToDistanceTable
    {
        /// <summary>
        /// The curve curve this belongs to.
        /// </summary>
        public int CurveIndex;
        /// <summary>
        /// Value 0 to 1 on the curve.
        /// </summary>
        public float CurveLocalT;
        /// <summary>
        /// Travel distance on the spline to this position.
        /// </summary>
        public float TotalSplineDistance;
        /// <summary>
        /// Local position relative to the spline.
        /// </summary>
        public Vector3 Position;
    }

    private struct LookupTableResult
    {
        public int LookupTableIndex;
        public int NextLookupTableIndex;
        public float LookupTableLocalT;

        public int CurveIndex;
        public float CurveLocalT;
    }

    private struct DistanceLookupComparer : IComparer<CurveTToDistanceTable>
    {
        public static DistanceLookupComparer Instance => new();

        public readonly int Compare(CurveTToDistanceTable x, CurveTToDistanceTable y)
        {
            return x.TotalSplineDistance.CompareTo(y.TotalSplineDistance);
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
