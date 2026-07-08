// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using System;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshTube")]
[Display("Tube")]
public class SplineMeshTube : SplineMesh
{
    /// <summary>
    /// The amount of sides
    /// </summary>
    public int Sides = 16;

    /// <summary>
    /// The radius of the cylinder or tube: x for inner and y for outer
    /// </summary>
    public Vector2 Radius = new Vector2(0.1f, 1.0f);

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var splinePoints = new List<Vector3>();
        SplineExtensions.CollectSplineSamplePoints(Spline, splinePoints);
        var splinePointsSpan = CollectionsMarshal.AsSpan(splinePoints);
        int splinePointCount = splinePointsSpan.Length;

        bool isHollow = Radius.X > 0;
        int ringVertexCount = Sides + 1;        // +1 to duplicate start/end vertex for UV wrapping
        int vertexCount = splinePointCount * ringVertexCount;
        int indicesCount = (splinePointCount - 1) * Sides * 6;
        if (isHollow)
        {
            // Effectively two cylinders
            ringVertexCount *= 2;
            vertexCount *= 2;
            indicesCount *= 2;
        }

        if (CloseEnds && !Spline.IsClosedLoop)
        {
            if (isHollow)
            {
                // Cap made with quads (x2 for start and end caps)
                int capVertCount = (Sides * 2) + 2;     // +2 to duplicate start/end vertex for UV wrapping
                int capIndicesCount = Sides * 6;
                vertexCount += capVertCount * 2;
                indicesCount += capIndicesCount * 2;
            }
            else
            {
                // Cap made like pie slices (x2 for start and end caps)
                int capVertCount = Sides + 1 + 1;       // +1 to duplicate start/end vertex for UV wrapping and another +1 for center vertex
                int capIndicesCount = Sides * 3;
                vertexCount += capVertCount * 2;
                indicesCount += capIndicesCount * 2;
            }
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;

        float innerRadius = Radius.X;
        float outerRadius = Radius.Y;

        for (int i = 0; i < splinePointCount - 1; i++)
        {
            var startPoint = splinePointsSpan[i];
            var targetPoint = splinePointsSpan[i + 1];
            var forward = Vector3.Normalize(targetPoint - startPoint);
            var perpendicular = new Vector3(-forward.Z, 0, forward.X);  // Perpendicular vector on the XZ plane

            // Generate vertices around the spline at position 'startPoint'
            if (i == 0)
            {
                for (int side = 0; side <= Sides; side++)
                {
                    float angle = side * MathUtil.TwoPi / Sides;
                    float cosAngle = MathF.Cos(angle);
                    float sinAngle = MathF.Sin(angle);

                    if (isHollow)
                    {
                        // Outer vertices
                        var outerVertexPosition = startPoint + perpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
                        var outerNormal = CalculateRadialNormal(outerVertexPosition, startPoint);
                        vertices[verticesIndex++] = CreateVertex(outerVertexPosition, outerNormal, new Vector2(side / (float)Sides, 0));

                        // Inner vertices
                        var innerVertexPosition = startPoint + perpendicular * cosAngle * innerRadius + Vector3.UnitY * Scale.Y * sinAngle * innerRadius;
                        var innerNormal = -CalculateRadialNormal(innerVertexPosition, startPoint);
                        vertices[verticesIndex++] = CreateVertex(innerVertexPosition, innerNormal, new Vector2(1 - side / (float)Sides, 0));
                    }
                    else
                    {
                        // Single radius (solid cylinder)
                        var sideVertexPosition = startPoint + perpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
                        var normal = CalculateRadialNormal(sideVertexPosition, startPoint);
                        vertices[verticesIndex++] = CreateVertex(sideVertexPosition, normal, new Vector2(side / (float)Sides, 0));
                    }
                }
            }

            // Generate vertices around the spline at position 'targetPoint'
            splineDistance += Vector3.Distance(startPoint, targetPoint);
            float textureY = splineDistance / UvScale.Y;
            for (int side = 0; side <= Sides; side++)
            {
                float angle = side * MathUtil.TwoPi / Sides;
                float cosAngle = MathF.Cos(angle);
                float sinAngle = MathF.Sin(angle);

                if (isHollow)
                {
                    // Outer vertices
                    var outerVertexPosition = targetPoint + perpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
                    var outerNormal = CalculateRadialNormal(outerVertexPosition, targetPoint);
                    vertices[verticesIndex++] = CreateVertex(outerVertexPosition, outerNormal, new Vector2(side / (float)Sides, textureY));

                    // Inner vertices
                    var innerVertexPosition = targetPoint + perpendicular * cosAngle * innerRadius + Vector3.UnitY * Scale.Y * sinAngle * innerRadius;
                    var innerNormal = -CalculateRadialNormal(innerVertexPosition, targetPoint);
                    vertices[verticesIndex++] = CreateVertex(innerVertexPosition, innerNormal, new Vector2(1 - side / (float)Sides, textureY));
                }
                else
                {
                    // Single radius (solid cylinder)
                    var sideVertexPosition = targetPoint + perpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
                    var normal = CalculateRadialNormal(sideVertexPosition, targetPoint);
                    vertices[verticesIndex++] = CreateVertex(sideVertexPosition, normal, new Vector2(side / (float)Sides, textureY));
                }
            }
        }

        // Generating indices for each cylinder segment
        for (int i = 0; i < splinePointCount - 1; i++)
        {
            int currentRingStartIndex = i * ringVertexCount;
            int nextRingStartIndex = (i + 1) * ringVertexCount;

            for (int side = 0; side < Sides; side++)
            {
                if (isHollow)
                {
                    int currentRingVert0Outer = currentRingStartIndex + (side * 2);
                    int currentRingVert1Outer = currentRingVert0Outer + 2;
                    int nextRingVert0Outer = nextRingStartIndex + (side * 2);
                    int nextRingVert1Outer = nextRingVert0Outer + 2;

                    int currentRingVert0Inner = currentRingVert0Outer + 1;
                    int currentRingVert1Inner = currentRingVert1Outer + 1;
                    int nextRingVert0Inner = nextRingVert0Outer + 1;
                    int nextRingVert1Inner = nextRingVert1Outer + 1;

                    // Outer side
                    indices[indicesIndex++] = currentRingVert0Outer;
                    indices[indicesIndex++] = nextRingVert1Outer;
                    indices[indicesIndex++] = nextRingVert0Outer;

                    indices[indicesIndex++] = currentRingVert0Outer;
                    indices[indicesIndex++] = currentRingVert1Outer;
                    indices[indicesIndex++] = nextRingVert1Outer;

                    // Inner side
                    indices[indicesIndex++] = currentRingVert0Inner;
                    indices[indicesIndex++] = nextRingVert0Inner;
                    indices[indicesIndex++] = nextRingVert1Inner;

                    indices[indicesIndex++] = currentRingVert0Inner;
                    indices[indicesIndex++] = nextRingVert1Inner;
                    indices[indicesIndex++] = currentRingVert1Inner;
                }
                else
                {
                    int currentRingVert0 = currentRingStartIndex + side;
                    int currentRingVert1 = currentRingVert0 + 1;
                    int nextRingVert0 = nextRingStartIndex + side;
                    int nextRingVert1 = nextRingVert0 + 1;

                    indices[indicesIndex++] = currentRingVert0;
                    indices[indicesIndex++] = nextRingVert1;
                    indices[indicesIndex++] = nextRingVert0;

                    indices[indicesIndex++] = currentRingVert0;
                    indices[indicesIndex++] = currentRingVert1;
                    indices[indicesIndex++] = nextRingVert1;
                }
            }
        }

        if (Spline.IsClosedLoop)
        {
            // Stitch the start/end positions to remove seams.
            int endStartIndex = vertices.Length - ringVertexCount;
            for (int i = 0; i < ringVertexCount; i++)
            {
                StitchVertexPositions(vertices, i, endStartIndex + i);
            }
        }

        // Close the cylinder/tube ends
        if (CloseEnds && !Spline.IsClosedLoop)
        {
            if (isHollow)
            {
                CloseTubeEnds(Sides, splinePointsSpan, vertices, indices, ref indicesIndex);
            }
            else
            {
                CloseCylinderEnds(Sides, splinePointsSpan, vertices, indices, ref indicesIndex);
            }
        }

        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: true);
    }

    private void CloseCylinderEnds(
        int sides, Span<Vector3> splinePoints, VertexPositionNormalTexture[] vertices, int[] indices, ref int indicesIndex)
    {
        int ringVertexCount = sides + 1;        // Duplicate start/end vertex for UV wrapping
        int startCapVertexOffset = vertices.Length - (2 * (ringVertexCount + 1));       // +1 for the center vertex
        int endCapVertexOffset = vertices.Length - (ringVertexCount + 1);

        var startCenter = splinePoints[0];
        var startCenterNext = splinePoints[1];
        var endCenter = splinePoints[^1];
        var endCenterPrev = splinePoints[^2];

        var startNormal = Vector3.Normalize(startCenter - startCenterNext);         // Face 'backwards' (ie. reversed tangent direction)
        var startPerpendicular = new Vector3(-startNormal.Z, 0, startNormal.X);     // Perpendicular vector on the XZ plane

        var endNormal = Vector3.Normalize(endCenter - endCenterPrev);               // Face 'forward' (ie. along tangent direction)
        var endPerpendicular = new Vector3(-endNormal.Z, 0, endNormal.X);           // Perpendicular vector on the XZ plane

        float outerRadius = Radius.Y;

        // Generate vertices for the caps
        for (int side = 0; side <= sides; side++)
        {
            float angle = side * MathUtil.TwoPi / sides;
            float offsetX = MathF.Cos(angle) * outerRadius;
            float offsetY = MathF.Sin(angle) * outerRadius;

            float texCoordCircleX = -MathF.Cos(angle);
            float texCoordCircleY = -MathF.Sin(angle);
            var texCoord = RemapCircleToSquare(texCoordCircleX, texCoordCircleY);

            // Start cap vertices
            var startCapPosition = startCenter + startPerpendicular * offsetX + Vector3.UnitY * Scale.Y * offsetY;
            vertices[startCapVertexOffset + side] = new VertexPositionNormalTexture(startCapPosition, startNormal, new Vector2(texCoord.X, texCoord.Y));

            // End cap vertices
            var endCapPosition = endCenter + endPerpendicular * offsetX + Vector3.UnitY * Scale.Y * offsetY;
            vertices[endCapVertexOffset + side] = new VertexPositionNormalTexture(endCapPosition, endNormal, new Vector2(texCoord.X, texCoord.Y));
        }

        // Generate indices for the start cap
        int startCenterVertexIndex = startCapVertexOffset + ringVertexCount;
        vertices[startCenterVertexIndex] = new VertexPositionNormalTexture(startCenter, startNormal, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = side + 1;

            indices[indicesIndex++] = startCenterVertexIndex; // Center vertex of the start cap
            indices[indicesIndex++] = startCapVertexOffset + side;
            indices[indicesIndex++] = startCapVertexOffset + nextSide;
        }

        // Generate indices for the end cap
        int endCenterVertexIndex = endCapVertexOffset + ringVertexCount;
        vertices[endCenterVertexIndex] = new VertexPositionNormalTexture(endCenter, endNormal, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = side + 1;

            indices[indicesIndex++] = endCenterVertexIndex; // Center vertex of the end cap
            indices[indicesIndex++] = endCapVertexOffset + side;
            indices[indicesIndex++] = endCapVertexOffset + nextSide;
        }
    }

    private static Vector2 RemapCircleToSquare(float x, float y)
    {
        float length = MathF.Sqrt(x * x + y * y);

        // Move the circle's point onto the square
        float scale = MathF.Max(MathF.Abs(x), MathF.Abs(y)) / length;
        float sx = x / scale;
        float sy = y / scale;

        // Remap [-1, 1] to [0, 1]
        return new Vector2(sx * 0.5f + 0.5f, sy * 0.5f + 0.5f);
    }

    private void CloseTubeEnds(int sides, Span<Vector3> splinePoints, VertexPositionNormalTexture[] vertices, int[] indices, ref int indicesIndex)
    {
        int ringVertexCount = sides + 1;        // Duplicate start/end vertex for UV wrapping
        int startOuterCapVertexOffset = vertices.Length - 4 * ringVertexCount;
        int startInnerCapVertexOffset = vertices.Length - 3 * ringVertexCount;
        int endOuterCapVertexOffset = vertices.Length - 2 * ringVertexCount;
        int endInnerCapVertexOffset = vertices.Length - ringVertexCount;

        var startCenter = splinePoints[0];
        var startCenterNext = splinePoints[1];
        var endCenter = splinePoints[^1];
        var endCenterPrev = splinePoints[^2];

        var startNormal = Vector3.Normalize(startCenter - startCenterNext);         // Face 'backwards' (ie. reversed tangent direction)
        var startPerpendicular = new Vector3(-startNormal.Z, 0, startNormal.X);     // Perpendicular vector on the XZ plane

        var endNormal = Vector3.Normalize(endCenter - endCenterPrev);               // Face 'forward' (ie. along tangent direction)
        var endPerpendicular = new Vector3(-endNormal.Z, 0, endNormal.X);           // Perpendicular vector on the XZ plane

        float innerRadius = Radius.X;
        float outerRadius = Radius.Y;

        // Generate vertices for the caps
        for (int side = 0; side <= sides; side++)
        {
            float angle = side * MathUtil.TwoPi / sides;
            float cosAngle = MathF.Cos(angle);
            float sinAngle = MathF.Sin(angle);

            // Start cap vertices
            var startOuterCapPosition = startCenter + startPerpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
            var startInnerCapPosition = startCenter + startPerpendicular * cosAngle * innerRadius + Vector3.UnitY * Scale.Y * sinAngle * innerRadius;

            vertices[startOuterCapVertexOffset + side] = new VertexPositionNormalTexture(startOuterCapPosition, startNormal, new Vector2(side / (float)sides, 0));
            vertices[startInnerCapVertexOffset + side] = new VertexPositionNormalTexture(startInnerCapPosition, startNormal, new Vector2(side / (float)sides, 1));

            // End cap vertices
            var endOuterCapPosition = endCenter + endPerpendicular * cosAngle * outerRadius + Vector3.UnitY * Scale.Y * sinAngle * outerRadius;
            var endInnerCapPosition = endCenter + endPerpendicular * cosAngle * innerRadius + Vector3.UnitY * Scale.Y * sinAngle * innerRadius;

            vertices[endOuterCapVertexOffset + side] = new VertexPositionNormalTexture(endOuterCapPosition, endNormal, new Vector2(side / (float)sides, 0));
            vertices[endInnerCapVertexOffset + side] = new VertexPositionNormalTexture(endInnerCapPosition, endNormal, new Vector2(side / (float)sides, 1));
        }

        // Generate indices for the start cap
        for (int side = 0; side < sides; side++)
        {
            int currentRingVert0 = startOuterCapVertexOffset + side;
            int currentRingVert1 = currentRingVert0 + 1;
            int nextRingVert0 = startInnerCapVertexOffset + side;
            int nextRingVert1 = nextRingVert0 + 1;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = nextRingVert1;
            indices[indicesIndex++] = nextRingVert0;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = currentRingVert1;
            indices[indicesIndex++] = nextRingVert1;
        }

        // Generate indices for the end cap
        for (int side = 0; side < sides; side++)
        {
            int currentRingVert0 = endOuterCapVertexOffset + side;
            int currentRingVert1 = currentRingVert0 + 1;
            int nextRingVert0 = endInnerCapVertexOffset + side;
            int nextRingVert1 = nextRingVert0 + 1;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = nextRingVert1;
            indices[indicesIndex++] = nextRingVert0;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = currentRingVert1;
            indices[indicesIndex++] = nextRingVert1;
        }
    }
}
