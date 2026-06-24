// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Specialized;

namespace Stride.Engine.Splines.Models;

public struct SplineControlPointsChangedEventArgs
{
    public SplineControlPointsChangedEventArgs(NotifyCollectionChangedAction action, bool collectionChanged = true)
    {
        Action = action;
        Item = default;
        OldItem = default;
        Index = -1;
        CollectionChanged = collectionChanged;
    }

    public SplineControlPointsChangedEventArgs(NotifyCollectionChangedAction action, SplineControlPoint item, SplineControlPoint oldItem, int index = -1, bool collectionChanged = true)
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
    public SplineControlPoint Item { get; private set; }

    /// <summary>
    /// Gets the previous value. Only valid if <see cref="Action"/> is <see cref="NotifyCollectionChangedAction.Add"/> and <see cref="NotifyCollectionChangedAction.Remove"/>
    /// </summary>
    public SplineControlPoint OldItem { get; private set; }

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
