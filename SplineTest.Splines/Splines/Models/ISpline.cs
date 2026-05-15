// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Specialized;
using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public interface ISpline
{
    /// <summary>
    /// Gets or sets a value indicating whether the last spline node connects back to the first spline node to form a loop.
    /// Default is <c>false</c>.
    /// </summary>
    /// <remarks>
    /// This is only applicable where there are at least two spline nodes.
    /// </remarks>
    bool IsClosedLoop { get; set; }
    /// <summary>
    /// Gets the number of nodes in this spline.
    /// </summary>
    int Count { get; }

    void Add(Vector3 position);
    void RemoveAt(int index);
    void Clear();

    /// <summary>
    /// Returns the total distance over the entire spline.
    /// </summary>
    float GetTotalDistance();

    /// <summary>
    /// Returns the position on the spline curve closest to <paramref name="position"/>.
    /// </summary>
    /// <param name="position">Position in the spline's local space.</param>
    SplineClosestPositionInfo GetClosestPointOnSpline(Vector3 position);

    void CollectSplinePlacements(List<SplinePlacement> splinePlacements);

    SplinePlacement GetPlacementFromSplineDistance(float splineDistance);
}

public delegate void SplinePropertyChangedEventHandler(object sender);

public delegate void SplineNodeEventHandler<TEventArgs>(object sender, ref TEventArgs e);

public interface ISpline<TNode> : ISpline
    where TNode : ISplineNode
{
    event SplinePropertyChangedEventHandler SplinePropertyChanged;
    event SplineNodeEventHandler<SplineNodeCollectionChangedEventArgs<TNode>> NodeCollectionChanged;

    TNode this[int index] { get; }
    void Add(TNode node);
}

public struct SplineNodeCollectionChangedEventArgs<TNode>
{
    public SplineNodeCollectionChangedEventArgs(NotifyCollectionChangedAction action, bool collectionChanged = true)
    {
        Action = action;
        Item = default;
        OldItem = default;
        Index = -1;
        CollectionChanged = collectionChanged;
    }

    public SplineNodeCollectionChangedEventArgs(NotifyCollectionChangedAction action, TNode item, TNode oldItem, int index = -1, bool collectionChanged = true)
    {
        Action = action;
        Item = item;
        OldItem = oldItem;
        Index = index;
        CollectionChanged = collectionChanged;
    }

    /// <summary>
    /// Gets the type of action performed.
    /// Allowed values are <see cref="NotifyCollectionChangedAction.Add"/> and <see cref="NotifyCollectionChangedAction.Remove"/>.
    /// </summary>
    public NotifyCollectionChangedAction Action { get; private set; }

    /// <summary>
    /// Gets the added or removed item (if dictionary, value only).
    /// </summary>
    public TNode Item { get; private set; }

    /// <summary>
    /// Gets the previous value. Only valid if <see cref="Action"/> is <see cref="NotifyCollectionChangedAction.Add"/> and <see cref="NotifyCollectionChangedAction.Remove"/>
    /// </summary>
    public TNode OldItem { get; private set; }

    /// <summary>
    /// Gets the index in the collection (if applicable).
    /// </summary>
    public int Index { get; private set; }

    /// <summary>
    /// Gets a value indicating whether [collection changed (not a replacement but real insertion/removal)].
    /// </summary>
    /// <value>
    ///   <c>true</c> if [collection changed]; otherwise, <c>false</c>.
    /// </value>
    public bool CollectionChanged { get; private set; }
}
