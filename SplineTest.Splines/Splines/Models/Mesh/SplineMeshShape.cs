// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Graphics;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshShape")]
[Display("Spline")]
public class SplineMeshShape : SplineMesh
{
    public SplineComponent ShapeSplineComponent;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        if (ShapeSplineComponent?.Spline is null)
        {
            return null;
        }

        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(Spline, splineSamples, sampleStepDistance: 0.5f);     // TODO adaptive
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        var shapeProfileVertices = BuildProfileVertices(ShapeSplineComponent.Spline);

        int splinePointCount = splineSamplesSpan.Length;
        int shapeProfileVerticesCount = shapeProfileVertices.Length;
        int shapeProfileEdgeCount = shapeProfileVerticesCount - 1;

        int totalVertexCount = splinePointCount * shapeProfileVerticesCount;
        int totalIndicesCount = (splinePointCount - 1) * shapeProfileEdgeCount * 6;

        var vertices = new VertexPositionNormalTexture[totalVertexCount];
        var indices = new int[totalIndicesCount];

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;

        var prevSplinePosition = splineSamplesSpan[0].Position;
        for (int i = 0; i < splinePointCount; i++)
        {
            var sample = splineSamplesSpan[i];
            var splinePosition = sample.Position;
            var forward = sample.Tangent;

            var profileLocalRotation = Quaternion.BetweenDirections(Vector3.UnitZ, forward);

            splineDistance += Vector3.Distance(prevSplinePosition, splinePosition);
            prevSplinePosition = splinePosition;
            float textureY = splineDistance / EnsureNonZero(UvScale.Y);
            for (int profIdx = 0; profIdx < shapeProfileVertices.Length; profIdx++)
            {
                ref readonly var profVert = ref shapeProfileVertices[profIdx];

                var vertPos = profileLocalRotation * profVert.Position;
                vertPos += splinePosition;
                var vertNorm = profileLocalRotation * profVert.Normal;
                var texCoordX = profVert.ProfileT / EnsureNonZero(UvScale.X);

                vertices[verticesIndex++] = CreateVertex(vertPos, vertNorm, new Vector2(texCoordX, textureY));
            }

        }

        for (int i = 0; i < splinePointCount - 1; i++)
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

    private float EnsureNonZero(float value)
    {
        return value == 0 ? 1 : value;
    }
}
