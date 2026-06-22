using SplineTest.Splines.Processors;
using Stride.Core;
using Stride.Engine.Design;
using Stride.Engine.Splines.Models;

namespace SplineTest.Splines.Components;

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
    /// Event triggered when the spline node has changed.
    /// </summary>
    public delegate void SplineNodeChangedHandler(SplineComponent splineComponent);
    public event SplineNodeChangedHandler SplineNodeChanged;
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
            spline?.NodeCollectionChanged -= OnSplineNodeCollectionChanged;
            spline = value;
            spline?.SplinePropertyChanged += OnSplinePropertyChanged;
            spline?.NodeCollectionChanged += OnSplineNodeCollectionChanged;
        }
    }

    private void OnSplinePropertyChanged(object sender)
    {
        SplinePropertyChanged?.Invoke(this);
    }

    private void OnSplineNodeCollectionChanged(object sender, ref SplineNodeCollectionChangedEventArgs e)
    {
        SplineNodeChanged?.Invoke(this);
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
