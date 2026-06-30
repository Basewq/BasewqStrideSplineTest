// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public struct SplineSample : IEquatable<SplineSample>
{
    /// <summary>
    /// Local position relative to the spline.
    /// </summary>
    public Vector3 Position;
    /// <summary>
    /// Local rotation relative to the spline.
    /// </summary>
    public Quaternion Rotation;
    /// <summary>
    /// Normalized tangent vector relative to the spline.
    /// </summary>
    public Vector3 Tangent;

    public SplineSample(Vector3 position, Quaternion rotation, Vector3 tangent)
    {
        Position = position;
        Rotation = rotation;
        Tangent = tangent;
    }

    public bool Equals(SplineSample other)
    {
        bool isEqual = Position.Equals(other.Position)
            && Rotation.Equals(other.Rotation)
            && Tangent.Equals(other.Tangent);
        return isEqual;
    }
}
