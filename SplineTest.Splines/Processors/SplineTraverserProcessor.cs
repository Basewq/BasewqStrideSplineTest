// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.HierarchyTransformOperations;
using Stride.Engine.Splines.Models;
using Stride.Games;

namespace Stride.Engine.Splines.Processors;

/// <summary>
/// The processor for <see cref="SplineTraverserComponent"/>.
/// </summary>
public class SplineTraverserProcessor : EntityProcessor<SplineTraverserComponent, SplineTraverserProcessor.AssociatedData>
{
    private HashSet<SplineTraverserComponent> splineTraverserComponents = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SplineProcessor"/> class.
    /// </summary>
    public SplineTraverserProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineTraverserComponent component)
    {
        var data = new AssociatedData(this, component)
        {
            TransformOperation = new SplineTraverserViewHierarchyTransformOperation(component)
        };

        return data;
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineTraverserComponent component, AssociatedData associatedData)
    {
        return component == associatedData.TransformOperation.SplineTraverserComponent;
    }

    protected override void OnEntityComponentAdding(Entity entity, SplineTraverserComponent component, AssociatedData data)
    {
        if (component.SplineComponent is not null)
        {
            component.SplineComponent.Spline.SplinePropertyChanged += data.OnSplinePropertyChangedAction;
            component.SplineComponent.Spline.ControlPointsChanged += data.OnSplineControlPointCollectionChangedAction;
        }

        splineTraverserComponents.Add(component);
        component.SplineTraverser.Spline = component.SplineComponent?.Spline;
        //component.SplineTraverser.Entity = entity;

        entity.Transform.PostOperations.Add(data.TransformOperation);
    }

    protected override void OnEntityComponentRemoved(Entity entity, SplineTraverserComponent component, AssociatedData data)
    {
        component.SplineComponent?.Spline.SplinePropertyChanged -= data.OnSplinePropertyChangedAction;
        component.SplineComponent?.Spline.ControlPointsChanged -= data.OnSplineControlPointCollectionChangedAction;
        entity.Transform.PostOperations.Remove(data.TransformOperation);
        splineTraverserComponents.Remove(component);
    }

    public class AssociatedData
    {
        public SplineTraverserViewHierarchyTransformOperation TransformOperation;
        public SplinePropertyChangedEventHandler OnSplinePropertyChangedAction;
        public SplineControlPointEventHandler<SplineControlPointsChangedEventArgs> OnSplineControlPointCollectionChangedAction;

        internal SplineSample PreviousSplineSample;
        internal bool AttachedToSpline;

        public AssociatedData(SplineTraverserProcessor processor, SplineTraverserComponent component)
        {
            OnSplinePropertyChangedAction = sender => Update(processor, component);
            OnSplineControlPointCollectionChangedAction = (sender, ref e) => Update(processor, component);
        }

        private void Update(SplineTraverserProcessor processor, SplineTraverserComponent component)
        {
            processor.splineTraverserComponents.Add(component);
            AttachedToSpline = false;
        }
    }

    public override void Update(GameTime gameTime)
    {
        foreach (var (component, data) in ComponentDatas)
        {
            if (!IsValidComponent(component))
                continue;

            if (!data.AttachedToSpline)
            {
                DetermineOriginAndTarget(component, data);
            }

            if (!component.IsMoving || component.Speed == 0 || !data.AttachedToSpline)
            {
                continue;
            }

            float dt = (float)gameTime.Elapsed.TotalSeconds;
            component.SplineTraverser.Update(dt);
            var curSplineSample = component.SplineTraverser.CurrentSplineSample;
            //component.Entity.Transform.Position = curSplineSample.Position;
            //if (component.SplineTraverser.IsRotating)
            //{
            //    component.Entity.Transform.Rotation = curSplineSample.Rotation;
            //}

            data.PreviousSplineSample = curSplineSample;


            ////float dt = (float)gameTime.Elapsed.TotalSeconds;
            ////UpdatePosition(component, dt);
            ////UpdateRotation(component, dt);

            //////Avoid square root check. Using LengthSquared
            ////var distanceSquared = (component.Entity.Transform.WorldMatrix.TranslationVector - component.SplineTraverser.targetBezierPoint.Position).LengthSquared();
            ////if (distanceSquared < component.SplineTraverser.thresholdDistance * component.SplineTraverser.thresholdDistance)
            ////{
            ////    SetNextTarget(component);
            ////}
        }
    }

    private static bool IsValidComponent(SplineTraverserComponent component)
    {
        if (component is { Entity: not null, SplineComponent.Spline: not null })
        {
            return true;
        }

        Console.WriteLine($"[Warning] Invalid component or missing Spline: {component}");
        return false;
    }

    private void DetermineOriginAndTarget(SplineTraverserComponent component, AssociatedData data)
    {
        if (component.Entity is null || !(component.SplineComponent.Spline?.Count > 1))
        {
            return;
        }


        var currentPositionOfTraverser = component.Entity.Transform.WorldMatrix.TranslationVector;
        component.SplineTraverser.SnapPositionToSpline(currentPositionOfTraverser);

        var curSplineSample = component.SplineTraverser.CurrentSplineSample;
        component.Entity.Transform.Position = curSplineSample.Position;
        if (component.SplineTraverser.IsRotating)
        {
            var forwardRotation = Quaternion.BetweenDirections(Vector3.UnitZ, curSplineSample.Tangent);
            component.Entity.Transform.Rotation = curSplineSample.Rotation * forwardRotation;
        }

        data.AttachedToSpline = true;

        ////var spline = component.SplineComponent.Spline;
        ////var splinePositionInfo = spline.GetClosestPointOnSpline(currentPositionOfTraverser);
        ////bool isMovingForward = component.Speed >= 0;

        ////component.SplineTraverser.targetSplineControlPointIndex = isMovingForward ? splinePositionInfo.SplineControlPointBIndex : splinePositionInfo.SplineControlPointAIndex;

        ////component.SplineTraverser.originSplineControlPoint = spline[isMovingForward ?  splinePositionInfo.SplineControlPointAIndex : splinePositionInfo.SplineControlPointBIndex];
        ////component.SplineTraverser.targetSplineControlPoint = spline[isMovingForward ? splinePositionInfo.SplineControlPointBIndex : splinePositionInfo.SplineControlPointAIndex];

        ////component.SplineTraverser.bezierPointsToTraverse = isMovingForward
        ////    ? component.SplineTraverser.originSplineControlPoint.GetBezierPoints()
        ////    : component.SplineTraverser.targetSplineControlPoint.GetBezierPoints();

        ////if (component.SplineTraverser.bezierPointsToTraverse is null)
        ////{
        ////    return;
        ////}

        ////component.SplineTraverser.bezierPointIndex = splinePositionInfo.ClosestBezierPointIndex;
        ////component.SplineTraverser.originBezierPoint = component.SplineTraverser.bezierPointsToTraverse[component.SplineTraverser.bezierPointIndex];
        ////component.SplineTraverser.targetBezierPoint = component.SplineTraverser.bezierPointsToTraverse[component.SplineTraverser.bezierPointIndex];
        ////component.SplineTraverser.AttachedToSpline = true;
        ////component.SplineTraverser.startRotation = component.Entity.Transform.Rotation;
        ////SetNextTarget(component);
    }


    //private void UpdatePosition(SplineTraverserComponent component, float dt)
    //{
    //    var entityWorldPosition = component.Entity.Transform.WorldMatrix.TranslationVector;
    //    var velocity = component.SplineTraverser.targetBezierPoint.Position - entityWorldPosition;
    //    if (velocity.LengthSquared() > 0)
    //    {
    //        velocity.Normalize();
    //        velocity *= Math.Abs(component.SplineTraverser.Speed) * dt;
    //        component.Entity.Transform.Position += velocity;
    //    }

    //    component.Entity.Transform.UpdateWorldMatrix();
    //}

    //private void UpdateRotation(SplineTraverserComponent component, float dt)
    //{
    //    if (!component.SplineTraverser.IsRotating)
    //    {
    //        return;
    //    }

    //    var entityWorldPosition = component.Entity.Transform.WorldMatrix.TranslationVector;
    //    var originPosition = component.SplineTraverser.originBezierPoint.Position;
    //    var targetPosition = component.SplineTraverser.targetBezierPoint.Position;

    //    float totalDistance = Vector3.Distance(originPosition, targetPosition);
    //    float currentDistance = Vector3.Distance(originPosition, entityWorldPosition);

    //    // divide-by-zero
    //    if (totalDistance < 1e-6f)
    //        return;

    //    float rawRatio = currentDistance / totalDistance;
    //    float clampedRatio = Math.Clamp(rawRatio, 0, 1);
    //    float easedRatio = clampedRatio * clampedRatio * (3 - 2 * clampedRatio);


    //    float rotationStep = Math.Clamp(dt / totalDistance, 0, 1);
    //    easedRatio = Math.Clamp(easedRatio + rotationStep, 0, 1);

    //    var startRotation = Quaternion.Normalize(component.SplineTraverser.startRotation);
    //    var targetRotation = Quaternion.Normalize(component.SplineTraverser.targetBezierPoint.Rotation);

    //    component.Entity.Transform.Rotation = Quaternion.Slerp(startRotation, targetRotation, easedRatio);
    //}

    //private void SetNextTarget(SplineTraverserComponent component)
    //{
    //    var traverser = component.SplineTraverser;
    //    var controlPointsCount = component.SplineComponent.Spline.ControlPoints.Count;
    //    bool isMovingForward = traverser.Speed >= 0;
    //    var backwards = !isMovingForward;
    //    var indexIncrement = isMovingForward ? 1 : -1;

    //    // Is there a next/previous bezier point?
    //    if ((isMovingForward && traverser.bezierPointIndex + 1 < traverser.bezierPointsToTraverse.Length) || (backwards && traverser.bezierPointIndex - 1 >= 0))
    //    {
    //        traverser.originBezierPoint = traverser.bezierPointsToTraverse[traverser.bezierPointIndex];

    //        traverser.bezierPointIndex += indexIncrement;
    //        traverser.targetBezierPoint = traverser.bezierPointsToTraverse[traverser.bezierPointIndex];
    //        traverser.startRotation = component.Entity.Transform.Rotation;
    //    }
    //    else
    //    {
    //        traverser.RaiseSplineControlPointReached(traverser.targetSplineControlPoint);

    //        // Is there a next/previous Spline controlPoint?
    //        if (component.SplineComponent.Spline.IsClosedLoop || (isMovingForward && traverser.targetSplineControlPointIndex + 1 < controlPointsCount) || (backwards && traverser.targetSplineControlPointIndex - 1 == 0))
    //        {
    //            SetNextSplineControlPoint(component, controlPointsCount, isMovingForward, backwards, indexIncrement);
    //        }
    //        else
    //        {
    //            traverser.isMoving = false;
    //            traverser.targetSplineControlPointIndex += (indexIncrement * -1); //Inverse the increment
    //            traverser.targetSplineControlPoint = component.SplineComponent.Spline.ControlPoints[traverser.targetSplineControlPointIndex];
    //            traverser.RaiseSplineEndReached(traverser.targetSplineControlPoint);
    //        }
    //    }
    //}

    //private void SetNextSplineControlPoint(SplineTraverserComponent component, int controlPointsCount, bool isMovingForward, bool backwards, int indexIncrement)
    //{
    //    var traverser = component.SplineTraverser;
    //    traverser.originSplineControlPoint = traverser.targetSplineControlPoint;
    //    traverser.targetSplineControlPointIndex += indexIncrement;

    //    if ((isMovingForward && traverser.targetSplineControlPointIndex < controlPointsCount) || (backwards && traverser.targetSplineControlPointIndex >= 0))
    //    {
    //        traverser.targetSplineControlPoint = component.SplineComponent.Spline.ControlPoints[traverser.targetSplineControlPointIndex];
    //    }
    //    else if (component.SplineComponent.Spline.IsClosedLoop && ((isMovingForward && traverser.targetSplineControlPointIndex == controlPointsCount) || (backwards && traverser.targetSplineControlPointIndex < 0)))
    //    {
    //        traverser.RaiseSplineEndReached(traverser.targetSplineControlPoint);
    //        traverser.targetSplineControlPointIndex = isMovingForward ? 0 : controlPointsCount - 1;
    //        traverser.targetSplineControlPoint = component.SplineComponent.Spline.ControlPoints[traverser.targetSplineControlPointIndex];
    //    }

    //    traverser.bezierPointsToTraverse = isMovingForward ? traverser.originSplineControlPoint.GetBezierPoints() : traverser.targetSplineControlPoint.GetBezierPoints();
    //    traverser.bezierPointIndex = isMovingForward ? traverser.bezierPointIndex = 0 : traverser.bezierPointsToTraverse.Length - 1;

    //    SetNextTarget(component);
    //}
}
