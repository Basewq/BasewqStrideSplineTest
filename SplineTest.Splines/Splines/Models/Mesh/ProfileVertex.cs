// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models.Mesh;

public struct ProfileVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    /// <summary>
    /// [0...1] value for how far along the profile this vertex is.
    /// </summary>
    public float ProfileT;
}
