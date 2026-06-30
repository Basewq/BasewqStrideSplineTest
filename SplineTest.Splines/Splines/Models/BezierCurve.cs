// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public struct BezierCurve
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

    public BezierCurve(Vector3 p0, Vector3 p3)
        : this(p0, p0, p3, p3)
    { }

    public BezierCurve(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
    }

    public BezierCurve(in SplineControlPoint controlPoint1, in SplineControlPoint controlPoint2)
    {
        P0 = controlPoint1.Position;
        P1 = controlPoint1.TangentOutPosition;
        P2 = controlPoint2.TangentInPosition;
        P3 = controlPoint2.Position;
    }

    public readonly Vector3 GetPosition(float t)
    {
        float tt = t * t;
        float ttt = t * t * t;
        float u = 1f - t;
        float uu = u * u;
        float uuu = u * u * u;
        var position = (uuu * P0)
            + (3f * uu * t * P1)
            + (3f * u * tt * P2)
            + (ttt * P3);
        return position;
    }

    public readonly Vector3 GetTangent(float t)
    {
        float tt = t * t;
        float u = 1f - t;
        float uu = u * u;
        var tangent = (3f * uu * (P1 - P0))
            + (6f * u * t * (P2 - P1))
            + (3f * tt * (P3 - P2));
        tangent.Normalize();
        return tangent;
    }
}
