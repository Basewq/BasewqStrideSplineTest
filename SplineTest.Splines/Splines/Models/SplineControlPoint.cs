// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using System.ComponentModel;

namespace Stride.Engine.Splines.Models;

[DataContract]
[Display(Expand = ExpandRule.Once)]
public struct SplineControlPoint
{
    /// <summary>
    /// Local position relative to the spline.
    /// </summary>
    public Vector3 Position;
    /// <summary>
    /// Tangent vector, in the spline's local space, relative to the control point, that determines the direction and curvature of the curve entering this control point.
    /// </summary>
    public Vector3 TangentIn;
    /// <summary>
    /// Tangent vector, in the spline's local space, relative to the control point, that determines the direction and curvature of the curve leaving this control point.
    /// </summary>
    public Vector3 TangentOut;

    /// <summary>
    /// Accumulated roll rotation offset (in degrees) along the forward vector.
    /// Roll is applied after <see cref="OverrideUpDirection"/>, if it is set.
    /// </summary>
    [Display("Roll (degrees)")]
    public AngleSingle Roll;

    /// <summary>
    /// Override up direction, in the spline's local space.
    /// The curve's forward/tangent vector takes priority over this direction, if not perpendicular to the tangent.
    /// </summary>
    [DefaultValue(typeof(Vector3), "X:0 Y:0 Z:0")]
    public Vector3 OverrideUpDirection;

    /// <summary>
    /// Scale relative to the control point's frame of reference, eg. Width, Height, Length for X, Y, Z, respectively.
    /// </summary>
    [DefaultValue(typeof(Vector3), "X:1 Y:1 Z:1")]
    public Vector3 Scale = Vector3.One;

    public SplineControlPointType Type = SplineControlPointType.Auto;

    /// <summary>
    /// <see cref="TangentIn"/> position relative to the spline.
    /// </summary>
    public readonly Vector3 TangentInPosition => Position + TangentIn;

    /// <summary>
    /// <see cref="TangentOut"/> position relative to the spline.
    /// </summary>
    public readonly Vector3 TangentOutPosition => Position + TangentOut;

    public SplineControlPoint()
    {
    }
}
