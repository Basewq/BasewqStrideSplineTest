// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Engine.Design;
using Stride.Engine.Splines.Models;
using Stride.Engine.Splines.Models.Decorators;
using Stride.Engine.Splines.Processors;

namespace Stride.Engine.Splines.Components;

/// <summary>
/// Component representing a Spline decorator.
/// </summary>
/// <remarks>
/// <para>
/// This component allows distribution of prefabs along the spline.
/// </para>
/// </remarks>
[DataContract("SplineDecoratorComponent")]
[Display("Spline decorator", Expand = ExpandRule.Once)]
[DefaultEntityComponentProcessor(typeof(SplineDecoratorProcessor))]
[ComponentCategory("Splines")]
public sealed class SplineDecoratorComponent : EntityComponent
{
    private SplineComponent splineComponent;
    private List<Entity> decorationInstances = new List<Entity>();
    private SplineDecoratorSettings decoratorSettings;

    [Display(10, "SplineComponent")]
    public SplineComponent SplineComponent
    {
        get { return splineComponent; }
        set
        {
            splineComponent?.Spline?.SplinePropertyChanged -= OnSplinePropertyChanged;
            splineComponent?.Spline?.ControlPointsChanged -= OnControlPointsChanged;
            splineComponent = value;
            splineComponent?.Spline?.SplinePropertyChanged += OnSplinePropertyChanged;
            splineComponent?.Spline?.ControlPointsChanged += OnControlPointsChanged;

            EnqueueSplineDecoratorUpdate();
        }
    }

    private void OnSplinePropertyChanged(object sender)
    {
        EnqueueSplineDecoratorUpdate();
    }

    private void OnControlPointsChanged(object sender, ref SplineControlPointsChangedEventArgs e)
    {
        EnqueueSplineDecoratorUpdate();
    }

    /// <summary>
    /// Event triggered when the splineDecorator has become dirty.
    /// </summary>
    public delegate void DirtySplineDecoratorHandler(SplineDecoratorComponent component);

    public event DirtySplineDecoratorHandler OnSplineDecoratorDirty;

    /// <summary>
    /// Invokes the Spline Traverser Update event.
    /// </summary>
    private void EnqueueSplineDecoratorUpdate()
    {
        OnSplineDecoratorDirty?.Invoke(this);
    }

    public SplineDecoratorComponent()
    {
    }

    public SplineDecoratorComponent(SplineDecoratorSettings decoratorSettings)
    {
        this.decoratorSettings = decoratorSettings;
    }

    /// <summary>
    /// Decorator settings of the decorator components.
    /// </summary>
    [DataMember(40)]
    [Display("Decorator settings")]
    public SplineDecoratorSettings DecoratorSettings
    {
        get { return decoratorSettings; }
        set
        {
            decoratorSettings?.DecoratorSettingsChanged -= OnDecoratorSettingsChanged;
            decoratorSettings = value;
            decoratorSettings?.DecoratorSettingsChanged += OnDecoratorSettingsChanged;
            EnqueueSplineDecoratorUpdate();
        }
    }

    private void OnDecoratorSettingsChanged(SplineDecoratorSettings renderSettings)
    {
        EnqueueSplineDecoratorUpdate();
    }

    /// <summary>
    /// All entity instances created and decorated along the spline.
    /// </summary>
    [DataMemberIgnore]
    public List<Entity> DecorationInstances
    {
        get { return decorationInstances; }
        set
        {
            decorationInstances = value;
        }
    }

    public void ClearDecorationInstances()
    {
        if (decorationInstances is null)
        {
            return;
        }

        foreach (var decorationInstance in decorationInstances)
        {
            Entity?.RemoveChild(decorationInstance);
            decorationInstance.Dispose();
        }

        decorationInstances.Clear();
    }

    internal void Update(TransformComponent transformComponent)
    {
    }
}
