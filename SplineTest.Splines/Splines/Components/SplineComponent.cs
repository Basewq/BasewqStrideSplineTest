// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Engine.Splines.Processors;
using Stride.Core;
using Stride.Core.Annotations;
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

    private SplineDebugRenderSettings debugRenderSettings;
    /// <summary>
    /// A spline renderer is used to visualise the spline.
    /// </summary>
    [DataMember(0)]
    [Display(0, "Debug renderer")]
    public SplineDebugRenderSettings DebugRenderSettings
    {
        get => debugRenderSettings;
        internal set
        {
            debugRenderSettings?.RenderSettingsChanged -= OnDebugRenderSettingsChanged;
            debugRenderSettings = value;
            debugRenderSettings?.RenderSettingsChanged += OnDebugRenderSettingsChanged;
        }
    }

    private void OnDebugRenderSettingsChanged(SplineDebugRenderSettings renderSettings)
    {
        HasDebugRenderSettingsChanged = true;
    }

    private ISplineEvaluator splineEvaluator;
    [Display(49)]
    [NotNull]
    public ISplineEvaluator SplineEvaluator
    {
        get => splineEvaluator;
        set
        {
            splineEvaluator?.UnregisterSpline();
            splineEvaluator = value;
            if (Spline is not null)
            {
                splineEvaluator?.RegisterSpline(Spline);
            }
        }
    }

    private Spline spline;
    [DataMember(50)]
    [Display(50, Expand = ExpandRule.Once)]
    [NotNull]
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

            if (spline is not null)
            {
                splineEvaluator?.RegisterSpline(spline);
            }
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

    public SplineComponent()
    {
        Spline = new();
        SplineEvaluator = new SplineEvaluator();
        DebugRenderSettings = new();
    }
}
