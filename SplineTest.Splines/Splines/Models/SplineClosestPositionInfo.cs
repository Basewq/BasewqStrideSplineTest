// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

public readonly struct SplineClosestPositionInfo
{
    /// <summary>
    /// Local position on the spline.
    /// </summary>
    public readonly Vector3 Position { get; init; }

    public readonly int SplineNodeAIndex { get; init; }
    public readonly int SplineNodeBIndex { get; init; }
    /// <summary>
    /// T value in range [0...1] between <see cref="SplineNodeAIndex"/> and <see cref="SplineNodeBIndex"/>.
    /// </summary>
    public readonly float LocalT { get; init; }
}
