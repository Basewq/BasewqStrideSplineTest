// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Processors;
using Stride.Core;
using Stride.Engine.Design;
using Stride.Engine.Splines.Models;

namespace Stride.Engine.Splines.Components;

[DataContract]
[ComponentCategory("Splines")]
[Display(name: "Spline", Expand = ExpandRule.Once)]
[DefaultEntityComponentProcessor(typeof(SplineEditorProcessor), ExecutionMode = ExecutionMode.Editor)]
public class SplineComponent : EntityComponent
{
    /// <summary>
    /// Event triggered when the spline has become dirty.
    /// </summary>
    public delegate void SplinePropertyChangedHandler(SplineComponent splineComponent);
    public event SplinePropertyChangedHandler SplinePropertyChanged;

    /// <summary>
    /// Event triggered when the spline control point has changed.
    /// </summary>
    public delegate void ControlPointsChangedHandler(SplineComponent splineComponent);
    public event ControlPointsChangedHandler ControlPointsChanged;
    /// <summary>
    /// Event triggered when the spline has become dirty.
    /// </summary>
    public delegate void SplineRenderSettingsChangedHandler(SplineComponent splineComponent);
    public event SplineRenderSettingsChangedHandler RenderSettingsChanged;

    [DataMember(1)]
    [Display("Editor control", Expand = ExpandRule.Once)]
    public EditSplineControl Control = new();

    private Spline spline;
    [DataMember(10)]
    [Display(10, Expand = ExpandRule.Once)]
    public Spline Spline
    {
        get => spline;
        internal set
        {
            spline?.SplinePropertyChanged -= OnSplinePropertyChanged;
            spline?.ControlPointsChanged -= OnSplineControlPointsChanged;
            spline = value;
            spline?.SplinePropertyChanged += OnSplinePropertyChanged;
            spline?.ControlPointsChanged += OnSplineControlPointsChanged;
        }
    }

    private void OnSplinePropertyChanged(object sender)
    {
        SplinePropertyChanged?.Invoke(this);
    }

    private void OnSplineControlPointsChanged(object sender, ref SplineControlPointsChangedEventArgs e)
    {
        ControlPointsChanged?.Invoke(this);
    }

    private SplineRenderSettings renderSettings;
    /// <summary>
    /// A spline renderer is used to visualise the spline.
    /// </summary>
    [Display(50, "Spline renderer")]
    public SplineRenderSettings RenderSettings
    {
        get => renderSettings;
        internal set
        {
            renderSettings?.RenderSettingsChanged -= OnRenderSettingsChanged;
            renderSettings = value;
            renderSettings?.RenderSettingsChanged += OnRenderSettingsChanged;
        }
    }

    private void OnRenderSettingsChanged()
    {
        RenderSettingsChanged?.Invoke(this);
    }

    public SplineComponent()
    {
        Spline = new();
        RenderSettings = new();
    }
}
