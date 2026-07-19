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

    public SplineSamplingSettings ShapeSamplingSettings { get; set; } = new();

    public ShapeProfilePlane ShapeProfilePlane = ShapeProfilePlane.XZ;
    public ShapeProfileFlipAxis ShapeProfileFlipAxis = ShapeProfileFlipAxis.Z;      // In the editor spline would most likely be drawn X right, Z down
    public bool ShapeProfileInvertNormals;
    public bool MeshFlipWinding;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        if (ShapeSplineComponent?.Spline is null)
        {
            return null;
        }

        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(SplineEvaluator, MeshSamplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        var shapeProfileVertices = BuildProfileVertices(ShapeSplineComponent.Spline, ShapeSplineComponent.SplineEvaluator, ShapeSamplingSettings, ShapeProfilePlane, ShapeProfileFlipAxis, ShapeProfileInvertNormals);

        int splineSamplesCount = splineSamplesSpan.Length;
        int shapeProfileVerticesCount = shapeProfileVertices.Length;
        int shapeProfileEdgeCount = shapeProfileVerticesCount - 1;

        int totalVertexCount = splineSamplesCount * shapeProfileVerticesCount;
        int totalIndicesCount = (splineSamplesCount - 1) * shapeProfileEdgeCount * 6;

        var vertices = new VertexPositionNormalTexture[totalVertexCount];
        var indices = new int[totalIndicesCount];

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

                if (MeshFlipWinding)
                {
                    Utilities.Swap(ref currentShapeVert0, ref currentShapeVert1);
                    Utilities.Swap(ref nextShapeVert0, ref nextShapeVert1);
                }
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
