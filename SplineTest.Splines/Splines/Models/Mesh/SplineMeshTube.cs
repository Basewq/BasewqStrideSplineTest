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
        int vertexCount = isHollow ? splinePointCount * Sides * 2 : splinePointCount * Sides;
        int indicesCount = isHollow ? (splinePointCount - 1) * Sides * 12 : (splinePointCount - 1) * Sides * 6;

        if (Spline.IsClosedLoop)
        {
            indicesCount += isHollow ? Sides * 12 : Sides * 6;
        }
        else if (CloseEnds && !Spline.IsClosedLoop)
        {
            vertexCount += isHollow ? 4 * Sides : 2 * Sides;    // Additional vertices for the start and end caps
            indicesCount += isHollow ? 12 * Sides : 6 * Sides;  // Additional triangles for the caps
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;

        for (int i = 0; i < splinePointCount; i++)
        {
            var point = splinePointsSpan[i];
            var nextPoint = splinePointsSpan[(i + 1) % splinePointCount];
            var direction = (nextPoint - point);
            direction.Normalize();

            float textureY = splineDistance / UvScale.Y;

            // Generate vertices around the spline point
            for (int side = 0; side < Sides; side++)
            {
                float angle = side * MathUtil.TwoPi / Sides;
                float cosAngle = MathF.Cos(angle);
                float sinAngle = MathF.Sin(angle);

                var perpendicular = new Vector3(-direction.Z, 0, direction.X); // Perpendicular vector on the XZ plane

                if (isHollow)
                {
                    // Outer vertices
                    var outerVertexPosition = point + perpendicular * cosAngle * Radius.Y + Vector3.UnitY * Scale.Y * sinAngle * Radius.Y;
                    var outerNormal = CalculateRadialNormal(outerVertexPosition, point);
                    vertices[verticesIndex++] = CreateVertex(outerVertexPosition, outerNormal, new Vector2((float)side / Sides, textureY));

                    // Inner vertices
                    var innerVertexPosition = point + perpendicular * cosAngle * Radius.X + Vector3.UnitY * Scale.Y * sinAngle * Radius.X;
                    var innerNormal = CalculateRadialNormal(innerVertexPosition, point);
                    vertices[verticesIndex++] = CreateVertex(innerVertexPosition, -innerNormal, new Vector2((float)side / Sides, textureY));
                }
                else
                {
                    // Single radius (solid cylinder)
                    var sideVertexPosition = point + perpendicular * cosAngle * Radius.Y + Vector3.UnitY * Scale.Y * sinAngle * Radius.Y;
                    var normal = CalculateRadialNormal(sideVertexPosition, point);
                    vertices[verticesIndex++] = CreateVertex(sideVertexPosition, normal, new Vector2((float)side / Sides, textureY));
                }
            }

            if (i < splinePointCount - 1)
            {
                splineDistance += Vector3.Distance(point, splinePointsSpan[i + 1]);
            }
        }

        // Generating indices for each cylinder segment
        for (int i = 0; i < splinePointCount - 1; i++)
        {
            for (int side = 0; side < Sides; side++)
            {
                if (isHollow)
                {
                    int currentOuter = i * Sides * 2 + side * 2;
                    int nextOuter = (side + 1) % Sides * 2 + i * Sides * 2;
                    int currentNextOuter = (i + 1) * Sides * 2 + side * 2;
                    int nextNextOuter = (i + 1) * Sides * 2 + (side + 1) % Sides * 2;

                    int currentInner = currentOuter + 1;
                    int nextInner = nextOuter + 1;
                    int currentNextInner = currentNextOuter + 1;
                    int nextNextInner = nextNextOuter + 1;

                    // Outer side
                    indices[indicesIndex++] = currentOuter;
                    indices[indicesIndex++] = nextNextOuter;
                    indices[indicesIndex++] = currentNextOuter;

                    indices[indicesIndex++] = currentOuter;
                    indices[indicesIndex++] = nextOuter;
                    indices[indicesIndex++] = nextNextOuter;

                    // Inner side
                    indices[indicesIndex++] = currentInner;
                    indices[indicesIndex++] = currentNextInner;
                    indices[indicesIndex++] = nextNextInner;
                    indices[indicesIndex++] = currentInner;
                    indices[indicesIndex++] = nextNextInner;
                    indices[indicesIndex++] = nextInner;
                }
                else
                {
                    int current = i * Sides + side;
                    int next = (side + 1) % Sides + i * Sides;
                    int currentNext = (i + 1) * Sides + side;
                    int nextNext = (i + 1) * Sides + (side + 1) % Sides;

                    indices[indicesIndex++] = current;
                    indices[indicesIndex++] = nextNext;
                    indices[indicesIndex++] = currentNext;

                    indices[indicesIndex++] = current;
                    indices[indicesIndex++] = next;
                    indices[indicesIndex++] = nextNext;
                }
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
        int startCapVertexOffset = vertices.Length - 2 * sides;
        int endCapVertexOffset = vertices.Length - sides;

        var startCenter = splinePoints[0];
        var endCenter = splinePoints[^1];

        // Generate vertices for the caps
        for (int side = 0; side < sides; side++)
        {
            float angle = side * MathUtil.TwoPi / sides;
            float x = (float)Math.Cos(angle) * Radius.Y;
            float z = (float)Math.Sin(angle) * Radius.Y;

            // Start cap vertices
            var startCapPosition = startCenter + new Vector3(x, 0, z);
            var startNormal = -Vector3.UnitY; // Normal pointing inward for the cap
            vertices[startCapVertexOffset + side] = new VertexPositionNormalTexture(startCapPosition, startNormal, new Vector2((float)side / sides, 0));

            // End cap vertices
            var endCapPosition = endCenter + new Vector3(x, 0, z);
            var endNormal = Vector3.UnitY; // Normal pointing outward for the cap
            vertices[endCapVertexOffset + side] = new VertexPositionNormalTexture(endCapPosition, endNormal, new Vector2((float)side / sides, 1));
        }

        // Generate indices for the start cap
        int startCenterVertexIndex = startCapVertexOffset + sides;
        vertices[startCenterVertexIndex] = new VertexPositionNormalTexture(startCenter, -Vector3.UnitY, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = (side + 1) % sides;

            indices[indicesIndex++] = startCenterVertexIndex; // Center vertex of the start cap
            indices[indicesIndex++] = startCapVertexOffset + nextSide;
            indices[indicesIndex++] = startCapVertexOffset + side;
        }

        // Generate indices for the end cap
        int endCenterVertexIndex = endCapVertexOffset + sides;
        vertices[endCenterVertexIndex] = new VertexPositionNormalTexture(endCenter, Vector3.UnitY, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = (side + 1) % sides;

            indices[indicesIndex++] = endCenterVertexIndex; // Center vertex of the end cap
            indices[indicesIndex++] = endCapVertexOffset + side;
            indices[indicesIndex++] = endCapVertexOffset + nextSide;
        }
    }

    private void CloseTubeEnds(int sides, Span<Vector3> splinePoints, VertexPositionNormalTexture[] vertices, int[] indices, ref int indicesIndex)
    {
        int startOuterCapVertexOffset = vertices.Length - 4 * sides;
        int startInnerCapVertexOffset = vertices.Length - 3 * sides;
        int endOuterCapVertexOffset = vertices.Length - 2 * sides;
        int endInnerCapVertexOffset = vertices.Length - sides;

        var startCenter = splinePoints[0];
        var endCenter = splinePoints[^1];

        // Generate vertices for the caps
        for (int side = 0; side < sides; side++)
        {
            float angle = side * MathUtil.TwoPi / sides;
            float cosAngle = (float)Math.Cos(angle);
            float sinAngle = (float)Math.Sin(angle);

            // Start cap vertices
            var startOuterCapPosition = startCenter + new Vector3(cosAngle * Radius.Y, 0, sinAngle * Radius.Y);
            var startInnerCapPosition = startCenter + new Vector3(cosAngle * Radius.X, 0, sinAngle * Radius.X);
            var startNormal = -Vector3.UnitY; // Normal pointing inward for the cap

            vertices[startOuterCapVertexOffset + side] = new VertexPositionNormalTexture(startOuterCapPosition, startNormal, new Vector2((float)side / sides, 0));
            vertices[startInnerCapVertexOffset + side] = new VertexPositionNormalTexture(startInnerCapPosition, startNormal, new Vector2((float)side / sides, 1));

            // End cap vertices
            var endOuterCapPosition = endCenter + new Vector3(cosAngle * Radius.Y, 0, sinAngle * Radius.Y);
            var endInnerCapPosition = endCenter + new Vector3(cosAngle * Radius.X, 0, sinAngle * Radius.X);
            var endNormal = Vector3.UnitY; // Normal pointing outward for the cap

            vertices[endOuterCapVertexOffset + side] = new VertexPositionNormalTexture(endOuterCapPosition, endNormal, new Vector2((float)side / sides, 0));
            vertices[endInnerCapVertexOffset + side] = new VertexPositionNormalTexture(endInnerCapPosition, endNormal, new Vector2((float)side / sides, 1));
        }

        // Generate indices for the start cap
        int startOuterCenterVertexIndex = startOuterCapVertexOffset + sides;
        int startInnerCenterVertexIndex = startInnerCapVertexOffset + sides;
        vertices[startOuterCenterVertexIndex] = new VertexPositionNormalTexture(startCenter, -Vector3.UnitY, new Vector2(0.5f, 0.5f));
        vertices[startInnerCenterVertexIndex] = new VertexPositionNormalTexture(startCenter, -Vector3.UnitY, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = (side + 1) % sides;

            // Outer cap
            indices[indicesIndex++] = startOuterCenterVertexIndex; // Center vertex of the start outer cap
            indices[indicesIndex++] = startOuterCapVertexOffset + nextSide;
            indices[indicesIndex++] = startOuterCapVertexOffset + side;

            // Inner cap
            indices[indicesIndex++] = startInnerCenterVertexIndex; // Center vertex of the start inner cap
            indices[indicesIndex++] = startInnerCapVertexOffset + side;
            indices[indicesIndex++] = startInnerCapVertexOffset + nextSide;
        }

        // Generate indices for the end cap
        int endOuterCenterVertexIndex = endOuterCapVertexOffset + sides;
        int endInnerCenterVertexIndex = endInnerCapVertexOffset + sides;
        vertices[endOuterCenterVertexIndex] = new VertexPositionNormalTexture(endCenter, Vector3.UnitY, new Vector2(0.5f, 0.5f));
        vertices[endInnerCenterVertexIndex] = new VertexPositionNormalTexture(endCenter, Vector3.UnitY, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = (side + 1) % sides;

            // Outer cap
            indices[indicesIndex++] = endOuterCenterVertexIndex; // Center vertex of the end outer cap
            indices[indicesIndex++] = endOuterCapVertexOffset + side;
            indices[indicesIndex++] = endOuterCapVertexOffset + nextSide;

            // Inner cap
            indices[indicesIndex++] = endInnerCenterVertexIndex; // Center vertex of the end inner cap
            indices[indicesIndex++] = endInnerCapVertexOffset + nextSide;
            indices[indicesIndex++] = endInnerCapVertexOffset + side;
        }
    }
}
