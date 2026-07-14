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
        SplineExtensions.CollectSplineSamplePositionsByResolution(Spline, splinePoints);
        var splinePointsSpan = CollectionsMarshal.AsSpan(splinePoints);
        int splinePointCount = splinePointsSpan.Length;
        int vertexCount = splinePointCount * 4 * 2;         // 4 vertPosOffset * 2 per corner - don't share vertices because we want hard normals/texture coords for each face
        int indicesCount = (splinePointCount - 1) * 24;     // 4 quads * 2 tris * 3 indices

        if (CloseEnds && !Spline.IsClosedLoop)
        {
            vertexCount += 8;   // Additional vertices for the start and end caps
            indicesCount += 12; // Additional triangles for the caps
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        float halfWidth = Scale.X / 2;
        var halfHeight3d = new Vector3(0, Scale.Y / 2, 0);
        int verticesIndex = 0;
        int triangleIndex = 0;
        float splineDistance = 0.0f;

        Span<Vector3> faceNormals = stackalloc Vector3[]
        {
            -Vector3.UnitY, // Down Vector3(0,1,0)
            -Vector3.UnitX, // Right Vector3(0,1,0)
            +Vector3.UnitY, // Up Vector3(0,1,0)
            +Vector3.UnitX  // Left Vector3(0,1,0)
        };
        Span<Vector3> vertPosOffset = stackalloc Vector3[4];
        for (int i = 0; i < splinePointCount - 1; i++)
        {
            var startPoint = splinePointsSpan[i];
            var targetPoint = splinePointsSpan[i + 1];
            var forward = Vector3.Normalize(targetPoint - startPoint);

            var right = Vector3.Cross(forward, Vector3.UnitY) * halfWidth;      // TODO use spline's Up orientation in the future?
            var left = -right;

            // Create vertices
            vertPosOffset[0] = left - halfHeight3d;   // Bottom left
            vertPosOffset[1] = right - halfHeight3d;  // Bottom right
            vertPosOffset[2] = right + halfHeight3d;  // Top right
            vertPosOffset[3] = left + halfHeight3d;   // Top Left

            // Generate vertices around the spline at position 'startPoint'
            if (i == 0) // First vertices
            {
                // Loop over each face in following order: Bottom, Right, Top, Left
                for (int offsetIdx = 0; offsetIdx < vertPosOffset.Length; offsetIdx++)
                {
                    vertices[verticesIndex] = CreateVertex(startPoint + vertPosOffset[offsetIdx], faceNormals[offsetIdx], new Vector2(0, 0));
                    vertices[verticesIndex + 1] = CreateVertex(startPoint + vertPosOffset[(offsetIdx + 1) % 4], faceNormals[offsetIdx], new Vector2(1, 0));
                    verticesIndex += 2;
                }
            }

            // Generate vertices around the spline at position 'targetPoint'
            splineDistance += Vector3.Distance(startPoint, targetPoint);
            float texCoordY = splineDistance / UvScale.Y;
            // Loop over each face in following order: Bottom, Right, Top, Left
            for (int offsetIdx = 0; offsetIdx < vertPosOffset.Length; offsetIdx++)
            {
                vertices[verticesIndex] = CreateVertex(targetPoint + vertPosOffset[offsetIdx], faceNormals[offsetIdx], new Vector2(0, texCoordY));
                vertices[verticesIndex + 1] = CreateVertex(targetPoint + vertPosOffset[(offsetIdx + 1) % 4], faceNormals[offsetIdx], new Vector2(1, texCoordY));
                verticesIndex += 2;
            }

            // Create indices
            int indiceIndex = i * 24;

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

        if (Spline.IsClosedLoop)
        {
            // Stitch the start/end positions to remove seams.
            int endStartIndex = vertices.Length - 8;
            for (int i = 0; i < 8; i++)
            {
                StitchVertexPositions(vertices, i, endStartIndex + i);
            }
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
        vertices[verticesIndex + 0] = CreateVertex(vertices[0].Position, -Vector3.UnitZ, new Vector2(0, 1));                // Bottom left
        vertices[verticesIndex + 1] = CreateVertex(vertices[1].Position, -Vector3.UnitZ, new Vector2(1, 1));                // Bottom right
        vertices[verticesIndex + 2] = CreateVertex(vertices[4].Position, -Vector3.UnitZ, new Vector2(1, 0));                // Top right
        vertices[verticesIndex + 3] = CreateVertex(vertices[5].Position, -Vector3.UnitZ, new Vector2(0, 0));                // Top left

        // Back face vertices (effectively mirrored direction)
        vertices[verticesIndex + 4] = CreateVertex(vertices[backIndex - 8].Position, Vector3.UnitZ, new Vector2(1, 1));     // Bottom right
        vertices[verticesIndex + 5] = CreateVertex(vertices[backIndex - 7].Position, Vector3.UnitZ, new Vector2(0, 1));     // Bottom left
        vertices[verticesIndex + 6] = CreateVertex(vertices[backIndex - 4].Position, Vector3.UnitZ, new Vector2(0, 0));     // Top left
        vertices[verticesIndex + 7] = CreateVertex(vertices[backIndex - 3].Position, Vector3.UnitZ, new Vector2(1, 0));     // Top right

        int closeIndicesIndex = indicesCount - 12;
        int vertexCountIndex = vertexCount - 8;

        // Front
        indices[closeIndicesIndex + 0] = vertexCountIndex + 0;
        indices[closeIndicesIndex + 1] = vertexCountIndex + 3;
        indices[closeIndicesIndex + 2] = vertexCountIndex + 2;

        indices[closeIndicesIndex + 3] = vertexCountIndex + 0;
        indices[closeIndicesIndex + 4] = vertexCountIndex + 2;
        indices[closeIndicesIndex + 5] = vertexCountIndex + 1;
        closeIndicesIndex += 6;

        // Back (effectively mirrored direction)
        int closeVerticesIndex = vertexCount - 4;
        indices[closeIndicesIndex + 0] = closeVerticesIndex + 1;
        indices[closeIndicesIndex + 1] = closeVerticesIndex + 2;
        indices[closeIndicesIndex + 2] = closeVerticesIndex + 3;

        indices[closeIndicesIndex + 3] = closeVerticesIndex + 1;
        indices[closeIndicesIndex + 4] = closeVerticesIndex + 3;
        indices[closeIndicesIndex + 5] = closeVerticesIndex + 0;
    }
}
