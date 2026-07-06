// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Graphics;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("SplineMeshShape")]
[Display("Spline")]
public class SplineMeshShape : SplineMesh
{
    public SplineComponent SplineComponent;

    protected override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData()
    {
        if (SplineComponent is null)
        {
            return null;
        }

        var splinePoints = new List<Vector3>();
        SplineExtensions.CollectSplineSamplePoints(Spline, splinePoints);
        var shapePoints = new List<Vector3>();
        SplineExtensions.CollectSplineSamplePoints(Spline, shapePoints);

        int splinePointCount = splinePoints.Count;
        var shapePointsCount = shapePoints.Count;

        var totalVertexCount = 4 * (shapePointsCount - 1) * (splinePointCount - 1);
        var totalIndicesCount = (totalVertexCount / 4) * 6;

        var verticesPerShapeCount = (shapePointsCount - 1) * 2;
        var indicesPerShapeCount = (shapePointsCount - 1) * 6;

        var vertices = new VertexPositionNormalTexture[totalVertexCount];
        var indices = new int[totalIndicesCount];

        int verticesIndex = 0;
        Vector3 posA, posB, posC, posD;
        float splineDistance = 0.0f;

        for (int i = 0; i < splinePointCount - 1; i++)
        {
            var startPoint = splinePoints[i];
            var targetPoint = splinePoints[i + 1];
            var splineForward = targetPoint - startPoint;

            splineForward.Normalize();
            var left = Vector3.Cross(splineForward, Vector3.UnitY);
            var right = -left;
            float textureY;

            for (int j = 0; j < shapePointsCount - 1; j++)
            {
                var startShapePoint = shapePoints[j];
                var targetShapePoint = shapePoints[j + 1];

                var shapeForward = (targetShapePoint - startShapePoint);
                shapeForward.Normalize();
                var normal = Vector3.Cross(shapeForward, Vector3.UnitY);
                // First vertices
                if (j == 0)
                {
                    var temp = right;
                    temp *= targetShapePoint.X - startShapePoint.X;
                    temp.Y += targetShapePoint.Y - startShapePoint.Y;
                    posA = startPoint;
                    posB = startPoint + temp;
                    posC = targetPoint;
                    posD = targetPoint + temp;

                    textureY = splineDistance / UvScale.Y;
                    vertices[verticesIndex++] = CreateVertex(posA, normal, new Vector2(0, textureY));
                    vertices[verticesIndex++] = CreateVertex(posB, normal, new Vector2(1, textureY));
                    vertices[verticesIndex++] = CreateVertex(posC, normal, new Vector2(0, textureY));
                    vertices[verticesIndex++] = CreateVertex(posD, normal, new Vector2(1, textureY));
                }
                else
                {
                    var temp = right;
                    temp.X *= targetShapePoint.X - startShapePoint.X;
                    temp.Y += targetShapePoint.Y - startShapePoint.Y;
                    //right *= offset.X;
                    posA = vertices[verticesIndex - 3].Position;
                    posB = vertices[verticesIndex - 3].Position + temp;
                    posC = vertices[verticesIndex - 1].Position;
                    posD = vertices[verticesIndex - 1].Position + temp;
                    splineDistance += Vector3.Distance(startPoint, targetPoint);
                    textureY = splineDistance / UvScale.Y;
                    vertices[verticesIndex++] = CreateVertex(posA, normal, new Vector2(0, textureY));
                    vertices[verticesIndex++] = CreateVertex(posB, normal, new Vector2(1, textureY));
                    vertices[verticesIndex++] = CreateVertex(posC, normal, new Vector2(0, textureY));
                    vertices[verticesIndex++] = CreateVertex(posD, normal, new Vector2(1, textureY));
                }
            }
        }

        for (int i = 0; i < splinePointCount - 1; i++)
        {
            for (int j = 0; j < shapePointsCount - 1; j++)
            {
                //if (j > 0)
                {
                    // Indices
                    int vertexIndex = i * verticesPerShapeCount * 2; // Huidige Spline iteratie, 6, 12, 18
                    int vertexShapeIndex = j * 4;

                    int indiceIndex = i * indicesPerShapeCount;
                    int triangleIndex = j * 6;

                    indices[indiceIndex + triangleIndex + 0] = vertexIndex + vertexShapeIndex + 0;
                    indices[indiceIndex + triangleIndex + 1] = vertexIndex + vertexShapeIndex + 2;
                    indices[indiceIndex + triangleIndex + 2] = vertexIndex + vertexShapeIndex + 1;

                    indices[indiceIndex + triangleIndex + 3] = vertexIndex + vertexShapeIndex + 1;
                    indices[indiceIndex + triangleIndex + 4] = vertexIndex + vertexShapeIndex + 2;
                    indices[indiceIndex + triangleIndex + 5] = vertexIndex + vertexShapeIndex + 3;
                }
            }
        }

        // Create the primitive object for further processing by the base class
        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }
}
