// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Engine.Design;
using Stride.Engine.Splines.Models;
using Stride.Engine.Splines.Processors;

namespace Stride.Engine.Splines.Components;

/// <summary>
/// Component representing a Spline Traverser.
/// </summary>
[DataContract("SplineTraverserComponent")]
[Display("Spline Traverser", Expand = ExpandRule.Once)]
[DefaultEntityComponentProcessor(typeof(SplineTraverserProcessor), ExecutionMode = ExecutionMode.Runtime)]
[ComponentCategory("Splines")]
public sealed class SplineTraverserComponent : EntityComponent
{
    private SplineTraverser splineTraverser;
    private SplineComponent splineComponent;

    /// <summary>
    /// SplineTraverser object
    /// </summary>
    [DataMemberIgnore]
    public SplineTraverser SplineTraverser
    {
        get
        {
            splineTraverser ??= new SplineTraverser();
            return splineTraverser;
        }
        set
        {
            splineTraverser = value;
            //splineTraverser.EnqueueSplineTraverserUpdate();
        }
    }

    /// <summary>
    /// The spline to traverse
    /// No spline, no movement
    /// </summary>
    [Display(10, "Spline")]
    public SplineComponent SplineComponent
    {
        get { return splineComponent; }
        set
        {
            splineComponent = value;

            if (splineComponent is null)
            {
                SplineTraverser.Spline = null;
            }
        }
    }

    /// <summary>
    /// The speed at which the traverser moves over the spline. Use a negative value, to go in to the opposite direction
    /// Note: Using a high value, can cause jitters.
    /// With a higher speed value, it is recommended to reduced the amount of spline points or segments
    /// </summary>
    [Display(20, "Speed")]
    public float Speed
    {
        get { return SplineTraverser.Speed; }
        set
        {
            SplineTraverser.Speed = value;
        }
    }

    /// <summary>
    /// Determines whether the spline traver is moving
    /// For a traverser to work we require a Spline reference, a non-zero and IsMoving must be True
    /// </summary>
    [Display(40, "Moving")]
    public bool IsMoving
    {
        get
        {
            return SplineTraverser.IsMoving;
        }
        set
        {
            SplineTraverser.IsMoving = value;
        }
    }

    /// <summary>
    /// Determines whether the spline traver rotates along the spline
    /// For a traverse to work we require a Spline reference, a non-zero and IsMoving must be True
    /// </summary>
    [Display(50, "Rotate")]
    public bool IsRotating
    {
        get
        {
            return SplineTraverser.IsRotating;
        }
        set
        {
            SplineTraverser.IsRotating = value;
        }
    }

    internal void Update(TransformComponent transformComponent)
    {
        var splineSample = SplineTraverser.CurrentSplineSample;
        transformComponent.Position = splineSample.Position;
        transformComponent.Rotation = splineSample.Rotation;
    }
}
