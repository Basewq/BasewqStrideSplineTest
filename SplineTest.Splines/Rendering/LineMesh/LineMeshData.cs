using Stride.Core.Mathematics;
using Stride.Graphics;

namespace SplineTest.Rendering.LineMesh;

record struct LineMeshData
{
    public static PrimitiveType PrimitiveType => PrimitiveType.TriangleList;

    public required LineVertex[] Vertices;
    public required ushort[] VertexIndices;
    public required int TriangleCount;
    public required BoundingBox BoundingBox;

    public static LineMeshData Generate()
    {
        /* Quad to Triangle (clockwise winding for DirectX):
         * 0---1
         * | / |
         * 2---3
         */
        var quadVertices = new LineVertex[]
        {
            new(new Vector3(-1, +1, 0)),
            new(new Vector3(+1, +1, 0)),
            new(new Vector3(-1, -1, 0)),
            new(new Vector3(+1, -1, 0)),
        };
        var vertexIndices = new ushort[] { 0, 1, 2, 1, 3, 2 };

        var boundingBox = new BoundingBox(minimum: -Vector3.One, maximum: Vector3.One);
        var mesh = new LineMeshData
        {
            Vertices = quadVertices,
            VertexIndices = vertexIndices,
            TriangleCount = 2,
            BoundingBox = boundingBox,
        };
        return mesh;
    }
}
