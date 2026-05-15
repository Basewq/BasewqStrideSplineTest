// Copyright (c) Stride contributors (https://Stride.com)
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
    private bool showNodes;
    private Material segmentsMaterial;
    private Material boundingBoxMaterial;
    private Material nodesMaterial;

    public delegate void SplineRenderSettingsChangedHandler();

    public event SplineRenderSettingsChangedHandler RenderSettingsChanged;

    /// <summary>
    /// Display spline curve mesh
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
    /// Display Spline nodes
    /// </summary>
    [Display(23, "Show nodes")]
    public bool ShowNodes
    {
        get => showNodes;
        set => SetField(ref showNodes, value);
    }

    /// <summary>
    /// The material used by the spline nodes mesh
    /// </summary>
    [Display(26, "Nodes material")]
    public Material NodesMaterial
    {
        get => nodesMaterial;
        set => SetField(ref nodesMaterial, value);
    }

    /// <summary>
    /// Display the bounding boxes of each node and the entire spline
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
    /// The render group used to when displaying the curve segment, node and bounding box.
    /// </summary>
    public RenderGroup RenderGroup { get; set; } = RenderGroup.Group4;

    private void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        RenderSettingsChanged?.Invoke();
    }
}
