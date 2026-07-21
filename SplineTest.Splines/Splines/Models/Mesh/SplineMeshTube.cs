// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
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
        bool isHollow = Radius.X > 0;
        if (!isHollow)
        {
            // Just a standard cylinder
            return SplineMeshCylinder.CreateCylinder(
                SplineEvaluator, MeshSamplingSettings,
                Sides, Radius.Y, CloseEnds, MeshScale, UvScale);
        }

        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(SplineEvaluator, MeshSamplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;

        int ringVertexCount = Sides + 1;        // +1 to duplicate start/end vertex for UV wrapping
        int cylinderVertexCount = splineSamplesCount * ringVertexCount;
        int cylinderIndicesCount = (splineSamplesCount - 1) * Sides * 6;

        int totalVertexCount = cylinderVertexCount * 2;
        int totalIndicesCount = cylinderIndicesCount * 2;

        int innerTubeVerticesStartIndex = cylinderVertexCount;
        int innerTubeIndicesStartIndex = cylinderIndicesCount;

        if (CloseEnds && !Spline.IsClosedLoop)
        {
            // Cap made with quads (x2 for start and end caps)
            int capVertCount = ringVertexCount * 2;
            int capIndicesCount = Sides * 6;
            totalVertexCount += capVertCount * 2;
            totalIndicesCount += capIndicesCount * 2;
        }

        var shapeProfileVertices = new ProfileVertex[ringVertexCount];
        for (int i = 0; i < ringVertexCount; i++)
        {
            float circleT = i / (float)Sides;       // Sides = ringVertexCount - 1
            float angle = circleT * MathUtil.TwoPi;
            // Actual radius not applied here due to outer/inner sizes
            float offsetX = -MathF.Cos(angle);      // Start on (-1, 0) then go clockwise
            float offsetY = MathF.Sin(angle);

            var position = new Vector3(offsetX, offsetY, 0);
            var normal = Vector3.Normalize(position);
            shapeProfileVertices[i] = new ProfileVertex { Position = position, Normal = normal, ProfileT = circleT };
        }
        if (MeshScale != Vector2.One)
        {
            var scale3d = new Vector3(MeshScale, 1);
            var inverseScaleMatrix = Matrix.Invert(Matrix.Scaling(scale3d));
            for (int i = 0; i < shapeProfileVertices.Length; i++)
            {
                shapeProfileVertices[i].Position *= scale3d;
                Vector3.TransformCoordinate(in shapeProfileVertices[i].Normal, in inverseScaleMatrix, out shapeProfileVertices[i].Normal);
            }
        }

        var vertices = new VertexPositionNormalTexture[totalVertexCount];
        var indices = new int[totalIndicesCount];

        int outerTubeVerticesIndex = 0;
        int outerTubeIndicesIndex = 0;
        int innerTubeVerticesIndex = innerTubeVerticesStartIndex;
        int innerTubeIndicesIndex = innerTubeIndicesStartIndex;
        float splineDistance = 0.0f;
        var texCoordScale = UvScale with
        {
            X = UvScale.X == 0 ? 1 : 1f / UvScale.X,
            Y = UvScale.Y == 0 ? 1 : 1f / UvScale.Y
        };
        var prevSplinePosition = splineSamplesSpan[0].Position;
        var outerRadiusScale = new Vector3(Radius.Y, Radius.Y, 1);
        var innerRadiusScale = new Vector3(Radius.X, Radius.X, 1);
        for (int i = 0; i < splineSamplesCount; i++)
        {
            ref readonly var sample = ref splineSamplesSpan[i];
            var splinePosition = sample.Position;
            var splineRotation = sample.Orientation;

            splineDistance += Vector3.Distance(prevSplinePosition, splinePosition);
            prevSplinePosition = splinePosition;
            float textureY = splineDistance * texCoordScale.Y;
            // Outer tube
            for (int profIdx = 0; profIdx < shapeProfileVertices.Length; profIdx++)
            {
                ref readonly var profVert = ref shapeProfileVertices[profIdx];

                var vertPos = splineRotation * (profVert.Position * outerRadiusScale);
                vertPos += splinePosition;
                var vertNorm = splineRotation * profVert.Normal;
                float texCoordX = profVert.ProfileT * texCoordScale.X;

                vertices[outerTubeVerticesIndex++] = CreateVertex(vertPos, vertNorm, new Vector2(texCoordX, textureY));
            }
            // Inner tube
            for (int profIdx = 0; profIdx < shapeProfileVertices.Length; profIdx++)
            {
                ref readonly var profVert = ref shapeProfileVertices[profIdx];

                var vertPos = splineRotation * (profVert.Position * innerRadiusScale);
                vertPos += splinePosition;
                var vertNorm = splineRotation * -profVert.Normal;
                float texCoordX = profVert.ProfileT * texCoordScale.X;

                vertices[innerTubeVerticesIndex++] = CreateVertex(vertPos, vertNorm, new Vector2(texCoordX, textureY));
            }
        }

        int shapeProfileVerticesCount = shapeProfileVertices.Length;
        for (int i = 0; i < splineSamplesCount - 1; i++)
        {
            int currentShapeStartIndex = i * shapeProfileVerticesCount;
            int nextShapeStartIndex = (i + 1) * shapeProfileVerticesCount;
            // Outer tube indices
            for (int j = 0; j < shapeProfileVerticesCount - 1; j++)
            {
                int currentShapeVert0 = currentShapeStartIndex + j;
                int currentShapeVert1 = currentShapeVert0 + 1;
                int nextShapeVert0 = nextShapeStartIndex + j;
                int nextShapeVert1 = nextShapeVert0 + 1;

                indices[outerTubeIndicesIndex++] = currentShapeVert0;
                indices[outerTubeIndicesIndex++] = nextShapeVert1;
                indices[outerTubeIndicesIndex++] = nextShapeVert0;

                indices[outerTubeIndicesIndex++] = currentShapeVert0;
                indices[outerTubeIndicesIndex++] = currentShapeVert1;
                indices[outerTubeIndicesIndex++] = nextShapeVert1;
            }
            // Inner tube indices
            for (int j = 0; j < shapeProfileVerticesCount - 1; j++)
            {
                int currentShapeVert0 = currentShapeStartIndex + j + cylinderVertexCount;
                int currentShapeVert1 = currentShapeVert0 + 1;
                int nextShapeVert0 = nextShapeStartIndex + j + cylinderVertexCount;
                int nextShapeVert1 = nextShapeVert0 + 1;
                // Note the winding is reversed compared to the outer tube since we want to see the 'inside' of the tube
                indices[innerTubeIndicesIndex++] = currentShapeVert0;
                indices[innerTubeIndicesIndex++] = nextShapeVert0;
                indices[innerTubeIndicesIndex++] = nextShapeVert1;

                indices[innerTubeIndicesIndex++] = currentShapeVert0;
                indices[innerTubeIndicesIndex++] = nextShapeVert1;
                indices[innerTubeIndicesIndex++] = currentShapeVert1;
            }
        }

        // Close the tube ends
        if (CloseEnds && !Spline.IsClosedLoop)
        {
            CloseTubeEnds(Sides, splineSamplesSpan, vertices, indices, ref innerTubeIndicesIndex);
        }

        return new GeometricMeshData<VertexPositionNormalTexture>(vertices, indices, isLeftHanded: false);
    }

    private static void CloseTubeEnds(
        int sides, Span<SplineSample> splineSamples, VertexPositionNormalTexture[] vertices, int[] indices,
        ref int indicesIndex)
    {
        int ringVertexCount = sides + 1;        // Duplicate start/end vertex for UV wrapping
        int cylinderVertexCount = splineSamples.Length * ringVertexCount;
        int startOuterCapVertexOffset = 2 * cylinderVertexCount;
        int startInnerCapVertexOffset = startOuterCapVertexOffset + (1 * ringVertexCount);
        int endOuterCapVertexOffset = startOuterCapVertexOffset + (2 * ringVertexCount);
        int endInnerCapVertexOffset = startOuterCapVertexOffset + (3 * ringVertexCount);

        var startNormal = -splineSamples[0].Tangent;    // Face 'backwards' (ie. reversed tangent direction)
        var endNormal = splineSamples[^1].Tangent;      // Face 'forward' (ie. along tangent direction)

        // Generate vertices for the caps
        for (int side = 0; side <= sides; side++)
        {
            float circleT = side / (float)sides;
            var texCoord0 = new Vector2(circleT, 0);
            var texCoord1 = new Vector2(circleT, 1);

            int outerStartCapPositionVertIdx = PositiveModulo((sides / 2) - side, sides);    // Start cap vertices is *mirrorred* from the profile shape, so get right side index and go counter-clockwise
            int outerEndCapPositionVertIdx = cylinderVertexCount - ringVertexCount + side;
            int innerStartCapPositionVertIdx = cylinderVertexCount + outerStartCapPositionVertIdx;
            int innerEndCapPositionVertIdx = (2 * cylinderVertexCount) - ringVertexCount + side;
            ref readonly var outerStartVert = ref vertices[outerStartCapPositionVertIdx];
            ref readonly var outerEndVert = ref vertices[outerEndCapPositionVertIdx];
            ref readonly var innerStartVert = ref vertices[innerStartCapPositionVertIdx];
            ref readonly var innerEndVert = ref vertices[innerEndCapPositionVertIdx];

            // Start cap vertex
            vertices[startOuterCapVertexOffset + side] = new VertexPositionNormalTexture(outerStartVert.Position, startNormal, texCoord0);
            vertices[startInnerCapVertexOffset + side] = new VertexPositionNormalTexture(innerStartVert.Position, startNormal, texCoord1);

            // End cap vertex
            vertices[endOuterCapVertexOffset + side] = new VertexPositionNormalTexture(outerEndVert.Position, endNormal, texCoord0);
            vertices[endInnerCapVertexOffset + side] = new VertexPositionNormalTexture(innerEndVert.Position, endNormal, texCoord1);
        }

        // Generate indices for the start cap
        for (int side = 0; side < sides; side++)
        {
            int currentRingVert0 = startOuterCapVertexOffset + side;
            int currentRingVert1 = currentRingVert0 + 1;
            int nextRingVert0 = startInnerCapVertexOffset + side;
            int nextRingVert1 = nextRingVert0 + 1;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = nextRingVert1;
            indices[indicesIndex++] = nextRingVert0;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = currentRingVert1;
            indices[indicesIndex++] = nextRingVert1;
        }

        // Generate indices for the end cap
        for (int side = 0; side < sides; side++)
        {
            int currentRingVert0 = endOuterCapVertexOffset + side;
            int currentRingVert1 = currentRingVert0 + 1;
            int nextRingVert0 = endInnerCapVertexOffset + side;
            int nextRingVert1 = nextRingVert0 + 1;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = nextRingVert1;
            indices[indicesIndex++] = nextRingVert0;

            indices[indicesIndex++] = currentRingVert0;
            indices[indicesIndex++] = currentRingVert1;
            indices[indicesIndex++] = nextRingVert1;
        }
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
