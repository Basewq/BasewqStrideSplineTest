// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using System.Collections;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

public delegate void SplinePropertyChangedEventHandler(object sender);

public delegate void SplineControlPointEventHandler<TEventArgs>(object sender, ref TEventArgs e);

public readonly record struct IndexedSplineControlPoint(int ControlPointIndex, SplineControlPoint ControlPoint)
{
}

[DataContract]
[Display(Expand = ExpandRule.Once)]
public class Spline
{
    private readonly List<BezierSegment> splineSegments = [];   // The start pos of the segment is the same as the control point on the same index in ControlPoints list.
    private readonly List<SegmentTToPositionTable> segmentTToPositionLookupTable = [];
    private bool isRebuildLookupTableRequired;

    public event SplinePropertyChangedEventHandler SplinePropertyChanged;
    public event SplineControlPointEventHandler<SplineControlPointsChangedEventArgs> ControlPointsChanged;

    private bool isClosedLoop;
    /// <summary>
    /// Gets or sets a value indicating whether the last spline control point connects back to the first spline control point to form a loop.
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This is only applicable where there are at least two spline control points.
    /// </remarks>
    public bool IsClosedLoop
    {
        get => isClosedLoop;
        set
        {
            isRebuildLookupTableRequired = true;
            SetField(ref isClosedLoop, value);
        }
    }

    private TrackingCollection<SplineControlPoint> controlPoints;
    [DataMember]
    [Display(Expand = ExpandRule.Once)]
    internal TrackingCollection<SplineControlPoint> ControlPoints
    {
        get => controlPoints;
        set
        {
            controlPoints?.CollectionChanged -= OnSplineControlPoints_CollectionChanged;
            controlPoints = value;
            controlPoints?.CollectionChanged += OnSplineControlPoints_CollectionChanged;
        }
    }

    private void OnSplineControlPoints_CollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
    {
        isRebuildLookupTableRequired = true;
        SplineControlPoint newItem = (SplineControlPoint?)e.Item  ?? default;
        SplineControlPoint oldItem = (SplineControlPoint?)e.OldItem ?? default;
        var eventArgs = new SplineControlPointsChangedEventArgs(e.Action, newItem, oldItem, index: e.Index, e.CollectionChanged);
        ControlPointsChanged?.Invoke(this, ref eventArgs);
    }

    [DataMemberIgnore]
    public SplineControlPoint this[int index]
    {
        get => ControlPoints[index];
        set
        {
            bool hasChanged = ControlPoints[index] != value;
            isRebuildLookupTableRequired = isRebuildLookupTableRequired || hasChanged;
            ControlPoints[index] = value;
        }
    }

    /// <summary>
    /// Gets the number of control points in this spline.
    /// </summary>
    public int Count => ControlPoints.Count;

    public Spline()
    {
        ControlPoints = [];
    }

    public void Add(Vector3 position)
    {
        var newControlPoint = new SplineControlPoint
        {
            Position = position,
            TangentIn = Vector3.Zero,
            TangentOut = Vector3.Zero
        };
        Add(newControlPoint);
    }

    public void Add(SplineControlPoint controlPoint)
    {
        isRebuildLookupTableRequired = true;
        int newControlPointsIndex = ControlPoints.Count;
        ControlPoints.Add(controlPoint);
    }

    public bool Remove(SplineControlPoint item)
    {
        int index = ControlPoints.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        isRebuildLookupTableRequired = true;
        var controlPoint = ControlPoints[index];
        ControlPoints.RemoveAt(index);

        return true;
    }

    public void RemoveAt(int index)
    {
        isRebuildLookupTableRequired = true;
        var controlPoint = ControlPoints[index];
        ControlPoints.RemoveAt(index);
    }

    public bool Contains(SplineControlPoint item) => ControlPoints.Contains(item);

    public void CopyTo(SplineControlPoint[] array, int arrayIndex) => ControlPoints.CopyTo(array, arrayIndex);

    public void Clear()
    {
        isRebuildLookupTableRequired = true;
        ControlPoints.Clear();
    }

    /// <summary>
    /// Returns the total distance over the entire spline.
    /// </summary>
    public float GetTotalDistance()
    {
        BuildLookupTable();
        return segmentTToPositionLookupTable[segmentTToPositionLookupTable.Count - 1].TotalSplineDistance;
    }

    /// <summary>
    /// Returns the position on the spline segment closest to <paramref name="position"/>.
    /// </summary>
    /// <param name="position">Position in the spline's local space.</param>
    public SplineClosestPositionInfo GetClosestPointOnSpline(Vector3 position)
    {
        if (ControlPoints.Count == 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }
        BuildLookupTable();

        int minSegmentIndex = -1;
        int minControlPointsIndex = -1;
        Vector3 minPointOnSpline = Vector3.Zero;
        float minPointLocalT = 0;
        float minDistSqrd = float.MaxValue;
        // TODO Calculate closest distance from actual segment equation instead of linear approx
        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        for (int i = 0; i < segmentTToPositionLookupTableSpan.Length - 1; i++)
        {
            ref var tableValue1 = ref segmentTToPositionLookupTableSpan[i];
            ref var tableValue2 = ref segmentTToPositionLookupTableSpan[i + 1];
            var startPos = tableValue1.Position;
            var endPos = tableValue2.Position;
            var pointOnLine = GetClosestPointOnLine(startPos, endPos, position, out float lineT);
            float distSqrd = Vector3.DistanceSquared(pointOnLine, position);
            if (minDistSqrd > distSqrd)
            {
                minSegmentIndex = i;
                minDistSqrd = distSqrd;
                minControlPointsIndex = tableValue1.ControlPointIndex;
                minPointOnSpline = pointOnLine;
                minPointLocalT = tableValue1.ControlPointLocalT + lineT;
            }
        }

        ref var segmentTableValue1 = ref segmentTToPositionLookupTableSpan[minSegmentIndex];
        ref var segmentTableValue2 = ref segmentTToPositionLookupTableSpan[minSegmentIndex + 1];
        float curSplineDistance =  MathUtil.Lerp(from: segmentTableValue1.TotalSplineDistance, to: segmentTableValue1.TotalSplineDistance, amount: minPointLocalT);
        float splineTotalDistance = GetTotalDistance();

        int controlPointAIndex = minControlPointsIndex;
        int controlPointBIndex = IsClosedLoop ? (controlPointAIndex + 1) % Count : MathUtil.Clamp(controlPointAIndex, min: 0, max: Count - 1);
        var closestPosInfo = new SplineClosestPositionInfo
        {
            Position = minPointOnSpline,
            SplineControlPointAIndex = controlPointAIndex,
            SplineControlPointBIndex = controlPointBIndex,
            LocalT = minPointLocalT,
            SplineDistance = Math.Clamp(curSplineDistance, min: 0, max: splineTotalDistance)
        };
        return closestPosInfo;
    }

    private static Vector3 GetClosestPointOnLine(in Vector3 lineP0, in Vector3 lineP1, in Vector3 point, out float lineT)
    {
        var pointToP0 = point - lineP0;
        var p1ToP0 = lineP1 - lineP0;
        float dotProduct = Vector3.Dot(p1ToP0, pointToP0);
        lineT = dotProduct / p1ToP0.Length();               // Normalized projection amount
        lineT = MathUtil.Clamp(lineT, min: 0, max: 1);      // Only care how much is on the line segment
        var closetPoint = Vector3.Lerp(lineP0, lineP1, lineT);
        return closetPoint;
    }

    public void CollectSplineSamples(List<SplineSample> splineSamples)
    {
        BuildLookupTable();
        splineSamples.EnsureCapacity(segmentTToPositionLookupTable.Count);
        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        for (int i = 0; i < segmentTToPositionLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref segmentTToPositionLookupTableSpan[i];
            var placement = new SplineSample
            {
                Position = tableValue.Position,
                Rotation = CalculateRotation(tableValue.ControlPointIndex, tableValue.ControlPointLocalT),
                Tangent = CalculateTangent(tableValue.ControlPointIndex, tableValue.ControlPointLocalT),
            };
            splineSamples.Add(placement);
        }
    }

    public BoundingBox CalculateBoundingBox()
    {
        BuildLookupTable();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        for (int i = 0; i < segmentTToPositionLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref segmentTToPositionLookupTableSpan[i];
            Vector3.Min(ref min, ref tableValue.Position, out min);
            Vector3.Max(ref max, ref tableValue.Position, out max);
        }

        return new BoundingBox(min, max);
    }

    public SplineSample SampleFromDistance(float splineDistance)
    {
        BuildLookupTable();
        float totalDistance = GetTotalDistance();
        if (totalDistance <= 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }

        if (IsClosedLoop)
        {
            while (splineDistance >= totalDistance)
            {
                splineDistance -= totalDistance;
            }
            while (splineDistance < 0)
            {
                splineDistance += totalDistance;
            }
        }
        else
        {
            splineDistance = MathUtil.Clamp(splineDistance, min: 0, max: totalDistance);
        }
        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        int tableValueStartIndex = segmentTToPositionLookupTableSpan.Length - 1;
        for (int i = 1; i < segmentTToPositionLookupTableSpan.Length; i++)
        {
            if (splineDistance < segmentTToPositionLookupTableSpan[i].TotalSplineDistance)
            {
                tableValueStartIndex = i - 1;
                break;
            }
        }

        int tableValueEndIndex;
        if (IsClosedLoop)
        {
            // For a closed loop spline, the last table value is the same as the first value,
            // so subtract one from segmentTToPositionLookupTable.Count when we use modulo to skip it.
            tableValueEndIndex = (tableValueStartIndex + 1) % segmentTToPositionLookupTableSpan.Length;
        }
        else
        {
            tableValueEndIndex = Math.Min(tableValueStartIndex + 1, segmentTToPositionLookupTableSpan.Length - 1);
        }

        ref var tableValue1 = ref segmentTToPositionLookupTableSpan[tableValueStartIndex];
        ref var tableValue2 = ref segmentTToPositionLookupTableSpan[tableValueEndIndex];
        //System.Diagnostics.Debug.Write($"\tControlPoint: {tableValue1.ControlPointIndex} - {tableValue2.ControlPointIndex}\t");

        float lookupTableTValue;
        float segmentTValue;
        if (tableValueStartIndex == tableValueEndIndex)
        {
            // Lies exactly on a table value
            lookupTableTValue = 0;
            segmentTValue = tableValue1.ControlPointLocalT;
        }
        else
        {
            lookupTableTValue = MathUtil.InverseLerp(tableValue1.TotalSplineDistance, tableValue2.TotalSplineDistance, splineDistance);
            lookupTableTValue = MathUtil.Clamp(lookupTableTValue, 0, 1);
            segmentTValue = MathUtil.Lerp(tableValue1.ControlPointLocalT, tableValue2.ControlPointLocalT, lookupTableTValue);
            //segmentTValue = MathUtil.Clamp(segmentTValue, 0, 1);
        }
        var sample = new SplineSample
        {
            Position = Vector3.Lerp(tableValue1.Position, tableValue2.Position, lookupTableTValue),
            Rotation = CalculateRotation(tableValue1.ControlPointIndex, segmentTValue),
            Tangent = CalculateTangent(tableValue1.ControlPointIndex, segmentTValue),
        };
        return sample;
    }

    private Quaternion CalculateRotation(int controlPointStartIndex, float tValue)
    {
        var controlPoint1 = this[controlPointStartIndex];
        if (tValue == 0)
        {
            return controlPoint1.Rotation;
        }

        int controlPointEndIndex = controlPointStartIndex + 1;
        if (IsClosedLoop)
        {
            if (controlPointEndIndex >= Count)
            {
                controlPointEndIndex -= Count;
            }
        }
        else
        {
            controlPointEndIndex = Math.Min(controlPointEndIndex, Count - 1);
        }
        var controlPoint2 = this[controlPointEndIndex];
        if (tValue == 1)
        {
            return controlPoint2.Rotation;
        }

        var rotation = Quaternion.Slerp(controlPoint1.Rotation, controlPoint2.Rotation, tValue);
        return rotation;
    }

    private Vector3 CalculateTangent(int controlPointStartIndex, float tValue)
    {
        var controlPoint1 = this[controlPointStartIndex];
        Vector3 tangent;
        if (tValue == 0)
        {
            tangent = Vector3.Normalize(controlPoint1.TangentOut);
            return tangent;
        }

        int controlPointEndIndex = controlPointStartIndex + 1;
        if (IsClosedLoop)
        {
            if (controlPointEndIndex >= Count)
            {
                controlPointEndIndex -= Count;
            }
        }
        else
        {
            controlPointEndIndex = Math.Min(controlPointEndIndex, Count - 1);
        }
        var controlPoint2 = this[controlPointEndIndex];
        if (tValue == 1)
        {
            tangent = Vector3.Normalize(-controlPoint2.TangentIn);
            return tangent;
        }

        int segmentIndex = controlPointStartIndex;       // Same index
        tangent = splineSegments[segmentIndex].GetTangent(tValue);
        System.Diagnostics.Debug.WriteLineIf(true, $"Rot\t{segmentIndex}\t{tValue}\t{tangent}");
        return tangent;
    }

    public void CollectEncounteredControlPoints(List<IndexedSplineControlPoint> controlPointsEncountered, float startSplineDistance, float endSplineDistance)
    {
        BuildLookupTable();
        float totalDistance = GetTotalDistance();
        if (totalDistance <= 0 || segmentTToPositionLookupTable.Count <= 1)
        {
            return;
        }

        bool isMovingForward = endSplineDistance > startSplineDistance;
        float splineDistance = startSplineDistance;
        int lastEncounteredControlPointsIndex = -1;
        if (!isMovingForward)
        {
            Utilities.Swap(ref startSplineDistance, ref endSplineDistance);
            if (endSplineDistance < 0)
            {
                endSplineDistance += totalDistance;
            }
        }

        // Find initial segment this spline sits on
        int segmentStartIndex = 0;
        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        for (int i = 0; i < segmentTToPositionLookupTableSpan.Length; i++)
        {
            if (startSplineDistance > segmentTToPositionLookupTableSpan[i].TotalSplineDistance)
            {
                continue;
            }
            segmentStartIndex = i;
            lastEncounteredControlPointsIndex = segmentTToPositionLookupTableSpan[i].ControlPointIndex;
            break;
        }

        // Collect all control points we passed
        float curEndSplineDistance = endSplineDistance;
        while (true)
        {
            // Find end segment this spline sits on
            for (int i = segmentStartIndex; i < segmentTToPositionLookupTableSpan.Length; i++)
            {
                int segmentStartControlPointsIndex = segmentTToPositionLookupTableSpan[i].ControlPointIndex;
                if (lastEncounteredControlPointsIndex == segmentStartControlPointsIndex)
                {
                    if (curEndSplineDistance < segmentTToPositionLookupTableSpan[i].TotalSplineDistance)
                    {
                        break;
                    }
                    continue;
                }
                var indexedPoint = new IndexedSplineControlPoint(segmentStartControlPointsIndex, this[segmentStartControlPointsIndex]);
                controlPointsEncountered.Add(indexedPoint);
                lastEncounteredControlPointsIndex = segmentStartControlPointsIndex;
                //if (curEndSplineDistance < segmentTToPositionLookupTableSpan[i].TotalSplineDistance)
                //{
                //    break;
                //}
            }

            if (IsClosedLoop)
            {
                if (curEndSplineDistance > totalDistance)
                {
                    curEndSplineDistance -= totalDistance;
                    segmentStartIndex = 0;
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

        if (!isMovingForward && controlPointsEncountered.Count > 1)
        {
            controlPointsEncountered.Reverse();
        }
    }

    private void BuildLookupTable()
    {
        if (!isRebuildLookupTableRequired)
        {
            return;
        }

        splineSegments.Clear();
        segmentTToPositionLookupTable.Clear();

        var totalControlPointCount = ControlPoints.Count;
        if (totalControlPointCount > 1)
        {
            splineSegments.EnsureCapacity(IsClosedLoop ? totalControlPointCount : totalControlPointCount - 1);
            for (int i = 0; i < totalControlPointCount - 1; i++)
            {
                var currentControlPoint = this[i];
                var nextControlPoint = this[i + 1];

                var segment = new BezierSegment(currentControlPoint, nextControlPoint);
                splineSegments.Add(segment);
            }
            if (IsClosedLoop)
            {
                // Add additional segment (forming the final control point back to first control point)
                var currentControlPoint = this[totalControlPointCount - 1];
                var nextControlPoint = this[0];

                var segment = new BezierSegment(currentControlPoint, nextControlPoint);
                splineSegments.Add(segment);
            }

            // TODO add additional sampling thresholds (eg. distance, angle change)
            const int SampleSizePerSegment = 10;
            float dt = 1f / SampleSizePerSegment;
            var splineSegmentsSpan = CollectionsMarshal.AsSpan(splineSegments);
            segmentTToPositionLookupTable.EnsureCapacity(totalControlPointCount * SampleSizePerSegment);
            float totalCurveDistance = 0;
            var previousPos = splineSegmentsSpan[0].P0;
            for (int segmentIndex = 0; segmentIndex < splineSegments.Count; segmentIndex++)
            {
                ref var segment = ref splineSegmentsSpan[segmentIndex];
                totalCurveDistance += (segment.P0 - previousPos).Length();
                previousPos = segment.P0;
                int controlPointIndex = segmentIndex;   // Same index
                // First position is always just the initial value of the segment
                segmentTToPositionLookupTable.Add(new SegmentTToPositionTable
                {
                    ControlPointIndex = controlPointIndex,
                    ControlPointLocalT = 0,
                    TotalSplineDistance = totalCurveDistance,
                    Position = segment.StartPosition
                });

                for (int i = 1; i < SampleSizePerSegment; i++)
                {
                    float currentT = dt * i;
                    var currentPos = segment.GetPosition(currentT);
                    totalCurveDistance += (currentPos - previousPos).Length();
                    previousPos = currentPos;
                    segmentTToPositionLookupTable.Add(new SegmentTToPositionTable
                    {
                        ControlPointIndex = controlPointIndex,
                        ControlPointLocalT = currentT,
                        TotalSplineDistance = totalCurveDistance,
                        Position = currentPos
                    });
                }
            }

            // Add the final position (this is included even when IsClosedLoop = true, because it contains the full TotalCurveDistance)
            var finalPos = splineSegmentsSpan[splineSegments.Count - 1].EndPosition;
            totalCurveDistance += (finalPos - previousPos).Length();
            segmentTToPositionLookupTable.Add(new SegmentTToPositionTable
            {
                ControlPointIndex = controlPoints.Count - 1,
                ControlPointLocalT = 1,
                TotalSplineDistance = totalCurveDistance,
                Position = finalPos
            });
        }

        isRebuildLookupTableRequired = false;
    }

    public Enumerator GetEnumerator() => new Enumerator(controlPoints);

    //IEnumerator<SplineControlPoint> IEnumerable<SplineControlPoint>.GetEnumerator() => GetEnumerator();

    //IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        SplinePropertyChanged?.Invoke(this);
    }

    private struct SegmentTToPositionTable
    {
        /// <summary>
        /// The starting control point this belongs to.
        /// </summary>
        public int ControlPointIndex;
        /// <summary>
        /// Value 0 to 1 from ControlPointIndex to (ControlPointIndex + 1)
        /// </summary>
        public float ControlPointLocalT;
        /// <summary>
        /// Travel distance on the spline to this position.
        /// </summary>
        public float TotalSplineDistance;
        /// <summary>
        /// Local position relative to the spline.
        /// </summary>
        public Vector3 Position;
    }

    public struct Enumerator : IEnumerator<SplineControlPoint>
    {
        private readonly TrackingCollection<SplineControlPoint> splineControlPoints;
        private TrackingCollection<SplineControlPoint>.Enumerator enumerator;

        public Enumerator(TrackingCollection<SplineControlPoint> splineControlPoints)
        {
            this.splineControlPoints = splineControlPoints;
            enumerator = splineControlPoints.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public bool MoveNext() => enumerator.MoveNext();

        public void Reset()
        {
            enumerator = splineControlPoints.GetEnumerator();
        }

        public readonly SplineControlPoint Current => enumerator.Current;

        readonly object IEnumerator.Current => Current;
    }
}
