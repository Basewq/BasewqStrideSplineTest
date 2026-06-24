// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public struct BezierSegment
{
    /// <summary>
    /// The first control point.
    /// </summary>
    public Vector3 P0;
    /// <summary>
    /// The second control point.
    /// </summary>
    public Vector3 P1;

    /// <summary>
    /// The third control point.
    /// </summary>
    public Vector3 P2;
    /// <summary>
    /// The fourth control point.
    /// </summary>
    public Vector3 P3;

    public readonly Vector3 StartPosition => P0;
    public readonly Vector3 EndPosition => P3;

    public BezierSegment(Vector3 p0, Vector3 p3)
        : this(p0, p0, p3, p3)
    { }

    public BezierSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
    }

    public BezierSegment(in SplineControlPoint controlPoint1, in SplineControlPoint controlPoint2)
    {
        P0 = controlPoint1.Position;
        P1 = controlPoint1.TangentOutPosition;
        P2 = controlPoint2.TangentInPosition;
        P3 = controlPoint2.Position;
    }

    public readonly Vector3 GetPosition(float t)
    {
        float tPower2 = t * t;
        float tPower3 = t * t * t;
        float oneMinusT = 1f - t;
        float oneMinusTPower2 = oneMinusT * oneMinusT;
        float oneMinusTPower3 = oneMinusT * oneMinusT * oneMinusT;
        var result = (oneMinusTPower3 * P0)
            + (3f * oneMinusTPower2 * t * P1)
            + (3f * oneMinusT * tPower2 * P2)
            + (tPower3 * P3);
        return result;
    }

    public readonly void SamplePositions(List<Vector3> splinePositionToTraverse)
    {
        // TODO add additional sampling thresholds (eg. distance, angle change)
        const int SampleSizePerCurve = 10;
        float dt = 1f / SampleSizePerCurve;

        // First position is always just the initial value of the segment
        splinePositionToTraverse.Add(P0);

        for (int i = 1; i < SampleSizePerCurve; i++)
        {
            var currentT = dt * i;
            var currentPos = GetPosition(currentT);
            splinePositionToTraverse.Add(currentPos);
        }
    }
}
