// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Rendering;
using System.ComponentModel;

namespace Stride.Engine.Splines.Models;

[DataContract]
public class SplineDebugRenderSettings
{
    public delegate void SplineRenderSettingsChangedHandler(SplineDebugRenderSettings renderSettings);

    public event SplineRenderSettingsChangedHandler? RenderSettingsChanged;

    private int segmentsPerCurve = 16;
    /// <summary>
    /// Number of line segments between each control point to display.
    /// </summary>
    [Display(5, "Segments per curve")]
    [DefaultValue(16)]
    public int SegmentsPerCurve
    {
        get => segmentsPerCurve;
        set => SetField(ref segmentsPerCurve, value);
    }

    private bool showCurves;
    /// <summary>
    /// Display spline curve mesh.
    /// </summary>
    [Display(10, "Show curves")]
    public bool ShowCurves
    {
        get => showCurves;
        set => SetField(ref showCurves, value);
    }

    private Color curveColor = Color.Aqua;
    /// <summary>
    /// The color used by the spline curves.
    /// </summary>
    [Display(11, "Curve color")]
    [DefaultValue(typeof(Color), "#FF00FFFF")]
    public Color CurveColor
    {
        get => curveColor;
        set => SetField(ref curveColor, value);
    }

    private bool showTangents;
    /// <summary>
    /// Display spline tangents.
    /// </summary>
    [Display(20, "Show tangent")]
    public bool ShowTangents
    {
        get => showTangents;
        set => SetField(ref showTangents, value);
    }

    private Color controlTangentColor = Color.LightCoral;
    /// <summary>
    /// The color used by the spline tangents.
    /// </summary>
    [Display(21, "Tangent color")]
    [DefaultValue(typeof(Color), "#FFF08080")]
    public Color TangentColor
    {
        get => controlTangentColor;
        set => SetField(ref controlTangentColor, value);
    }

    private bool showUpDirections;
    /// <summary>
    /// Display spline up directions.
    /// </summary>
    [Display(20, "Show up directions")]
    public bool ShowUpDirections
    {
        get => showUpDirections;
        set => SetField(ref showUpDirections, value);
    }

    private Color controlUpDirectionColor = Color.LightGreen;
    /// <summary>
    /// The color used by the spline tangents.
    /// </summary>
    [Display(21, "Up direction color")]
    [DefaultValue(typeof(Color), "#FF90EE90")]
    public Color UpDirectionColor
    {
        get => controlUpDirectionColor;
        set => SetField(ref controlUpDirectionColor, value);
    }

    private bool showControlPoints;
    /// <summary>
    /// Display spline control points.
    /// </summary>
    [Display(30, "Show control points")]
    public bool ShowControlPoints
    {
        get => showControlPoints;
        set => SetField(ref showControlPoints, value);
    }

    private Color controlPointColor = Color.White;
    /// <summary>
    /// The color used by the spline control points marker.
    /// </summary>
    [Display(31, "Control point color")]
    [DefaultValue(typeof(Color), "#FFFFFFFF")]
    public Color ControlPointColor
    {
        get => controlPointColor;
        set => SetField(ref controlPointColor, value);
    }

    private bool showBoundingBox;
    /// <summary>
    /// Display the bounding boxes of each control point and the entire spline.
    /// </summary>
    [Display(40, "Show bounding box")]
    public bool ShowBoundingBox
    {
        get => showBoundingBox;
        set => SetField(ref showBoundingBox, value);
    }

    private Color boundingBoxColor = Color.Orange;
    /// <summary>
    /// The color used by the spline bounding boxes.
    /// </summary>
    [Display(41, "Bounding box color")]
    [DefaultValue(typeof(Color), "#FFFFA500")]
    public Color BoundingBoxColor
    {
        get => boundingBoxColor;
        set => SetField(ref boundingBoxColor, value);
    }

    private RenderGroup renderGroup = RenderGroup.Group31;
    /// <summary>
    /// The render group used to when displaying the spline features.
    /// </summary>
    [Display(50)]
    [DefaultValue(RenderGroup.Group31)]
    public RenderGroup RenderGroup
    {
        get => renderGroup;
        set => SetField(ref renderGroup, value);
    }

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        RenderSettingsChanged?.Invoke(this);
    }
}
