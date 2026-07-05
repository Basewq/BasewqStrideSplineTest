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
[DefaultEntityComponentProcessor(typeof(SplineProcessor), ExecutionMode = ExecutionMode.Runtime)]
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

    internal bool HasDebugRenderSettingsChanged = true;

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
        HasDebugRenderSettingsChanged = true;
    }

    private void OnSplineControlPointsChanged(object sender, ref SplineControlPointsChangedEventArgs e)
    {
        ControlPointsChanged?.Invoke(this);
    }

    private SplineDebugRenderSettings debugRenderSettings;
    /// <summary>
    /// A spline renderer is used to visualise the spline.
    /// </summary>
    [DataMember(50)]
    [Display(50, "Debug renderer")]
    public SplineDebugRenderSettings DebugRenderSettings
    {
        get => debugRenderSettings;
        internal set
        {
            debugRenderSettings?.RenderSettingsChanged -= OnRenderSettingsChanged;
            debugRenderSettings = value;
            debugRenderSettings?.RenderSettingsChanged += OnRenderSettingsChanged;
        }
    }

    private void OnRenderSettingsChanged(SplineDebugRenderSettings renderSettings)
    {
        HasDebugRenderSettingsChanged = true;
    }

    public SplineComponent()
    {
        Spline = new();
        DebugRenderSettings = new();
    }
}
