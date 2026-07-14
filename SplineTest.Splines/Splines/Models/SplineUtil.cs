// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

/// <summary>
/// Common utility methods for spline operations.
/// </summary>
public static class SplineUtil
{
    public const float DefaultAutoTangentStrength = 1 / 3f;

    /// <param name="tangentInPosition">>Position is relative to <paramref name="currentPosition"/>.</param>
    /// <param name="tangentOutPosition">Position is relative to <paramref name="currentPosition"/>.</param>
    public static void CalculateAutoTangents(
        in Vector3 currentPosition, in Vector3? previousPosition, in Vector3? nextPosition, float strength,
        out Vector3 tangentInPosition, out Vector3 tangentOutPosition)
    {
        if (previousPosition.HasValue && nextPosition.HasValue)
        {
            var prevPointPos = previousPosition.Value;
            var nextPointPos = nextPosition.Value;
            var tangentOutDir = Vector3.Normalize(nextPointPos - prevPointPos);

            float currentToPrevDist = Vector3.Distance(currentPosition, prevPointPos);
            float currentToNextDist = Vector3.Distance(currentPosition, nextPointPos);
            tangentOutPosition = tangentOutDir * currentToNextDist * strength;
            tangentInPosition = -tangentOutDir * currentToPrevDist * strength;
        }
        else if (previousPosition.HasValue)
        {
            var prevPointPos = previousPosition.Value;
            var tangentInDir = Vector3.Normalize(prevPointPos - currentPosition);

            float currentToPrevDist = Vector3.Distance(currentPosition, prevPointPos);
            tangentInPosition = tangentInDir * currentToPrevDist * strength;
            tangentOutPosition = -tangentInPosition;      // Just mirror it
        }
        else if (nextPosition.HasValue)
        {
            var nextPointPos = nextPosition.Value;
            var tangentOutDir = Vector3.Normalize(nextPointPos - currentPosition);

            float currentToNextDist = Vector3.Distance(currentPosition, nextPointPos);
            tangentOutPosition = tangentOutDir * currentToNextDist * strength;
            tangentInPosition = -tangentOutPosition;      // Just mirror it
        }
        else
        {
            // No tangents
            tangentOutPosition = Vector3.Zero;
            tangentInPosition = Vector3.Zero;
        }
    }

    public static Vector3 CalculateLinearHandle(
        in Vector3 currentPosition, in Vector3? nextPosition)
    {
        if (nextPosition.HasValue)
        {
            var nextPointPos = nextPosition.Value;
            var currentToNextVec = nextPointPos - currentPosition;

            var tangentOutPosition = currentToNextVec / 3f;
            return tangentOutPosition;
        }
        else
        {
            // No tangent
            return Vector3.Zero;
        }
    }

    /// <param name="handlePosition">Position is relative to the control point.</param>
    /// <param name="oppositeHandlePosition">Position is relative to the control point.</param>
    public static Vector3 CalculateAlignedHandle(in Vector3 handlePosition, in Vector3 oppositeHandlePosition)
    {
        float currentHandleLength = handlePosition.Length();
        if (!MathUtil.IsZero(currentHandleLength))
        {
            // Mirror the other handle, but retain the original tangent's length
            var currentHandleDir = handlePosition / currentHandleLength;
            float oppositeHandleLength = oppositeHandlePosition.Length();
            var newOppositeHandlePosition = -currentHandleDir * oppositeHandleLength;
            return newOppositeHandlePosition;
        }
        else
        {
            return oppositeHandlePosition;
        }
    }

    public static ClosetPointLineSegmentResult GetClosestPointOnLineSegment(in Vector3 point, in Vector3 linePoint0, in Vector3 linePoint1)
    {
        // Vector projection of [point to linePoint0] onto [linePoint1 to linePoint0]
        var lineVector = linePoint1 - linePoint0;
        float lineLengthSqrd = lineVector.LengthSquared();
        if (MathUtil.IsZero(lineLengthSqrd))
        {
            return new ClosetPointLineSegmentResult
            {
                Point = linePoint0,
                T = 0,
            };
        }

        var pointVector = point - linePoint0;
        float t = Vector3.Dot(pointVector, lineVector) / lineLengthSqrd;
        t = Math.Clamp(t, min: 0, max: 1);

        var closestPointOnLine = linePoint0 + (t * lineVector);
        return new ClosetPointLineSegmentResult
        {
            Point = closestPointOnLine,
            T = t,
        };
    }
}

public readonly record struct ClosetPointLineSegmentResult
{
    /// <summary>
    /// The projected point on the line.
    /// </summary>
    public Vector3 Point { get; init; }
    /// <summary>
    /// Value in range [0...1] along the line, where the projected point is at.
    /// </summary>
    public float T { get; init; }

    public void Deconstruct(out Vector3 point, out float t)
    {
        point = Point;
        t = T;
    }
}
