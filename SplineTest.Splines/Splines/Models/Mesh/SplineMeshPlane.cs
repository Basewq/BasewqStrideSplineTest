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

        float halfWidth = Scale.X * 0.5f;

        // Single edge
        Span<ProfileVertex> shapeProfileVertices = stackalloc ProfileVertex[]
        {
            new ProfileVertex { Position = new(-halfWidth, 0, 0), Normal = Vector3.UnitY, ProfileT =  0 },
            new ProfileVertex { Position = new(+halfWidth, 0, 0), Normal = Vector3.UnitY, ProfileT =  1 },
        };

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;
        var texCoordScale = UvScale with
        {
            X = UvScale.X == 0 ? 1 : 1f / UvScale.X,
            Y = UvScale.Y == 0 ? 1 : 1f / UvScale.Y
        };
        var prevSplinePosition = splineSamplesSpan[0].Position;
        for (int i = 0; i < splineSamplesCount; i++)
        {
            ref readonly var sample = ref splineSamplesSpan[i];
            var splinePosition = sample.Position;
            var splineRotation = sample.Orientation;

            splineDistance += Vector3.Distance(prevSplinePosition, splinePosition);
            prevSplinePosition = splinePosition;
            float textureY = splineDistance * texCoordScale.Y;
            for (int profIdx = 0; profIdx < shapeProfileVertices.Length; profIdx++)
            {
                ref readonly var profVert = ref shapeProfileVertices[profIdx];

                var vertPos = splineRotation * profVert.Position;
                vertPos += splinePosition;
                var vertNorm = splineRotation * profVert.Normal;
                float texCoordX = profVert.ProfileT * texCoordScale.X;

                vertices[verticesIndex++] = CreateVertex(vertPos, vertNorm, new Vector2(texCoordX, textureY));
            }
        }

        int shapeProfileVerticesCount = shapeProfileVertices.Length;
        for (int i = 0; i < splineSamplesCount - 1; i++)
        {
            int currentShapeStartIndex = i * shapeProfileVerticesCount;
            int nextShapeStartIndex = (i + 1) * shapeProfileVerticesCount;

            for (int j = 0; j < shapeProfileVerticesCount - 1; j++)
            {
                int currentShapeVert0 = currentShapeStartIndex + j;
                int currentShapeVert1 = currentShapeVert0 + 1;
                int nextShapeVert0 = nextShapeStartIndex + j;
                int nextShapeVert1 = nextShapeVert0 + 1;

                indices[indicesIndex++] = currentShapeVert0;
                indices[indicesIndex++] = nextShapeVert1;
                indices[indicesIndex++] = nextShapeVert0;

                indices[indicesIndex++] = currentShapeVert0;
                indices[indicesIndex++] = currentShapeVert1;
                indices[indicesIndex++] = nextShapeVert1;
            }
        }

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }
}
