// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using Stride.Core;
using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

[DataContract]
[Display(Expand = ExpandRule.Once)]
public struct SplineNode : IEquatable<SplineNode>
{
    /// <summary>
    /// Local position relative to the spline.
    /// </summary>
    public Vector3 Position;
    /// <summary>
    /// The orientation of this node relative to the spline.
    /// </summary>
    [DataMemberIgnore, Obsolete("Unused?")]
    public Quaternion Rotation;
    /// <summary>
    /// Local position of the tangent relative to the spline that determines the direction of the segment entering this node.
    /// </summary>
    public Vector3 TangentInPosition;
    /// <summary>
    /// Local position of the tangent relative to the spline that determines the direction of the segment leaving this node.
    /// </summary>
    public Vector3 TangentOutPosition;

    /// <inheritdoc />
    public readonly bool Equals(SplineNode other)
    {
        return Position == other.Position
            && Rotation == other.Rotation
            && TangentInPosition == other.TangentInPosition
            && TangentOutPosition == other.TangentOutPosition;
    }

    /// <inheritdoc />
    public readonly override bool Equals(object obj) => obj is SplineNode other && Equals(other);

    /// <inheritdoc />
    public static bool operator ==(SplineNode left, SplineNode right) => Equals(left, right);

    /// <inheritdoc />
    public static bool operator !=(SplineNode left, SplineNode right) => !Equals(left, right);

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Position, Rotation, TangentInPosition, TangentOutPosition);
    }
}
