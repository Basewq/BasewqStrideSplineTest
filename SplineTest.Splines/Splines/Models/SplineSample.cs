// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public struct SplineSample
{
    /// <summary>
    /// Local position relative to the spline.
    /// </summary>
    public Vector3 Position;
    /// <summary>
    /// Local rotation relative to the spline.
    /// </summary>
    public Quaternion Rotation;

    public SplineSample(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }
}
