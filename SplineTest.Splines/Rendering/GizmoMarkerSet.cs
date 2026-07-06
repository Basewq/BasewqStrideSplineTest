// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Collections;
using Stride.Core.Mathematics;

namespace SplineTest.Rendering;

public class GizmoMarkerSet
{
    internal uint Version;

    private TrackingCollection<GizmoMarkerData> markers;
    public TrackingCollection<GizmoMarkerData> Markers
    {
        get => markers;
        set
        {
            markers?.CollectionChanged -= OnCollectionChanged;
            markers = value;
            markers?.CollectionChanged += OnCollectionChanged;

            Version = (Version == 0) ? 1u : 0;
        }
    }

    private void OnCollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
    {
        unchecked { Version++; }
    }

    public bool HasTransparency { get; set; } = true;

    public GizmoMarkerOccludedStyle OccludedStyle { get; set; }

    public GizmoMarkerSet()
    {
        Markers = [];
    }
}

public enum GizmoMarkerOccludedStyle : byte
{
    None,
    Dimmed,
    Checkered,
}

public enum GizmoMarkerShape : byte
{
    Circle,
    Box,
    Diamond,
}

public enum GizmoMarkerOrientationMode : byte
{
    /// <summary>
    /// Always faces the screen/camera.
    /// </summary>
    Billboard,
    /// <summary>
    /// Rotates towards the camera, but only around one axis.
    /// </summary>
    AxisBillboard,
    /// <summary>
    /// Orientates in world space.
    /// </summary>
    World,
}

public enum GizmoMarkerScaleMode : byte
{
    /// <summary>
    /// Fixed screen size regardless of camera position.
    /// </summary>
    FixedScreenSize,
    /// <summary>
    /// Scales according to distance from the camera.
    /// </summary>
    WorldSpace,
}

public struct GizmoMarkerData
{
    public GizmoMarkerShape Shape;
    public GizmoMarkerOrientationMode OrientationMode;
    public GizmoMarkerScaleMode ScaleMode;
    public Vector3 Position;
    public Color4 FillColor = Color4.White;
    /// <summary>
    /// Only applicable when <see cref="OrientationMode"/> is <see cref="GizmoMarkerOrientationMode.World"/>.
    /// </summary>
    public Quaternion Rotation = Quaternion.Identity;
    /// <summary>
    /// Only applicable when <see cref="OrientationMode"/> is <see cref="GizmoMarkerOrientationMode.AxisBillboard"/>.
    /// </summary>
    public Vector3 Axis = Vector3.UnitY;

    /// <summary>
    /// Size of the marker shape in pixels. This excludes the outline and glow.
    /// </summary>
    public Vector2 SizePx;

    public float OutlineWidthPx;
    public Color4 OutlineColor;

    public float GlowWidthPx;
    public Color4 GlowColor;

    public GizmoMarkerData()
    {
    }
}
