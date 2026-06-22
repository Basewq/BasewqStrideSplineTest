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

    public BezierSegment(in SplineNode node1, in SplineNode node2)
    {
        P0 = node1.Position;
        P1 = node1.TangentOutPosition;
        P2 = node2.TangentInPosition;
        P3 = node2.Position;
    }

    public readonly Vector3 GetPosition(float t)
    {
        var tPower2 = t * t;
        var tPower3 = t * t * t;
        var oneMinusT = 1 - t;
        var oneMinusTPower2 = oneMinusT * oneMinusT;
        var oneMinusTPower3 = oneMinusT * oneMinusT * oneMinusT;
        var x = (oneMinusTPower3 * P0.X) + (3 * oneMinusTPower2 * t * P1.X) + (3 * oneMinusT * tPower2 * P2.X + tPower3 * P3.X);
        var y = (oneMinusTPower3 * P0.Y) + (3 * oneMinusTPower2 * t * P1.Y) + (3 * oneMinusT * tPower2 * P2.Y + tPower3 * P3.Y);
        var z = (oneMinusTPower3 * P0.Z) + (3 * oneMinusTPower2 * t * P1.Z) + (3 * oneMinusT * tPower2 * P2.Z + tPower3 * P3.Z);
        return new Vector3(x, y, z);
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
