// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Buffer = Stride.Graphics.Buffer;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;

/// <summary>
/// Bounding box mesh with length one unit with the origin at the center of the box.
/// </summary>
public class GizmoBoundingBoxMesh : ComponentBase
{
    /// <summary>
    /// The vertex buffer used by this mesh.
    /// </summary>
    public readonly Buffer VertexBuffer;

    /// <summary>
    /// The index buffer used by this mesh.
    /// </summary>
    public readonly Buffer IndexBuffer;

    private GizmoBoundingBoxMesh(GraphicsDevice graphicsDevice, VertexPositionNormalTexture[] vertices, ushort[] indices)
    {
        VertexBuffer = Buffer.Vertex.New(graphicsDevice, vertices).RecreateWith(vertices).DisposeBy(this);
        IndexBuffer = Buffer.Index.New(graphicsDevice, indices).RecreateWith(indices).DisposeBy(this);
    }

    public MeshDraw ToMeshDraw()
    {
        var vertexBufferBinding = new VertexBufferBinding(VertexBuffer, VertexPositionNormalTexture.Layout, VertexBuffer.ElementCount);
        var indexBufferBinding = new IndexBufferBinding(IndexBuffer, is32Bit: false, IndexBuffer.ElementCount);
        var meshDraw = new MeshDraw
        {
            PrimitiveType = PrimitiveType.LineList,
            DrawCount = IndexBuffer.ElementCount,
            VertexBuffers = [vertexBufferBinding],
            IndexBuffer = indexBufferBinding,
        };
        return meshDraw;
    }

    public static GizmoBoundingBoxMesh CreateMesh(GraphicsDevice graphicsDevice)
    {
        var indices = new ushort[12 * 2];
        var vertices = new VertexPositionNormalTexture[8];

        const float HalfLength = 0.5f;
        vertices[0] = new VertexPositionNormalTexture(new Vector3(-HalfLength, HalfLength, -HalfLength), Vector3.UnitY, Vector2.Zero);
        vertices[1] = new VertexPositionNormalTexture(new Vector3(-HalfLength, HalfLength, +HalfLength), Vector3.UnitY, Vector2.Zero);
        vertices[2] = new VertexPositionNormalTexture(new Vector3(+HalfLength, HalfLength, +HalfLength), Vector3.UnitY, Vector2.Zero);
        vertices[3] = new VertexPositionNormalTexture(new Vector3(+HalfLength, HalfLength, -HalfLength), Vector3.UnitY, Vector2.Zero);

        int indexOffset = 0;
        // Top sides
        for (int i = 0; i < 4; i++)
        {
            indices[indexOffset++] = (ushort)i;
            indices[indexOffset++] = (ushort)((i + 1) % 4);
        }

        // Duplicate vertices and indices to bottom part
        for (int i = 0; i < 4; i++)
        {
            vertices[i + 4] = vertices[i];
            vertices[i + 4].Position.Y = -vertices[i + 4].Position.Y;

            indices[indexOffset++] = (ushort)(indices[i * 2] + 4);
            indices[indexOffset++] = (ushort)(indices[i * 2 + 1] + 4);
        }

        // Sides
        for (int i = 0; i < 4; i++)
        {
            indices[indexOffset++] = (ushort)i;
            indices[indexOffset++] = (ushort)(i + 4);
        }

        return new GizmoBoundingBoxMesh(graphicsDevice, vertices, indices);
    }
}
