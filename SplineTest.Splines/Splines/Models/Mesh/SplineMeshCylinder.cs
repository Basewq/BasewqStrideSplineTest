// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshCylinder")]
[Display("Cylinder")]
public class SplineMeshCylinder : SplineMesh
{
    /// <summary>
    /// The amount of sides.
    /// </summary>
    public int Sides = 16;

    /// <summary>
    /// The radius of the cylinder.
    /// </summary>
    public float Radius = 1.0f;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var splinePoints = new List<Vector3>();
        SplineExtensions.CollectSplineSamplePositionsByResolution(Spline, splinePoints);
        var splinePointsSpan = CollectionsMarshal.AsSpan(splinePoints);
        int splinePointCount = splinePointsSpan.Length;

        int ringVertexCount = Sides + 1;        // Duplicate start/end vertex for UV wrapping
        int vertexCount = splinePointCount * ringVertexCount;
        int indicesCount = (splinePointCount - 1) * Sides * 6;

        if (CloseEnds && !Spline.IsClosedLoop)
        {
            // Cap made like pie slices (x2 for start and end caps)
            int capVertCount = Sides + 1 + 1;       // +1 to duplicate start/end vertex for UV wrapping and another +1 for center vertex
            int capIndicesCount = Sides * 3;
            vertexCount += capVertCount * 2;
            indicesCount += capIndicesCount * 2;
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;

        for (int i = 0; i < splinePointCount - 1; i++)
        {
            var startPoint = splinePointsSpan[i];
            var targetPoint = splinePointsSpan[i + 1];
            var forward = Vector3.Normalize(targetPoint - startPoint);

            // Generate vertices around the spline at position 'startPoint'
            if (i == 0)
            {
                for (int side = 0; side <= Sides; side++)
                {
                    float angle = side * MathUtil.TwoPi / Sides;
                    float offsetX = MathF.Cos(angle) * Radius;
                    float offsetZ = MathF.Sin(angle) * Radius;

                    var perpendicular = new Vector3(-forward.Z, 0, forward.X);  // Perpendicular vector on the XZ plane
                    var sideVertexPosition = startPoint + perpendicular * offsetX + Vector3.UnitY * Scale.Y * offsetZ;
                    var normal = CalculateRadialNormal(sideVertexPosition, startPoint);

                    vertices[verticesIndex++] = CreateVertex(sideVertexPosition, normal, new Vector2(side / (float)Sides, 0));
                }
            }

            // Generate vertices around the spline at position 'targetPoint'
            splineDistance += Vector3.Distance(startPoint, targetPoint);
            float textureY = splineDistance / UvScale.Y;
            for (int side = 0; side <= Sides; side++)
            {
                float angle = side * MathUtil.TwoPi / Sides;
                float offsetX = MathF.Cos(angle) * Radius;
                float offsetZ = MathF.Sin(angle) * Radius;

                var perpendicular = new Vector3(-forward.Z, 0, forward.X);  // Perpendicular vector on the XZ plane
                var sideVertexPosition = targetPoint + perpendicular * offsetX + Vector3.UnitY * Scale.Y * offsetZ;
                var normal = CalculateRadialNormal(sideVertexPosition, targetPoint);

                vertices[verticesIndex++] = CreateVertex(sideVertexPosition, normal, new Vector2(side / (float)Sides, textureY));
            }
        }

        // Generating indices for each cylinder segment
        for (int i = 0; i < splinePointCount - 1; i++)
        {
            int currentRingStartIndex = i * ringVertexCount;
            int nextRingStartIndex = (i + 1) * ringVertexCount;

            for (int side = 0; side < Sides; side++)
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

        if (Spline.IsClosedLoop)
        {
            // Stitch the start/end positions to remove seams.
            int endStartIndex = vertices.Length - ringVertexCount;
            for (int i = 0; i < ringVertexCount; i++)
            {
                StitchVertexPositions(vertices, i, endStartIndex + i);
            }
        }

        // Close the cylinder ends
        if (CloseEnds && !Spline.IsClosedLoop)
        {
            CloseCylinderEnds(Sides, splinePointsSpan, vertices, indices, ref indicesIndex);
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

        // Generate vertices for the caps
        for (int side = 0; side <= sides; side++)
        {
            float angle = side * MathUtil.TwoPi / sides;
            float offsetX = MathF.Cos(angle) * Radius;
            float offsetY = MathF.Sin(angle) * Radius;

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
}
