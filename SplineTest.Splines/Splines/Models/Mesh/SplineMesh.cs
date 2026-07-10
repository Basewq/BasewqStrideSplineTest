// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Graphics;
using Stride.Rendering.ProceduralModels;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("Spline mesh")]
public abstract class SplineMesh : PrimitiveProceduralModelBase
{
    [DataMemberIgnore] public Spline Spline;

    public float Rotation;

    /// <summary>
    /// Generate geometry for endings
    /// </summary>
    public bool CloseEnds;

    protected abstract override GeometricMeshData<VertexPositionNormalTexture> CreatePrimitiveMeshData();

    protected static VertexPositionNormalTexture CreateVertex(Vector3 position, Vector3 normal, Vector2 texCoord)
    {
        return new VertexPositionNormalTexture(position, normal, texCoord);
    }

    protected static Vector3 CalculateRadialNormal(Vector3 vertexPosition, Vector3 centerPosition)
    {
        var radialVector = vertexPosition - centerPosition;
        radialVector.Normalize();
        return radialVector;
    }

    protected static void StitchVertexPositions(VertexPositionNormalTexture[] vertices, int index1, int index2)
    {
        var pos1 = vertices[index1].Position;
        var pos2 = vertices[index2].Position;
        var avgPos = (pos1 + pos2) * 0.5f;
        vertices[index1].Position = avgPos;
        vertices[index2].Position = avgPos;
    }

    public static ProfileVertex[] BuildProfileVertices(Spline spline)
    {
        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(spline, splineSamples, sampleStepDistance: 0.5f);     // TODO adaptive
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;

        var profileVertices = new ProfileVertex[splineSamplesCount];

        int vertexProfilesIndex = 0;
        for (int i = 0; i < splineSamplesCount; i++)
        {
            var sample = splineSamplesSpan[i];

            var forward = sample.Tangent;
            var up = sample.Rotation * Vector3.UnitY;
            var right = Vector3.Normalize(Vector3.Cross(up, forward));
            var orthoUp = Vector3.Cross(forward, right);    // Ensure the actual up used is right-angled

            profileVertices[vertexProfilesIndex++] = new ProfileVertex
            {
                Position = sample.Position,
                Normal = orthoUp,
                ProfileT = sample.SplineT
            };
        }

        return profileVertices;
    }
}
