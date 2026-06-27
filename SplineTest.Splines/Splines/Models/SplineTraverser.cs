// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using System.Diagnostics;

namespace Stride.Engine.Splines.Models;

[DataContract]
public class SplineTraverser
{
    private bool isSplineDirty = true;

    private float splineTotalDistance = 0;
    private float currentTravelDistance = 0;

    private Spline spline;
    //public Entity entity;
    private float speed = 1.0f;
    private bool isMoving = false;
    private bool isRotating = false;

    /// <summary>
    /// Event triggered when a spline control point has been reached.
    /// Does not get triggered when the last control point of the spline has been reached and <see cref="Spline.IsClosedLoop"/> is false.
    /// Does not get triggered on the first control point when initially placed at the first control point.
    /// </summary>
    public delegate void SplineTraverserControlPointReachedHandler(int controlPointIndex, SplineControlPoint controlPoint);

    public event SplineTraverserControlPointReachedHandler SplineControlPointReached;

    /// <summary>
    /// Event triggered when the last control point of the spline has been reached.
    /// Does not get triggered if <see cref="Spline.IsClosedLoop"/> is true.
    /// </summary>
    public delegate void SplineTraverserEndReachedHandler(SplineControlPoint controlPoint);

    public event SplineTraverserEndReachedHandler SplineEndReached;

    /// <summary>
    /// The spline to traverse.
    /// No spline, no movement.
    /// </summary>
    public Spline Spline
    {
        get => spline;
        set
        {
            spline = value;

            if (spline is null)
            {
                isMoving = false;
            }

            isSplineDirty = true;
        }
    }

    /// <summary>
    /// The speed at which the traverser moves over the spline.
    /// Use a negative value, to go in to the opposite direction.
    /// </summary>
    public float Speed
    {
        get => speed;
        set
        {
            speed = value;
        }
    }

    /// <summary>
    /// Determines whether the spline traver is moving.
    /// For a traverser to work we require a Spline reference, a non-zero and IsMoving must be True.
    /// </summary>
    public bool IsMoving
    {
        get => isMoving;
        set
        {
            isMoving = value;
        }
    }

    /// <summary>
    /// Determines whether the spline traver rotates along the spline.
    /// For a traverse to work we require a Spline reference, a non-zero and IsMoving must be True.
    /// </summary>
    public bool IsRotating
    {
        get => isRotating;
        set
        {
            isRotating = value;
        }
    }

    private SplineSample currentSplineSample;
    public SplineSample CurrentSplineSample
    {
        get => currentSplineSample;
    }

    public SplineTraverser()
    {
    }

    public void ForceUpdate()
    {
        if (spline is null)
        {
            return;
        }
        UpdateSplineInfo();
        UpdatePlacement(dt: 0, forceUpdate: true);
    }

    public void Update(float dt)
    {
        if (spline is null)
        {
            return;
        }

        if (!isMoving || speed == 0)
        {
            return;
        }

        UpdateSplineInfo();
        UpdatePlacement(dt);
    }

    private void UpdateSplineInfo()
    {
        if (!isSplineDirty)
        {
            return;
        }

        splineTotalDistance = spline.GetTotalDistance();

        currentTravelDistance = Math.Clamp(currentTravelDistance, min: 0, max: splineTotalDistance);

        isSplineDirty = false;
    }

    private readonly List<IndexedSplineControlPoint> controlPointsEncountered = [];
    private void UpdatePlacement(float dt, bool forceUpdate = false)
    {
        float previousTravelDistance = currentTravelDistance;

        float displacement = speed * dt;
        currentTravelDistance += displacement;

        bool isloop = false;

        if ((splineTotalDistance - currentTravelDistance) < 0.1f)
        {
        }
        if (currentTravelDistance > splineTotalDistance)
        {
            isloop = true;
        }
        if (!spline.IsClosedLoop)
        {
            currentTravelDistance = Math.Clamp(currentTravelDistance, min: 0, max: splineTotalDistance);
        }

        if (currentTravelDistance != previousTravelDistance || forceUpdate)
        {
            // TODO add traverser reached start event?

            if (SplineControlPointReached is not null)
            {
                spline.CollectEncounteredControlPoints(controlPointsEncountered, previousTravelDistance, currentTravelDistance);
                for (int i = 0; i < controlPointsEncountered.Count; i++)
                {
                    var indexedPoint = controlPointsEncountered[i];
                    if (!spline.IsClosedLoop && indexedPoint.ControlPointIndex == spline.Count - 1)
                    {
                        // Call SplineEndReached instead
                        continue;
                    }
                    SplineControlPointReached.Invoke(indexedPoint.ControlPointIndex, indexedPoint.ControlPoint);
                }
                controlPointsEncountered.Clear();
            }

            if (!spline.IsClosedLoop && currentTravelDistance >= splineTotalDistance)
            {
                // Traverser at the end
                var endControlPoint = spline[^1];
                SplineEndReached?.Invoke(endControlPoint);
            }

            if (spline.IsClosedLoop)
            {
                while (currentTravelDistance >= splineTotalDistance)
                {
                    currentTravelDistance -= splineTotalDistance;
                }
                while (currentTravelDistance < 0)
                {
                    currentTravelDistance += splineTotalDistance;
                }
            }
            var prevPos = currentSplineSample.Position;
            currentSplineSample = spline.SampleFromDistance(currentTravelDistance);
        }
    }

    public void SnapPositionToSpline(Vector3 position)
    {
        var splinePositionInfo = spline.GetClosestPointOnSpline(position);
        currentSplineSample = spline.SampleFromDistance(splinePositionInfo.SplineDistance);
    }
}
