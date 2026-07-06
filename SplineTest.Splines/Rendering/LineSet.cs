// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Collections;
using Stride.Core.Mathematics;

namespace SplineTest.Rendering;

public class LineSet
{
    internal uint Version;

    private TrackingCollection<LineSegment> segments;
    public TrackingCollection<LineSegment> Segments
    {
        get => segments;
        set
        {
            segments?.CollectionChanged -= OnCollectionChanged;
            segments = value;
            segments?.CollectionChanged += OnCollectionChanged;

            Version = (Version == 0) ? 1u : 0;
        }
    }

    private void OnCollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
    {
        unchecked { Version++; }
    }

    public bool HasTransparency { get; set; } = true;

    public LineOccludedStyle OccludedStyle { get; set; }

    public LineSet()
    {
        Segments = [];
    }
}

public enum LineOccludedStyle : byte
{
    None,
    Dimmed,
    Checkered,
}

public enum LineMode : byte
{
    /// <summary>
    /// Line that sits completely in world space.
    /// </summary>
    WorldSpace,
    /// <summary>
    /// Line orientated in world space with length scaled in screen space.
    /// </summary>
    ViewScaled,
    /// <summary>
    /// Line orientated in world space with length fixed in screen space.
    /// </summary>
    FixedScreenLength,
}

public struct LineSegment
{
    public LineMode LineMode;
    public Vector3 StartPosition;
    public Vector3 EndPosition;
    public Color4 StartColor;
    public Color4 EndColor;
    public float LineThicknessPx;
    /// <summary>
    /// Only applicable when <see cref="LineMode"/> is <see cref="LineMode.ViewScaled"/> or <see cref="LineMode.FixedScreenLength"/>.
    /// </summary>
    public float FixedLengthPx;


}
