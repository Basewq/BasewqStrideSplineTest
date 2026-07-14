// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.ProceduralModels;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Models.Mesh;

[DataContract("Spline mesh")]
public abstract class SplineMesh : PrimitiveProceduralModelBase
{
    /// <summary>
    /// Spline used to generate the mesh.
    /// </summary>
    [DataMemberIgnore]
    protected internal Spline Spline { get; set; }
    /// <summary>
    /// Spline Evaluator used to generate the mesh.
    /// </summary>
    [DataMemberIgnore]
    protected internal ISplineEvaluator SplineEvaluator { get; set; }

    /// <summary>
    /// Generate geometry for endings
    /// </summary>
    public bool CloseEnds { get; set; }

    /// <summary>
    /// The sampling settings for the mesh spline.
    /// </summary>
    public SplineSamplingSettings MeshSamplingSettings { get; set; } = new();

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

    /// <summary>
    /// Builds the profile vertices from the given spline that will be orientated from the spline's orientation to XY plane.
    /// </summary>
    public static ProfileVertex[] BuildProfileVertices(
        Spline spline, ISplineEvaluator splineEvaluator, SplineSamplingSettings samplingSettings,
        ShapeProfilePlane profilePlane, ShapeProfileFlipAxis profileFlipAxis, bool invertNormals)
    {
        var splineSamples = new List<SplineSample>();
        SplineExtensions.CollectSplineSamples(splineEvaluator, samplingSettings, splineSamples);
        var splineSamplesSpan = CollectionsMarshal.AsSpan(splineSamples);
        int splineSamplesCount = splineSamplesSpan.Length;

        var profileVertices = new ProfileVertex[splineSamplesCount];

        if (profileFlipAxis != ShapeProfileFlipAxis.None)
        {
            var flipVector = new Vector3
            (
                x: ((profileFlipAxis & ShapeProfileFlipAxis.X) > 0) ? -1 : 1,
                y: ((profileFlipAxis & ShapeProfileFlipAxis.Y) > 0) ? -1 : 1,
                z: ((profileFlipAxis & ShapeProfileFlipAxis.Z) > 0) ? -1 : 1
            );
            for (int i = 0; i < splineSamplesCount; i++)
            {
                splineSamplesSpan[i].Position *= flipVector;
                splineSamplesSpan[i].Tangent *= flipVector;
            }
        }

        // The profile's forward vector relative to the spline's frame
        var splineFwdVec = profilePlane switch
        {
            ShapeProfilePlane.XY => Vector3.UnitZ,
            ShapeProfilePlane.YZ => Vector3.UnitX,
            ShapeProfilePlane.ZY => -Vector3.UnitX,
            _ => -Vector3.UnitY,     // XZ is default
        };
        // The profile's up vector relative to the spline's frame
        var splineUpVec = profilePlane switch
        {
            ShapeProfilePlane.XY => Vector3.UnitY,
            ShapeProfilePlane.YZ => Vector3.UnitZ,
            ShapeProfilePlane.ZY => Vector3.UnitY,
            _ => Vector3.UnitZ,     // XZ is default
        };
        // Get the rotation so the spline's profile sits in XY plane
        var profileRotation = Quaternion.Identity;
        if (profilePlane != ShapeProfilePlane.XY)
        {
            profileRotation = Quaternion.LookRotation(splineFwdVec, splineUpVec);
            profileRotation.Invert();
        }

        int vertexProfilesIndex = 0;
        for (int i = 0; i < splineSamplesCount; i++)
        {
            var sample = splineSamplesSpan[i];

            var profileForward = profileRotation * sample.Tangent;
            var profileUp = sample.Orientation * profileRotation * splineUpVec;
            var profileRight = Vector3.Normalize(Vector3.Cross(profileForward, profileUp));
            var orthoProfileUp = Vector3.Normalize(Vector3.Cross(profileRight, profileForward));

            var profilePosition = profileRotation * sample.Position;
            profileVertices[vertexProfilesIndex++] = new ProfileVertex
            {
                Position = profilePosition,
                Normal = orthoProfileUp,    // Default assumption is spline goes left to right on XY plane, so the normal vector is 'left' of the forward.
                ProfileT = sample.SplineT
            };
        }

        if (spline.IsClosedLoop)
        {
            // Shoelace formula to determine if spline is clockwise or counter-clockwise so we know which of left/profileRight is inside/outside.
            float area = 0;
            for (int i = 0; i < splineSamplesCount - 1; i++)
            {
                var currentPoint = profileVertices[i].Position;
                var nextPoint = profileVertices[i + 1].Position;
                area += currentPoint.X * nextPoint.Y - nextPoint.X * currentPoint.Y;
            }

            if (area > 0)
            {
                // Counter-clockwise -> Left = Inside, Right = Outside
                invertNormals = !invertNormals;
            }
            // Clockwise -> Left = Inside, Right = Outside -> No change
        }

        if (invertNormals)
        {
            for (int i = 0; i < profileVertices.Length; i++)
            {
                profileVertices[i].Normal = -profileVertices[i].Normal;
            }
        }

        return profileVertices;
    }
}
