// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System.Collections.Generic;
using Stride.Core;
using Stride.Core.Collections;

namespace Stride.Engine.Splines.Models.Decorators;

[DataContract(Inherited = true)]
public abstract class SplineDecoratorSettings
{
    public delegate void SplineDecoratorSettingsChangedHandler(SplineDecoratorSettings renderSettings);

    public event SplineDecoratorSettingsChangedHandler? DecoratorSettingsChanged;

    /// <summary>
    /// A list of prefabs that the decorators uses to instantiate.
    /// </summary>
    [Display(20, "Decorations")]
    public TrackingCollection<Prefab> Decorations { get; } = [];

    private SplineDecoratorInstanceEnum spawnOrder;
    /// <summary>
    /// The way that decorations are spawned.
    /// </summary>
    [Display(30, "Spawn order")]
    public SplineDecoratorInstanceEnum SpawnOrder
    {
        get => spawnOrder;
        set => SetField(ref spawnOrder, value);
    }

    protected SplineDecoratorSettings()
    {
        Decorations.CollectionChanged += OnDecorationsCollectionChanged;
    }

    private void OnDecorationsCollectionChanged(object sender, TrackingCollectionChangedEventArgs e)
    {
        DecoratorSettingsChanged?.Invoke(this);
    }

    protected void SetField<T>(ref T backingField, T newValue)
    {
        backingField = newValue;
        DecoratorSettingsChanged?.Invoke(this);
    }
}

public enum SplineDecoratorInstanceEnum
{
    /// <summary>
    /// The prefabs in the decorations list are randomly picked for instantiation.
    /// </summary>
    Random,

    /// <summary>
    /// The prefabs in the decorations list are picked in sequential order.
    /// </summary>
    Sequential
}
