// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

namespace Stride.Engine.Splines.Models;

public static class SplineControlPointTypeExtensions
{
    public static bool IsTangentUserControllable(this SplineControlPointType type)
    {
        switch (type)
        {
            case SplineControlPointType.Mirrored:
            case SplineControlPointType.Aligned:
            case SplineControlPointType.Free:
                return true;
            default:
                return false;
        }
    }

    public static bool IsTangentVisible(this SplineControlPointType type)
    {
        switch (type)
        {
            case SplineControlPointType.Linear:
                return false;
            default:
                return true;
        }
    }
}
