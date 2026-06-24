// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Rendering;
using Stride.Core;
namespace Stride.Engine.Splines.Models;

[DataContract]
public class SplineRenderSettings
{
    // Currently unused class

    private bool showSegments;
    private bool showBoundingBox;
    private bool showControlPoints;
    private Material segmentsMaterial;
    private Material boundingBoxMaterial;
    private Material controlPointsMaterial;

    public delegate void SplineRenderSettingsChangedHandler();

    public event SplineRenderSettingsChangedHandler RenderSettingsChanged;

    /// <summary>
    /// Display spline segment mesh
    /// </summary>
    [Display(10, "Show segments")]
    public bool ShowSegments
    {
        get => showSegments;
        set => SetField(ref showSegments, value);
    }

    /// <summary>
    /// The material used by the spline mesh
    /// </summary>
    [Display(20, "Segments material")]
    public Material SegmentsMaterial
    {
        get => segmentsMaterial;
        set => SetField(ref segmentsMaterial, value);
    }

    /// <summary>
    /// Display spline control points
    /// </summary>
    [Display(23, "Show control points")]
    public bool ShowControlPoints
    {
        get => showControlPoints;
        set => SetField(ref showControlPoints, value);
    }

    /// <summary>
    /// The material used by the spline control points mesh
    /// </summary>
    [Display(26, "Control points material")]
    public Material ControlPointsMaterial
    {
        get => controlPointsMaterial;
        set => SetField(ref controlPointsMaterial, value);
    }

    /// <summary>
    /// Display the bounding boxes of each control point and the entire spline
    /// </summary>
    [Display(30, "Show bounding box")]
    public bool ShowBoundingBox
    {
        get => showBoundingBox;
        set => SetField(ref showBoundingBox, value);
    }

    /// <summary>
    /// The material used by the spline boundingboxes
    /// </summary>
    [Display(40, "Bounding box material")]
    public Material BoundingBoxMaterial
    {
        get => boundingBoxMaterial;
        set => SetField(ref boundingBoxMaterial, value);
    }

    /// <summary>
    /// The render group used to when displaying the spline segments, control points and bounding boxes.
    /// </summary>
    public RenderGroup RenderGroup { get; set; } = RenderGroup.Group4;

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        RenderSettingsChanged?.Invoke();
    }
}
