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
    /// Local orientation relative to the spline.
    /// </summary>
    public Quaternion Orientation;
    /// <summary>
    /// Normalized tangent vector relative to the spline.
    /// </summary>
    public Vector3 Tangent;

    public float SplineT;

    public SplineSample(Vector3 position, Quaternion orientation, Vector3 tangent, float splineT)
    {
        Position = position;
        Orientation = orientation;
        Tangent = tangent;
        SplineT = splineT;
    }

    public readonly bool Equals(SplineSample other)
    {
        bool isEqual = Position.Equals(other.Position)
            && Orientation.Equals(other.Orientation)
            && Tangent.Equals(other.Tangent)
            && SplineT.Equals(other.SplineT);
        return isEqual;
    }
}
