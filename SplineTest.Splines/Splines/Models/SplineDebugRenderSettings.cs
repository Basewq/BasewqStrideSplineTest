// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Rendering;
using Stride.Core;
using Stride.Core.Mathematics;
namespace Stride.Engine.Splines.Models;

[DataContract]
public class SplineDebugRenderSettings
{
    // Currently unused class


    public delegate void SplineRenderSettingsChangedHandler(SplineDebugRenderSettings renderSettings);

    public event SplineRenderSettingsChangedHandler? RenderSettingsChanged;

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
    [Display(20, "Curve color")]
    public Color CurveColor
    {
        get => curveColor;
        set => SetField(ref curveColor, value);
    }

    private bool showControlPoints;
    /// <summary>
    /// Display spline control points.
    /// </summary>
    [Display(23, "Show control points")]
    public bool ShowControlPoints
    {
        get => showControlPoints;
        set => SetField(ref showControlPoints, value);
    }

    private Color controlPointColor = Color.White;
    /// <summary>
    /// The color used by the spline control points mesh.
    /// </summary>
    [Display(26, "Control point color")]
    public Color ControlPointColor
    {
        get => controlPointColor;
        set => SetField(ref controlPointColor, value);
    }

    private bool showBoundingBox;
    /// <summary>
    /// Display the bounding boxes of each control point and the entire spline.
    /// </summary>
    [Display(30, "Show bounding box")]
    public bool ShowBoundingBox
    {
        get => showBoundingBox;
        set => SetField(ref showBoundingBox, value);
    }

    private Color boundingBoxColor = Color.Orange;
    /// <summary>
    /// The color used by the spline bounding boxes.
    /// </summary>
    [Display(40, "Bounding box color")]
    public Color BoundingBoxColor
    {
        get => boundingBoxColor;
        set => SetField(ref boundingBoxColor, value);
    }

    /// <summary>
    /// The render group used to when displaying the spline curves, control points and bounding boxes.
    /// </summary>
    public RenderGroup RenderGroup { get; set; } = RenderGroup.Group4;

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        RenderSettingsChanged?.Invoke(this);
    }
}
