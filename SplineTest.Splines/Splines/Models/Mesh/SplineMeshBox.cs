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
        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(SplineEvaluator, MeshSamplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;
        int vertexCount = splineSamplesCount * 4 * 2;       // 4 edges * 2 vertices per edge - don't share vertices because we want hard normals/texture coords for each face
        int indicesCount = (splineSamplesCount - 1) * 24;   // 4 quads * 2 tris * 3 indices

        if (CloseEnds && !Spline.IsClosedLoop)
        {
            vertexCount += 8;   // Additional vertices for the start and end caps
            indicesCount += 12; // Additional triangles for the caps
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        float halfWidth = MeshScale.X  * 0.5f;
        float halfHeight = MeshScale.Y * 0.5f;

        // Edges/Vertices in clockwise order, starting from top-left
        Span<ProfileVertex> shapeProfileVertices = stackalloc ProfileVertex[]
        {
            // Top
            new ProfileVertex { Position = new(-halfWidth, +halfHeight, 0), Normal = +Vector3.UnitY, ProfileT =  0 },
            new ProfileVertex { Position = new(+halfWidth, +halfHeight, 0), Normal = +Vector3.UnitY, ProfileT =  1 },
            // Right
            new ProfileVertex { Position = new(+halfWidth, +halfHeight, 0), Normal = +Vector3.UnitX, ProfileT =  0 },
            new ProfileVertex { Position = new(+halfWidth, -halfHeight, 0), Normal = +Vector3.UnitX, ProfileT =  1 },
            // Bottom
            new ProfileVertex { Position = new(+halfWidth, -halfHeight, 0), Normal = -Vector3.UnitY, ProfileT =  0 },
            new ProfileVertex { Position = new(-halfWidth, -halfHeight, 0), Normal = -Vector3.UnitY, ProfileT =  1 },
            // Left
            new ProfileVertex { Position = new(-halfWidth, -halfHeight, 0), Normal = -Vector3.UnitX, ProfileT =  0 },
            new ProfileVertex { Position = new(-halfWidth, +halfHeight, 0), Normal = -Vector3.UnitX, ProfileT =  1 },
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
            // Increment by 2 because edges are separate
            for (int j = 0; j < shapeProfileVerticesCount - 1; j += 2)
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

        // If this was the last loop, we do 1 additional check for Closing of the sides or looping the geometry
        if (!Spline.IsClosedLoop && CloseEnds)
        {
            CloseBoxEnds(splineSamplesSpan, vertices, indices, verticesIndex, indicesCount, vertexCount);
        }

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }

    private void CloseBoxEnds(
        Span<SplineSample> splineSamples,
        VertexPositionNormalTexture[] vertices, int[] indices,
        int verticesIndex, int indicesCount, int vertexCount)
    {
        int backIndex = verticesIndex;

        var startNormal = -splineSamples[0].Tangent;    // Face 'backwards' (ie. reversed tangent direction)
        var endNormal = splineSamples[^1].Tangent;      // Face 'forward' (ie. along tangent direction)

        // Spline start 'front' face vertices (effectively the 'back' of the profile shape so vertices should be from mirrored position)
        vertices[verticesIndex + 0] = CreateVertex(vertices[1].Position, startNormal, new Vector2(0, 0));                // Top left
        vertices[verticesIndex + 1] = CreateVertex(vertices[0].Position, startNormal, new Vector2(1, 0));                // Top right
        vertices[verticesIndex + 2] = CreateVertex(vertices[5].Position, startNormal, new Vector2(1, 1));                // Bottom right
        vertices[verticesIndex + 3] = CreateVertex(vertices[4].Position, startNormal, new Vector2(0, 1));                // Bottom left

        // Spline end 'back' face vertices (effectively the same direction as the profile shape)
        vertices[verticesIndex + 4] = CreateVertex(vertices[backIndex - 8].Position, endNormal, new Vector2(0, 0));     // Top left
        vertices[verticesIndex + 5] = CreateVertex(vertices[backIndex - 7].Position, endNormal, new Vector2(1, 0));     // Top right
        vertices[verticesIndex + 6] = CreateVertex(vertices[backIndex - 4].Position, endNormal, new Vector2(1, 1));     // Bottom right
        vertices[verticesIndex + 7] = CreateVertex(vertices[backIndex - 3].Position, endNormal, new Vector2(0, 1));     // Bottom left

        int closeIndicesIndex = indicesCount - 12;
        int vertexCountIndex = vertexCount - 8;

        // Front
        indices[closeIndicesIndex + 0] = vertexCountIndex + 0;
        indices[closeIndicesIndex + 1] = vertexCountIndex + 2;
        indices[closeIndicesIndex + 2] = vertexCountIndex + 3;

        indices[closeIndicesIndex + 3] = vertexCountIndex + 0;
        indices[closeIndicesIndex + 4] = vertexCountIndex + 1;
        indices[closeIndicesIndex + 5] = vertexCountIndex + 2;
        closeIndicesIndex += 6;

        // Back
        int closeVerticesIndex = vertexCount - 4;
        indices[closeIndicesIndex + 0] = closeVerticesIndex + 0;
        indices[closeIndicesIndex + 1] = closeVerticesIndex + 2;
        indices[closeIndicesIndex + 2] = closeVerticesIndex + 3;

        indices[closeIndicesIndex + 3] = closeVerticesIndex + 0;
        indices[closeIndicesIndex + 4] = closeVerticesIndex + 1;
        indices[closeIndicesIndex + 5] = closeVerticesIndex + 2;
    }
}
