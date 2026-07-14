// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;

namespace Stride.Engine.Splines.Models;

[DataContract]
public struct SplineSamplingSettings
{
    /// <summary>
    /// The maximum distance difference between a line segment's midpoint and spline's evaluated position to be sampled.
    /// </summary>
    public float MaximumPositionError { get; set; } = 0.05f;

    /// <summary>
    /// The maximum angle difference between a line segment and line segment start's position to spline's evaluated position to be sampled.
    /// </summary>
    public AngleSingle MaximumAngleError { get; set; } = new AngleSingle(3, AngleType.Degree);

    /// <summary>
    /// The maximum distance allowed before the next sample point must be sampled at from previous sample point.
    /// </summary>
    public float MaximumSegmentLength { get; set; } = 0.5f;

    /// <summary>
    /// The smallest distance allowed between two sample points, otherwise it will discarded.
    /// </summary>
    public float MinimumSegmentLength { get; set; } = 0.01f;

    /// <summary>
    /// The maximum number of subdivisions allowed between two sample points, otherwise it will discarded.
    /// </summary>
    public int MaximumSubdivisionDepth { get; set; } = 8;

    public SplineSamplingSettings()
    {
    }
}
