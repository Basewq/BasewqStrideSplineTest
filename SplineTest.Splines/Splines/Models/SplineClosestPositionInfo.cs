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

    public readonly int SplineControlPointAIndex { get; init; }
    public readonly int SplineControlPointBIndex { get; init; }
    /// <summary>
    /// T value in range [0...1] between <see cref="SplineControlPointAIndex"/> and <see cref="SplineControlPointBIndex"/>.
    /// </summary>
    public readonly float LocalT { get; init; }

    /// <summary>
    /// The distance value in range [0...<see cref="Spline.GetTotalDistance"/>] on the entire spline.
    /// </summary>
    public readonly float SplineDistance { get; init; }
}
