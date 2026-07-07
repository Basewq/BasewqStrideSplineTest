// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering.ProceduralModels;

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
}
