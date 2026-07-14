// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Engine.Design;
using Stride.Engine.Splines.Models;
using Stride.Engine.Splines.Models.Mesh;
using Stride.Engine.Splines.Processors;

namespace Stride.Engine.Splines.Components;

/// <summary>
/// Component representing a Spline Mesh.
/// </summary>
[DataContract("SplineMeshComponent")]
[Display("Spline Mesh", Expand = ExpandRule.Once)]
[DefaultEntityComponentProcessor(typeof(SplineMeshTransformProcessor), ExecutionMode = ExecutionMode.All)]
[ComponentCategory("Splines")]
public sealed class SplineMeshComponent : EntityComponent
{
    public delegate void MeshRequiresUpdate(SplineMeshComponent component);

    public event MeshRequiresUpdate OnMeshRequiresUpdate;

    private SplineComponent splineComponent;
    private SplineMesh splineMesh;

    /// <summary>
    /// Spline mesh
    /// </summary>
    [DataMember(70)]
    [Display("Spline Mesh")]
    public SplineMesh SplineMesh
    {
        get
        {
            return splineMesh;
        }
        set
        {
            splineMesh = value;

            if (splineMesh is not null)
            {
                InvalidateMesh();
            }
        }
    }

    /// <summary>
    /// The spline to place the mesh on.
    /// </summary>
    [Display(10, "Spline")]
    public SplineComponent SplineComponent
    {
        get { return splineComponent; }
        set
        {
            var oldValue = splineComponent;
            splineComponent?.Spline?.SplinePropertyChanged -= OnSplinePropertyChanged;
            splineComponent?.Spline?.ControlPointsChanged -= OnControlPointsChanged;
            splineComponent = value;
            splineComponent?.Spline?.SplinePropertyChanged += OnSplinePropertyChanged;
            splineComponent?.Spline?.ControlPointsChanged += OnControlPointsChanged;
            if (splineComponent is not null && oldValue != splineComponent)
            {
                InvalidateMesh();
            }
        }
    }

    private void OnSplinePropertyChanged(object sender)
    {
        InvalidateMesh();
    }

    private void OnControlPointsChanged(object sender, ref SplineControlPointsChangedEventArgs e)
    {
        InvalidateMesh();
    }

    public void InvalidateMesh()
    {
        if (SplineMesh is null)
        {
            return;
        }

        OnMeshRequiresUpdate?.Invoke(this);
    }

    internal void Update(TransformComponent transformComponent)
    {
    }
}
