// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;

namespace Stride.Engine.Splines.Models;

[DataContract]
public enum SplineControlPointType
{
    /// <summary>
    /// Automatic tangents. Creates a smooth continuous curve at this <see cref="SplineControlPoint"/>.
    /// </summary>
    Auto,

    /// <summary>
    /// No tangents. Creates straight lines from this <see cref="SplineControlPoint"/> directly to its neighboring <see cref="SplineControlPoint"/>s.
    /// </summary>
    Linear,

    /// <summary>
    /// Tangents that are opposite direction to each other and always have the same length.
    /// </summary>
    Mirrored,

    /// <summary>
    /// Tangents that are opposite direction to each other but can have the different lengths.
    /// </summary>
    Aligned,

    /// <summary>
    /// Independent tangents.
    /// </summary>
    Free,
}
