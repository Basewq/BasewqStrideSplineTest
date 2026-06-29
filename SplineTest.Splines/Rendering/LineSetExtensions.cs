// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core.Mathematics;

namespace SplineTest.Rendering;

public static class LineSetExtensions
{
    public static int AddWorldLine(
        this LineSet lineSet,
        Vector3 start, Vector3 end,
        Color4? lineColor = null, float lineThicknessPx = 1, float emissiveScale = 0)
    {
        int segmentIndex = lineSet.Segments.Count;

        var lineSegmentColor = AddEmissiveScale(lineColor ?? Color4.White, emissiveScale);
        var lineSegment = new LineSegment
        {
            StartPosition = start,
            EndPosition = end,
            StartColor = lineSegmentColor,
            EndColor = lineSegmentColor,
            LineThicknessPx = lineThicknessPx,
        };
        lineSet.Segments.Add(lineSegment);

        return segmentIndex;
    }

    public static int AddViewScaledLengthLine(
        this LineSet lineSet,
        Vector3 start, Vector3 direction, float fixedLengthPx,
        Color4? lineColor = null, float lineThicknessPx = 1, float emissiveScale = 0)
    {
        int segmentIndex = lineSet.Segments.Count;

        var lineSegmentColor = AddEmissiveScale(lineColor ?? Color4.White, emissiveScale);
        var lineSegment = new LineSegment
        {
            LineMode = LineMode.ViewScaled,
            StartPosition = start,
            EndPosition = start + direction,
            StartColor = lineSegmentColor,
            EndColor = lineSegmentColor,
            LineThicknessPx = lineThicknessPx,
            FixedLengthPx = fixedLengthPx
        };
        lineSet.Segments.Add(lineSegment);

        return segmentIndex;
    }

    public static int AddFixedScreenLengthLine(
        this LineSet lineSet,
        Vector3 start, Vector3 direction, float fixedLengthPx,
        Color4? lineColor = null, float lineThicknessPx = 1, float emissiveScale = 0)
    {
        int segmentIndex = lineSet.Segments.Count;

        var lineSegmentColor = AddEmissiveScale(lineColor ?? Color4.White, emissiveScale);
        var lineSegment = new LineSegment
        {
            LineMode = LineMode.FixedScreenLength,
            StartPosition = start,
            EndPosition = start + direction,
            StartColor = lineSegmentColor,
            EndColor = lineSegmentColor,
            LineThicknessPx = lineThicknessPx,
            FixedLengthPx = fixedLengthPx
        };
        lineSet.Segments.Add(lineSegment);

        return segmentIndex;
    }

    public static Range AddCircle(
        this LineSet lineSet,
        Vector3 center, float radius,
        int segments = 32,
        Vector3? normal = null, Vector3? tangent = null,
        Color4? lineColor = null, float lineThicknessPx = 1, float emissiveScale = 0)
    {
        int startIndex = lineSet.Segments.Count;

        var lineSegmentColor = AddEmissiveScale(lineColor ?? Color4.White, emissiveScale);

        var normalVec = normal ?? Vector3.UnitY;
        var tangentVec = tangent ?? -Vector3.UnitZ;
        var bitangentVec = Vector3.Cross(normalVec, tangent ?? -Vector3.UnitZ);

        float arcLength = MathUtil.TwoPi / segments;
        var prevPoint = center + radius * tangentVec;
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * arcLength;

            float x = MathF.Cos(angle) * radius;
            float y = MathF.Sin(angle) * radius;

            var point = center + radius * (tangentVec * MathF.Cos(angle) + bitangentVec * MathF.Sin(angle));

            var lineSegment = new LineSegment
            {
                StartPosition = prevPoint,
                EndPosition = point,
                StartColor = lineSegmentColor,
                EndColor = lineSegmentColor,
                LineThicknessPx = lineThicknessPx,
            };
            lineSet.Segments.Add(lineSegment);

            prevPoint = point;
        }

        return new Range(startIndex, lineSet.Segments.Count);
    }

    private static Color4 AddEmissiveScale(Color4 color, float emissiveScale)
    {
        color.R += color.R * emissiveScale;
        color.G += color.G * emissiveScale;
        color.B += color.B * emissiveScale;
        return color;
    }
}
