// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Collections;
using Stride.Core.Mathematics;
using System.Collections;

namespace Stride.Engine.Splines.Models;

public delegate void SplinePropertyChangedEventHandler(object sender);

public delegate void SplineControlPointEventHandler<TEventArgs>(object sender, ref TEventArgs e);

[DataContract]
[Display(Expand = ExpandRule.Once)]
public class Spline
{
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
            SetField(ref isClosedLoop, value);
        }
    }

    private Vector3 initialUpDirection = Vector3.UnitY;
    public Vector3 InitialUpDirection
    {
        get => initialUpDirection;
        set
        {
            SetField(ref initialUpDirection, value);
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
        SplineControlPoint newItem = (SplineControlPoint?)e.Item ?? default;
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
            ControlPoints[index] = value;
        }
    }

    /// <summary>
    /// Gets the number of control points in this spline.
    /// </summary>
    public int Count => ControlPoints.Count;

    public int CurveCount => IsClosedLoop ? ControlPoints.Count : Math.Max(ControlPoints.Count - 1, 0);

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
            TangentOut = Vector3.Zero,
            Type = SplineControlPointType.Auto,
        };
        Add(newControlPoint);
    }

    public void Add(SplineControlPoint controlPoint)
    {
        ControlPoints.Add(controlPoint);
    }

    public void RemoveAt(int index)
    {
        ControlPoints.RemoveAt(index);
    }

    public bool Contains(SplineControlPoint item) => ControlPoints.Contains(item);

    public void CopyTo(SplineControlPoint[] array, int arrayIndex) => ControlPoints.CopyTo(array, arrayIndex);

    public void Clear()
    {
        ControlPoints.Clear();
    }

    public BezierCurve GetCurve(int curveIndex)
    {
        int nextCurveIndex = curveIndex + 1;
        if (IsClosedLoop)
        {
            nextCurveIndex = nextCurveIndex % controlPoints.Count;
        }
        else if (nextCurveIndex >= controlPoints.Count)
        {
            throw new IndexOutOfRangeException($"{nameof(curveIndex)} is greater than the number of curves.");
        }

        var curve = new BezierCurve(controlPoints[curveIndex], controlPoints[nextCurveIndex]);
        return curve;
    }

    public Enumerator GetEnumerator() => new Enumerator(controlPoints);

    //IEnumerator<SplineControlPoint> IEnumerable<SplineControlPoint>.GetEnumerator() => GetEnumerator();

    //IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        SplinePropertyChanged?.Invoke(this);
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
