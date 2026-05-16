// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using System.Collections;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models;

[DataContract]
[Display(Expand = ExpandRule.Once)]
public class BezierSpline : ISpline<SplineNode> /*, ICollection<SplineNode>*/
{
    private readonly List<BezierCurve> splineCurves = [];   // The start pos of the curve is the same as the node on the same index in SplineNodes list.
    private readonly List<CurveTToPositionTable> curveTToPositionLookupTable = [];
    private bool isRebuildLookupTableRequired;

    public event SplinePropertyChangedEventHandler SplinePropertyChanged;
    public event SplineNodeEventHandler<SplineNodeCollectionChangedEventArgs<SplineNode>> NodeCollectionChanged;

    private bool isClosedLoop;
    public bool IsClosedLoop
    {
        get
        {
            return isClosedLoop;
        }
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
        var eventArgs = new SplineNodeCollectionChangedEventArgs<SplineNode>(e.Action, newItem, oldItem, index: e.Index, e.CollectionChanged);
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

    public int Count => SplineNodes.Count;

    ////public bool IsReadOnly => false;

    public BezierSpline()
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
    /// The total distance of the spline curve.
    /// </summary>
    public float GetTotalDistance()
    {
        BuildLookupTable();
        return curveTToPositionLookupTable[curveTToPositionLookupTable.Count - 1].TotalSplineDistance;
    }

    public SplineClosestPositionInfo GetClosestPointOnSpline(Vector3 position)
    {
        if (SplineNodes.Count == 0)
        {
            throw new InvalidOperationException("Spline is empty.");
        }
        BuildLookupTable();

        int minNodeIndex = -1;
        Vector3 minPointOnSpline = Vector3.Zero;
        float minPointCurveT = 0;
        float minDistSqrd = float.MaxValue;
        // TODO Calculate closest distance from actual curve equation instead of linear approx
        var curveTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(curveTToPositionLookupTable);
        for (int i = 0; i < curveTToPositionLookupTableSpan.Length - 1; i++)
        {
            ref var tableValue1 = ref curveTToPositionLookupTableSpan[i];
            ref var tableValue2 = ref curveTToPositionLookupTableSpan[i + 1];
            var startPos = tableValue1.Position;
            var endPos = tableValue2.Position;
            var pointOnLine = GetClosestPointOnLine(startPos, endPos, position, out float lineT);
            float distSqrd = Vector3.DistanceSquared(pointOnLine, position);
            if (minDistSqrd > distSqrd)
            {
                minDistSqrd = distSqrd;
                minNodeIndex = tableValue1.NodeIndex;
                minPointOnSpline = pointOnLine;
                minPointCurveT = tableValue1.NodeCurveT + lineT;
            }
        }

        int nodeAIndex = minNodeIndex;
        int nodeBIndex = IsClosedLoop ? (nodeAIndex + 1) % Count : MathUtil.Clamp(nodeAIndex, min: 0, max: Count - 1);
        var closestPosInfo = new SplineClosestPositionInfo
        {
            Position = minPointOnSpline,
            SplineNodeAIndex = nodeAIndex,
            SplineNodeBIndex = nodeBIndex,
            CurveT = minPointCurveT,
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
        splinePlacements.EnsureCapacity(curveTToPositionLookupTable.Count);
        var curveTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(curveTToPositionLookupTable);
        for (int i = 0; i < curveTToPositionLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref curveTToPositionLookupTableSpan[i];
            var placement = new SplinePlacement
            {
                Position = tableValue.Position,
                Rotation = CalculateRotation(tableValue.NodeIndex, tableValue.NodeCurveT),
            };
            splinePlacements.Add(placement);
        }
    }

    public BoundingBox CalculateBoundingBox()
    {
        BuildLookupTable();

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        var curveTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(curveTToPositionLookupTable);
        for (int i = 0; i < curveTToPositionLookupTableSpan.Length; i++)
        {
            ref var tableValue = ref curveTToPositionLookupTableSpan[i];
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
        var curveTToPositionLookupTableSpan = CollectionsMarshal.AsSpan(curveTToPositionLookupTable);
        int tableValueStartIndex = curveTToPositionLookupTableSpan.Length - 1;
        for (int i = 1; i < curveTToPositionLookupTableSpan.Length; i++)
        {
            if (splineDistance < curveTToPositionLookupTableSpan[i].TotalSplineDistance)
            {
                tableValueStartIndex = i - 1;
                break;
            }
        }

        int tableValueEndIndex;
        if (IsClosedLoop)
        {
            // For a closed loop spline, the last table value is the same as the first value,
            // so subtract one from curveTToPositionLookupTable.Count when we use modulo to skip it.
            tableValueEndIndex = (tableValueStartIndex + 1) % (curveTToPositionLookupTableSpan.Length - 1);
        }
        else
        {
            tableValueEndIndex = Math.Min(tableValueStartIndex + 1, curveTToPositionLookupTableSpan.Length);
        }

        ref var tableValue1 = ref curveTToPositionLookupTableSpan[tableValueStartIndex];
        ref var tableValue2 = ref curveTToPositionLookupTableSpan[tableValueEndIndex];

        if (tableValueStartIndex == tableValueEndIndex)
        {
            // Lies exactly on a table value
            float tValue = tableValue1.NodeCurveT;
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

        splineCurves.Clear();
        curveTToPositionLookupTable.Clear();

        var totalNodeCount = SplineNodes.Count;
        if (totalNodeCount > 1)
        {
            splineCurves.EnsureCapacity(IsClosedLoop ? totalNodeCount : totalNodeCount - 1);
            for (int i = 0; i < totalNodeCount - 1; i++)
            {
                var currentSplineNode = this[i];
                var nextSplineNode = this[i + 1];

                var curve = new BezierCurve(currentSplineNode, nextSplineNode);
                splineCurves.Add(curve);

            }
            if (IsClosedLoop)
            {
                var currentSplineNode = this[totalNodeCount - 1];
                var nextSplineNode = this[0];

                var curve = new BezierCurve(currentSplineNode, nextSplineNode);
                splineCurves.Add(curve);
            }

            // TODO add additional sampling thresholds (eg. distance, angle change)
            const int SampleSizePerCurve = 10;
            float dt = 1f / SampleSizePerCurve;
            var splineCurvesSpan = CollectionsMarshal.AsSpan(splineCurves);
            curveTToPositionLookupTable.EnsureCapacity(totalNodeCount * SampleSizePerCurve);
            float totalCurveDistance = 0;
            var previousPos = splineCurvesSpan[0].P0;
            for (int nodeIndex = 0; nodeIndex < splineCurves.Count; nodeIndex++)
            {
                ref var curve = ref splineCurvesSpan[nodeIndex];
                totalCurveDistance += (curve.P0 - previousPos).Length();
                previousPos = curve.P0;
                // First position is always just the initial value of the curve
                curveTToPositionLookupTable.Add(new CurveTToPositionTable
                {
                    NodeIndex = nodeIndex,
                    NodeCurveT = 0,
                    TotalSplineDistance = totalCurveDistance,
                    Position = curve.StartPosition
                });

                for (int i = 1; i < SampleSizePerCurve; i++)
                {
                    float currentT = dt * i;
                    var currentPos = curve.GetPosition(currentT);
                    totalCurveDistance += (currentPos - previousPos).Length();
                    previousPos = currentPos;
                    curveTToPositionLookupTable.Add(new CurveTToPositionTable
                    {
                        NodeIndex = nodeIndex,
                        NodeCurveT = currentT,
                        TotalSplineDistance = totalCurveDistance,
                        Position = currentPos
                    });
                }
            }

            // Add the final position (this is included even when IsClosedLoop = true, because it contains the full TotalCurveDistance)
            var finalPos = splineCurvesSpan[splineCurves.Count - 1].EndPosition;
            totalCurveDistance += (finalPos - previousPos).Length();
            curveTToPositionLookupTable.Add(new CurveTToPositionTable
            {
                NodeIndex = splineCurves.Count - 1,
                NodeCurveT = 1,
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

    private struct CurveTToPositionTable
    {
        /// <summary>
        /// The starting node this belongs to.
        /// </summary>
        public int NodeIndex;
        /// <summary>
        /// Value 0 to 1 from NodeIndex to (NodeIndex + 1)
        /// </summary>
        public float NodeCurveT;
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
