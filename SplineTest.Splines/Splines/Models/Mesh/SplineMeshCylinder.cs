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
        return CreateCylinder(
            SplineEvaluator, MeshSamplingSettings,
            Sides, Radius, CloseEnds, MeshScale, UvScale);
    }

    public static GeometricMeshData<VertexPositionNormalTexture> CreateCylinder(
        ISplineEvaluator splineEvaluator, SplineSamplingSettings meshSamplingSettings,
        int sides, float radius, bool CloseEnds, Vector2 meshScale, Vector2 uvScale)
    {
        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(splineEvaluator, meshSamplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;

        int ringVertexCount = sides + 1;        // +1 to duplicate start/end vertex for UV wrapping
        int vertexCount = splineSamplesCount * ringVertexCount;
        int indicesCount = (splineSamplesCount - 1) * sides * 6;

        var spline = splineEvaluator.Spline;
        if (CloseEnds && !spline.IsClosedLoop)
        {
            // Cap made like pie slices (x2 for start and end caps)
            int capVertCount = ringVertexCount + 1;       // +1 for center vertex
            int capIndicesCount = sides * 3;
            vertexCount += capVertCount * 2;
            indicesCount += capIndicesCount * 2;
        }

        var shapeProfileVertices = new ProfileVertex[ringVertexCount];
        for (int i = 0; i < ringVertexCount; i++)
        {
            float circleT = i / (float)sides;       // Sides = ringVertexCount - 1
            float angle = circleT * MathUtil.TwoPi;
            float offsetX = -MathF.Cos(angle) * radius;     // Start on (-1, 0) then go clockwise
            float offsetY = MathF.Sin(angle) * radius;

            var position = new Vector3(offsetX, offsetY, 0);
            var normal = Vector3.Normalize(position);
            shapeProfileVertices[i] = new ProfileVertex { Position = position, Normal = normal, ProfileT = circleT };
        }
        if (meshScale != Vector2.One)
        {
            var scale3d = new Vector3(meshScale, 1);
            var inverseScaleMatrix = Matrix.Invert(Matrix.Scaling(scale3d));
            for (int i = 0; i < shapeProfileVertices.Length; i++)
            {
                shapeProfileVertices[i].Position *= scale3d;
                Vector3.TransformCoordinate(in shapeProfileVertices[i].Normal, in inverseScaleMatrix, out shapeProfileVertices[i].Normal);
            }
        }

        var vertices = new VertexPositionNormalTexture[vertexCount];
        var indices = new int[indicesCount];

        int verticesIndex = 0;
        int indicesIndex = 0;
        float splineDistance = 0.0f;
        var texCoordScale = uvScale with
        {
            X = uvScale.X == 0 ? 1 : 1f / uvScale.X,
            Y = uvScale.Y == 0 ? 1 : 1f / uvScale.Y
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

        // Close the cylinder ends
        if (CloseEnds && !spline.IsClosedLoop)
        {
            CloseCylinderEnds(sides, splineSamplesSpan, vertices, indices, ref indicesIndex);
        }

        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }

    private static void CloseCylinderEnds(
        int sides, Span<SplineSample> splineSamples, VertexPositionNormalTexture[] vertices, int[] indices,
        ref int indicesIndex)
    {
        int ringVertexCount = sides + 1;        // Duplicate start/end vertex for UV wrapping
        int cylinderVertexCount = splineSamples.Length * ringVertexCount;
        int startCapVertexOffset = cylinderVertexCount;
        int endCapVertexOffset = cylinderVertexCount + (ringVertexCount + 1);   // +1 for the center vertex

        var startCenter = splineSamples[0].Position;
        var endCenter = splineSamples[^1].Position;

        var startNormal = -splineSamples[0].Tangent;    // Face 'backwards' (ie. reversed tangent direction)
        var endNormal = splineSamples[^1].Tangent;      // Face 'forward' (ie. along tangent direction)

        // Generate vertices for the caps
        for (int side = 0; side <= sides; side++)
        {
            float circleT = side / (float)sides;
            float angle = circleT * MathUtil.TwoPi;

            float texCoordCircleX = -MathF.Cos(angle);
            float texCoordCircleY = -MathF.Sin(angle);      // Also negative because top side of circle maps to texCoord.Y = 0
            var texCoord = RemapCircleToSquare(texCoordCircleX, texCoordCircleY);

            int startCapPositionVertIdx = PositiveModulo((sides / 2) - side, sides);    // Start cap vertices is *mirrorred* from the profile shape, so get right side index and go counter-clockwise
            int endCapPositionVertIdx = startCapVertexOffset - ringVertexCount + side;
            ref readonly var startVert = ref vertices[startCapPositionVertIdx];
            ref readonly var endVert = ref vertices[endCapPositionVertIdx];

            // Start cap vertex
            vertices[startCapVertexOffset + side] = new VertexPositionNormalTexture(startVert.Position, startNormal, texCoord);

            // End cap vertex
            vertices[endCapVertexOffset + side] = new VertexPositionNormalTexture(endVert.Position, endNormal, texCoord);
        }

        // Generate indices for the start cap
        int startCenterVertexIndex = startCapVertexOffset + ringVertexCount;
        vertices[startCenterVertexIndex] = new VertexPositionNormalTexture(startCenter, startNormal, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = side + 1;

            indices[indicesIndex++] = startCenterVertexIndex;   // Center vertex of the start cap
            indices[indicesIndex++] = startCapVertexOffset + side;
            indices[indicesIndex++] = startCapVertexOffset + nextSide;
        }

        // Generate indices for the end cap
        int endCenterVertexIndex = endCapVertexOffset + ringVertexCount;
        vertices[endCenterVertexIndex] = new VertexPositionNormalTexture(endCenter, endNormal, new Vector2(0.5f, 0.5f));
        for (int side = 0; side < sides; side++)
        {
            int nextSide = side + 1;

            indices[indicesIndex++] = endCenterVertexIndex;     // Center vertex of the end cap
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

        // Remap [-1, 1] range to [0, 1]
        return new Vector2(sx * 0.5f + 0.5f, sy * 0.5f + 0.5f);
    }

    private static int PositiveModulo(int value, int n)
    {
        int remainder = value % n;
        if (value < 0)
        {
            return remainder + n;
        }
        else
        {
            return remainder;
        }
    }
}
