// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Engine.Splines.Models.Mesh;

[Flags]
public enum ShapeProfileFlipAxis : byte
{
    None = 0,
    X = 1 << 0,
    Y = 1 << 1,
    Z = 1 << 2,

    All = X | Y | Z,
}
