// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshBox")]
[Display("Box")]
public class SplineMeshBox : SplineMesh
{
    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var splinePoints = new List<Vector3>();
        SplineExtensions.CollectSplineSamplePoints(Spline, splinePoints);
        var splinePointsSpan = CollectionsMarshal.AsSpan(splinePoints);
        int splinePointCount = splinePointsSpan.Length;
        int vertexCount = splinePointCount * 4 * 2; // 4 sides * 2 per corner
        int indicesCount = (splinePointCount - 1) * 24;

        if (Spline.IsClosedLoop)
        {
            vertexCount += 4;
            indicesCount += 24;
        }
        else if (CloseEnds && !Spline.IsClosedLoop)
        {
            vertexCount += 8;   // Additional vertices for the start and end caps
            indicesCount += 12; // Additional triangles for the caps
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        var halfWidth = Scale.X / 2;
        var halfHeight = new Vector3(0, Scale.Y / 2, 0);
        int verticesIndex = 0;
        int triangleIndex = 0;
        float splineDistance = 0.0f;

        Span<Vector3> normals = stackalloc Vector3[]
        {
            -Vector3.UnitY, // Down Vector3(0,1,0)
            -Vector3.UnitX, // Right Vector3(0,1,0)
            +Vector3.UnitY, // Up Vector3(0,1,0)
            +Vector3.UnitX  // Left Vector3(0,1,0)
        };
        Span<Vector3> sides = stackalloc Vector3[4];
        for (int i = 0; i < splinePointCount - 1; i++)
        {
            var startPoint = splinePointsSpan[i];
            var targetPoint = splinePointsSpan[i + 1];
            var forward = (targetPoint - startPoint);
            forward.Normalize();
            var right = Vector3.Cross(forward, Vector3.UnitY) * halfWidth;
            var left = -right;
            float textureY;

            // Create vertices
            sides[0] = left - halfHeight;   // Bottom left
            sides[1] = right - halfHeight;  // Bottom right
            sides[2] = right + halfHeight;  // Top right
            sides[3] = left + halfHeight;   // Top Left

            if (i == 0) // First vertexes
            {
                // Spline.IsClosedLoop over each side in following order: Bottom, Right, Top, Left
                for (int side = 0; side < sides.Length; side++)
                {
                    vertices[verticesIndex] = CreateVertex(startPoint + sides[side], normals[side], new Vector2(0, 0));
                    vertices[verticesIndex + 1] = CreateVertex(startPoint + sides[(side + 1) % 4], normals[side], new Vector2(1, 0));
                    verticesIndex += 2;
                }
            }

            if (i == splinePointCount - 2 && Spline.IsClosedLoop) // If Spline.IsClosedLoop is enabled, then the target node is the first node in the entire spline
            {
                splineDistance += Vector3.Distance(startPoint, splinePointsSpan[0]);
                textureY = splineDistance / UvScale.Y;

                for (int side = 0; side < sides.Length; side++)
                {
                    vertices[verticesIndex] = CreateVertex(vertices[side].Position, normals[side], new Vector2(0, textureY));
                    vertices[verticesIndex + 1] = CreateVertex(vertices[(side + 1) % 4].Position, normals[side], new Vector2(1, textureY));
                    verticesIndex += 2;
                }
            }
            else
            {
                splineDistance += Vector3.Distance(startPoint, targetPoint);
                textureY = splineDistance / UvScale.Y;
                for (int side = 0; side < sides.Length; side++)
                {
                    vertices[verticesIndex] = CreateVertex(targetPoint + sides[side], normals[side], new Vector2(0, textureY));
                    vertices[verticesIndex + 1] = CreateVertex(targetPoint + sides[(side + 1) % 4], normals[side], new Vector2(1, textureY));
                    verticesIndex += 2;
                }
            }


            // Create indices
            var indiceIndex = i * 24;

            // Bottom
            indices[indiceIndex + 0] = 0 + triangleIndex;
            indices[indiceIndex + 1] = 1 + triangleIndex;
            indices[indiceIndex + 2] = 8 + triangleIndex;

            indices[indiceIndex + 3] = 1 + triangleIndex;
            indices[indiceIndex + 4] = 9 + triangleIndex;
            indices[indiceIndex + 5] = 8 + triangleIndex;

            // Right
            indices[indiceIndex + 6] = 2 + triangleIndex;
            indices[indiceIndex + 7] = 3 + triangleIndex;
            indices[indiceIndex + 8] = 10 + triangleIndex;

            indices[indiceIndex + 9] = 3 + triangleIndex;
            indices[indiceIndex + 10] = 11 + triangleIndex;
            indices[indiceIndex + 11] = 10 + triangleIndex;

            // Top
            indices[indiceIndex + 12] = 4 + triangleIndex;
            indices[indiceIndex + 13] = 5 + triangleIndex;
            indices[indiceIndex + 14] = 12 + triangleIndex;

            indices[indiceIndex + 15] = 5 + triangleIndex;
            indices[indiceIndex + 16] = 13 + triangleIndex;
            indices[indiceIndex + 17] = 12 + triangleIndex;

            // Left
            indices[indiceIndex + 18] = 6 + triangleIndex;
            indices[indiceIndex + 19] = 7 + triangleIndex;
            indices[indiceIndex + 20] = 15 + triangleIndex;

            indices[indiceIndex + 21] = 6 + triangleIndex;
            indices[indiceIndex + 22] = 15 + triangleIndex;
            indices[indiceIndex + 23] = 14 + triangleIndex;

            triangleIndex += 8;
        }

        // If this was the last loop, we do 1 additional check for Closing of the sides or looping the geometry
        if (!Spline.IsClosedLoop && CloseEnds)
        {
            CloseBoxEnds(vertices, indices, verticesIndex, indicesCount, vertexCount);
        }

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }

    private void CloseBoxEnds(
        VertexPositionNormalTexture[] vertices, int[] indices,
        int verticesIndex, int indicesCount, int vertexCount)
    {
        int backIndex = verticesIndex;

        // Front face vertices
        vertices[verticesIndex + 0] = CreateVertex(vertices[0].Position, -Vector3.UnitZ, new Vector2(0, 0));
        vertices[verticesIndex + 1] = CreateVertex(vertices[1].Position, -Vector3.UnitZ, new Vector2(1, 0));
        vertices[verticesIndex + 2] = CreateVertex(vertices[4].Position, -Vector3.UnitZ, new Vector2(0, 1));
        vertices[verticesIndex + 3] = CreateVertex(vertices[5].Position, -Vector3.UnitZ, new Vector2(1, 1));

        // Back face vertices
        vertices[verticesIndex + 4] = CreateVertex(vertices[backIndex - 8].Position, Vector3.UnitZ, new Vector2(0, 0));
        vertices[verticesIndex + 5] = CreateVertex(vertices[backIndex - 7].Position, Vector3.UnitZ, new Vector2(1, 0));
        vertices[verticesIndex + 6] = CreateVertex(vertices[backIndex - 4].Position, Vector3.UnitZ, new Vector2(0, 1));
        vertices[verticesIndex + 7] = CreateVertex(vertices[backIndex - 3].Position, Vector3.UnitZ, new Vector2(1, 1));

        int closeIndicesIndex = indicesCount - 12;
        int vertexCountIndex = vertexCount - 8;

        // Front
        indices[closeIndicesIndex + 0] = vertexCountIndex + 0;
        indices[closeIndicesIndex + 1] = vertexCountIndex + 3;
        indices[closeIndicesIndex + 2] = vertexCountIndex + 1;

        indices[closeIndicesIndex + 3] = vertexCountIndex + 1;
        indices[closeIndicesIndex + 4] = vertexCountIndex + 3;
        indices[closeIndicesIndex + 5] = vertexCountIndex + 2;
        closeIndicesIndex += 6;

        // Back
        int closeVerticesIndex = vertexCount - 4;
        indices[closeIndicesIndex + 0] = closeVerticesIndex + 0;
        indices[closeIndicesIndex + 1] = closeVerticesIndex + 1;
        indices[closeIndicesIndex + 2] = closeVerticesIndex + 3;

        indices[closeIndicesIndex + 3] = closeVerticesIndex + 1;
        indices[closeIndicesIndex + 4] = closeVerticesIndex + 2;
        indices[closeIndicesIndex + 5] = closeVerticesIndex + 3;
    }
}
