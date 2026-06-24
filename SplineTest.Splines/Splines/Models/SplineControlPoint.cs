// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core;
using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

[DataContract]
[Display(Expand = ExpandRule.Once)]
public struct SplineControlPoint : IEquatable<SplineControlPoint>
{
    /// <summary>
    /// Local position relative to the spline.
    /// </summary>
    public Vector3 Position;
    /// <summary>
    /// The orientation of this control point relative to the spline.
    /// </summary>
    public Quaternion Rotation;
    /// <summary>
    /// Tangent vector, relative to the control point, that determines the direction and curvature of the curve entering this control point.
    /// </summary>
    public Vector3 TangentIn;
    /// <summary>
    /// Tangent vector, relative to the control point, that determines the direction and curvature of the curve leaving this control point.
    /// </summary>
    public Vector3 TangentOut;

    /// <summary>
    /// <see cref="TangentIn"/> position relative to the spline.
    /// </summary>
    public Vector3 TangentInPosition => Rotation * (Position + TangentIn);

    /// <summary>
    /// <see cref="TangentOut"/> position relative to the spline.
    /// </summary>
    public Vector3 TangentOutPosition => Rotation * (Position + TangentOut);

    public SplineControlPoint()
    {
        Rotation = Quaternion.Identity;
    }

    /// <inheritdoc />
    public readonly bool Equals(SplineControlPoint other)
    {
        return Position == other.Position
            && Rotation == other.Rotation
            && TangentIn == other.TangentIn
            && TangentOut == other.TangentOut;
    }

    /// <inheritdoc />
    public readonly override bool Equals(object obj) => obj is SplineControlPoint other && Equals(other);

    /// <inheritdoc />
    public static bool operator ==(SplineControlPoint left, SplineControlPoint right) => Equals(left, right);

    /// <inheritdoc />
    public static bool operator !=(SplineControlPoint left, SplineControlPoint right) => !Equals(left, right);

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Position, Rotation, TangentIn, TangentOut);
    }
}
