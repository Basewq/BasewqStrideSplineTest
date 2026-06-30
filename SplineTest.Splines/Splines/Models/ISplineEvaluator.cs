// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public interface ISplineEvaluator
{
    Spline Spline { get; }

    void RegisterSpline(Spline spline);
    void UnregisterSpline();

    /// <summary>
    /// Returns the total distance over the entire spline.
    /// </summary>
    float GetTotalDistance();

    float GetTFromDistance(float distance);
    float GetDistanceFromT(float splineT);

    SplineSample Evaluate(float splineT);
    SplineSample EvaluateFromDistance(float distance);

    Vector3 EvaluatePosition(float splineT);
    Vector3 EvaluateTangent(float splineT);
    Quaternion EvaluateRotation(float splineT);

    /// <summary>
    /// Returns the position on the spline curve closest to <paramref name="position"/>.
    /// </summary>
    /// <param name="position">Position in the spline's local space.</param>
    SplineClosestPositionInfo FindClosestPoint(Vector3 position);

    BoundingBox CalculateBoundingBox();
}
