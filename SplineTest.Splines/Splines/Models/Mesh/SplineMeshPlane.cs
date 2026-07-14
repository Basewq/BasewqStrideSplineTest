// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshPlane")]
[Display("Plane")]
public class SplineMeshPlane : SplineMesh
{
    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(SplineEvaluator, MeshSamplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;
        int vertexCount = splineSamplesCount * 2;         // 2 vertices to form a single edge
        int indexCount = (splineSamplesCount - 1) * 6;    // 6 indices = 2 tris * 3 indices => 1 quad

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indexCount];

        var halfWidth = Scale.X / 2;
        int verticesIndex = 0;
        int triangleIndex = 0;
        float splineDistance = 0.0f;

        for (int i = 0; i < splineSamplesCount - 1; i++)
        {
            var startPoint = splineSamplesSpan[i].Position;
            var targetPoint = splineSamplesSpan[i + 1].Position;
            var forward = Vector3.Normalize(targetPoint - startPoint);

            var left = Vector3.Cross(forward, Vector3.UnitY) * halfWidth;
            var right = -left;
            var normal = Vector3.UnitY;

            // Generate vertices around the spline at position 'startPoint'
            if (i == 0)
            {
                vertices[verticesIndex] = new VertexPositionNormalTexture(startPoint + left, normal, new Vector2(0, 0));
                vertices[verticesIndex + 1] = new VertexPositionNormalTexture(startPoint + right, normal, new Vector2(1, 0));
                verticesIndex += 2;
            }

            // Generate vertices around the spline at position 'targetPoint'
            splineDistance += Vector3.Distance(startPoint, targetPoint);
            float textureY = splineDistance / UvScale.Y;
            vertices[verticesIndex] = new VertexPositionNormalTexture(targetPoint + left, normal, new Vector2(0, textureY));
            vertices[verticesIndex + 1] = new VertexPositionNormalTexture(targetPoint + right, normal, new Vector2(1, textureY));
            verticesIndex += 2;

            // Create indices
            var indicesIndex = i * 6;
            SetIndices(indices, triangleIndex, indicesIndex);
            triangleIndex += 2;
        }

        if (Spline.IsClosedLoop)
        {
            // Stitch the start/end positions to remove seams.
            int endStartIndex = vertices.Length - 2;
            for (int i = 0; i < 2; i++)
            {
                StitchVertexPositions(vertices, i, endStartIndex + i);
            }
        }

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }

    private static void SetIndices(int[] indices, int triangleIndex, int indiceIndex)
    {
        indices[indiceIndex + 0] = triangleIndex + 0;
        indices[indiceIndex + 1] = triangleIndex + 1;
        indices[indiceIndex + 2] = triangleIndex + 2;
        indices[indiceIndex + 3] = triangleIndex + 1;
        indices[indiceIndex + 4] = triangleIndex + 3;
        indices[indiceIndex + 5] = triangleIndex + 2;
    }
}
