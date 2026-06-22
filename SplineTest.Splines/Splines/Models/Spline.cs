// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using System.Collections;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

public delegate void SplinePropertyChangedEventHandler(object sender);

public delegate void SplineNodeEventHandler<TEventArgs>(object sender, ref TEventArgs e);

[DataContract]
[Display(Expand = ExpandRule.Once)]
public class Spline
{
    private readonly List<BezierSegment> splineSegments = [];   // The start pos of the segment is the same as the node on the same index in SplineNodes list.
    private readonly List<SegmentTToPositionTable> segmentTToPositionLookupTable = [];
    private bool isRebuildLookupTableRequired;

    public event SplinePropertyChangedEventHandler SplinePropertyChanged;
    public event SplineNodeEventHandler<SplineNodeCollectionChangedEventArgs> NodeCollectionChanged;

    private bool isClosedLoop;
    /// <summary>
    /// Gets or sets a value indicating whether the last spline node connects back to the first spline node to form a loop.
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This is only applicable where there are at least two spline nodes.
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

    private TrackingCollection<SplineNode> splineNodes;
    [DataMember]
    [Display(Expand = ExpandRule.Once)]
    internal TrackingCollection<SplineNode> SplineNodes
    {
        get => splineNodes;
        set
        {
            splineNodes?.CollectionChanged -= OnSplineNodes_CollectionChanged;
            splineNodes = value;
            splineNodes?.CollectionChanged += OnSplineNodes_CollectionChanged;
        }
    }

    private void OnSplineNodes_CollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
    {
        isRebuildLookupTableRequired = true;
        SplineNode newItem = (SplineNode?)e.Item  ?? default;
        SplineNode oldItem = (SplineNode?)e.OldItem ?? default;
        var eventArgs = new SplineNodeCollectionChangedEventArgs(e.Action, newItem, oldItem, index: e.Index, e.CollectionChanged);
        NodeCollectionChanged?.Invoke(this, ref eventArgs);
    }

    [DataMemberIgnore]
    public SplineNode this[int index]
    {
        get => SplineNodes[index];
        set
        {
            bool hasChanged = SplineNodes[index] != value;
            isRebuildLookupTableRequired = isRebuildLookupTableRequired || hasChanged;
            SplineNodes[index] = value;
        }
    }

    /// <summary>
    /// Gets the number of nodes in this spline.
    /// </summary>
    public int Count => SplineNodes.Count;

    ////public bool IsReadOnly => false;

    public Spline()
    {
        SplineNodes = [];
    }

    public void Add(Vector3 position)
    {
        var newNode = new SplineNode
        {
            Position = position,
            TangentInPosition = position,
            TangentOutPosition = position
        };
        Add(newNode);
    }

    public void Add(SplineNode node)
    {
        isRebuildLookupTableRequired = true;
        int newNodeIndex = SplineNodes.Count;
        SplineNodes.Add(node);
    }

    public bool Remove(SplineNode item)
    {
        int index = SplineNodes.IndexOf(item);
        if (index < 0)
        {
            return false;
        }

        isRebuildLookupTableRequired = true;
        var node = SplineNodes[index];
        SplineNodes.RemoveAt(index);

        return true;
    }

    public void RemoveAt(int index)
    {
        isRebuildLookupTableRequired = true;
        var node = SplineNodes[index];
        SplineNodes.RemoveAt(index);
    }

    public bool Contains(SplineNode item) => SplineNodes.Contains(item);

    public void CopyTo(SplineNode[] array, int arrayIndex) => SplineNodes.CopyTo(array, arrayIndex);

    public void Clear()
    {
        isRebuildLookupTableRequired = true;
        SplineNodes.Clear();
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
        if (SplineNodes.Count == 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }
        BuildLookupTable();

        int minNodeIndex = -1;
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
                minDistSqrd = distSqrd;
                minNodeIndex = tableValue1.NodeIndex;
                minPointOnSpline = pointOnLine;
                minPointLocalT = tableValue1.NodeLocalT + lineT;
            }
        }

        int nodeAIndex = minNodeIndex;
        int nodeBIndex = IsClosedLoop ? (nodeAIndex + 1) % Count : MathUtil.Clamp(nodeAIndex, min: 0, max: Count - 1);
        var closestPosInfo = new SplineClosestPositionInfo
        {
            Position = minPointOnSpline,
            SplineNodeAIndex = nodeAIndex,
            SplineNodeBIndex = nodeBIndex,
            LocalT = minPointLocalT,
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

    public void CollectSplinePlacements(List<SplinePlacement> splinePlacements)
    {
        BuildLookupTable();
        splinePlacements.EnsureCapacity(segmentTToPositionLookupTable.Count);
        var segmentTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(segmentTToPositionLookupTable);
        for (int i = 0; i < segmentTToPositionLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref segmentTToPositionLookupTableSpan[i];
            var placement = new SplinePlacement
            {
                Position = tableValue.Position,
                Rotation = CalculateRotation(tableValue.NodeIndex, tableValue.NodeLocalT),
            };
            splinePlacements.Add(placement);
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

    public SplinePlacement GetPlacementFromSplineDistance(float splineDistance)
    {
        BuildLookupTable();
        float totalDistance = GetTotalDistance();
        if (totalDistance <= 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }

        if (IsClosedLoop)
        {
            while (splineDistance > totalDistance)
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
            tableValueEndIndex = (tableValueStartIndex + 1) % (segmentTToPositionLookupTableSpan.Length - 1);
        }
        else
        {
            tableValueEndIndex = Math.Min(tableValueStartIndex + 1, segmentTToPositionLookupTableSpan.Length);
        }

        ref var tableValue1 = ref segmentTToPositionLookupTableSpan[tableValueStartIndex];
        ref var tableValue2 = ref segmentTToPositionLookupTableSpan[tableValueEndIndex];

        if (tableValueStartIndex == tableValueEndIndex)
        {
            // Lies exactly on a table value
            float tValue = tableValue1.NodeLocalT;
            return new SplinePlacement
            {
                Position = tableValue1.Position,
                Rotation = CalculateRotation(tableValue1.NodeIndex, tValue)
            };
        }
        else
        {
            float tValue = MathUtil.InverseLerp(tableValue1.TotalSplineDistance, tableValue2.TotalSplineDistance, splineDistance);
            tValue = MathUtil.Clamp(tValue, 0, 1);
            return new SplinePlacement
            {
                Position = Vector3.Lerp(tableValue1.Position, tableValue2.Position, tValue),
                Rotation = CalculateRotation(tableValue1.NodeIndex, tValue)
            };
        }
    }

    private Quaternion CalculateRotation(int nodeStartIndex, float tValue)
    {
        var node1 = this[nodeStartIndex];
        var node2 = this[(nodeStartIndex + 1) % Count];
        return Quaternion.Slerp(node1.Rotation, node2.Rotation, tValue);
    }

    private void BuildLookupTable()
    {
        if (!isRebuildLookupTableRequired)
        {
            return;
        }

        splineSegments.Clear();
        segmentTToPositionLookupTable.Clear();

        var totalNodeCount = SplineNodes.Count;
        if (totalNodeCount > 1)
        {
            splineSegments.EnsureCapacity(IsClosedLoop ? totalNodeCount : totalNodeCount - 1);
            for (int i = 0; i < totalNodeCount - 1; i++)
            {
                var currentSplineNode = this[i];
                var nextSplineNode = this[i + 1];

                var segment = new BezierSegment(currentSplineNode, nextSplineNode);
                splineSegments.Add(segment);

            }
            if (IsClosedLoop)
            {
                var currentSplineNode = this[totalNodeCount - 1];
                var nextSplineNode = this[0];

                var segment = new BezierSegment(currentSplineNode, nextSplineNode);
                splineSegments.Add(segment);
            }

            // TODO add additional sampling thresholds (eg. distance, angle change)
            const int SampleSizePerSegment = 10;
            float dt = 1f / SampleSizePerSegment;
            var splineSegmentsSpan = CollectionsMarshal.AsSpan(splineSegments);
            segmentTToPositionLookupTable.EnsureCapacity(totalNodeCount * SampleSizePerSegment);
            float totalCurveDistance = 0;
            var previousPos = splineSegmentsSpan[0].P0;
            for (int nodeIndex = 0; nodeIndex < splineSegments.Count; nodeIndex++)
            {
                ref var segment = ref splineSegmentsSpan[nodeIndex];
                totalCurveDistance += (segment.P0 - previousPos).Length();
                previousPos = segment.P0;
                // First position is always just the initial value of the segment
                segmentTToPositionLookupTable.Add(new SegmentTToPositionTable
                {
                    NodeIndex = nodeIndex,
                    NodeLocalT = 0,
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
                        NodeIndex = nodeIndex,
                        NodeLocalT = currentT,
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
                NodeIndex = splineSegments.Count - 1,
                NodeLocalT = 1,
                TotalSplineDistance = totalCurveDistance,
                Position = finalPos
            });
        }

        isRebuildLookupTableRequired = false;
    }

    public Enumerator GetEnumerator() => new Enumerator(splineNodes);

    //IEnumerator<SplineNode> IEnumerable<SplineNode>.GetEnumerator() => GetEnumerator();

    //IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        SplinePropertyChanged?.Invoke(this);
    }

    private struct SegmentTToPositionTable
    {
        /// <summary>
        /// The starting node this belongs to.
        /// </summary>
        public int NodeIndex;
        /// <summary>
        /// Value 0 to 1 from NodeIndex to (NodeIndex + 1)
        /// </summary>
        public float NodeLocalT;
        /// <summary>
        /// Travel distance on the spline to this position.
        /// </summary>
        public float TotalSplineDistance;
        /// <summary>
        /// Local position relative to the spline.
        /// </summary>
        public Vector3 Position;
    }

    public struct Enumerator : IEnumerator<SplineNode>
    {
        private readonly TrackingCollection<SplineNode> splineNodes;
        private TrackingCollection<SplineNode>.Enumerator enumerator;

        public Enumerator(TrackingCollection<SplineNode> splineNodes)
        {
            this.splineNodes = splineNodes;
            enumerator = splineNodes.GetEnumerator();
        }

        public void Dispose()
        {
            enumerator.Dispose();
        }

        public bool MoveNext() => enumerator.MoveNext();

        public void Reset()
        {
            enumerator = splineNodes.GetEnumerator();
        }

        public readonly SplineNode Current => enumerator.Current;

        readonly object IEnumerator.Current => Current;
    }
}
