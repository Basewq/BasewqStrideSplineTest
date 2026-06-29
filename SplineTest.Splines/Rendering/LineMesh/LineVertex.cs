using System.Runtime.InteropServices;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace SplineTest.Rendering.LineMesh;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct LineVertex : IVertex
{
    public static readonly VertexDeclaration Layout = CreateVertexDeclaration();
    private static VertexDeclaration CreateVertexDeclaration()
    {
        var vertElems = new VertexElement[]
        {
            VertexElement.Position<Vector3>(),
            //VertexElement.Normal<Vector3>(),

            // Colors
            //VertexElement.Color<Color>(semanticIndex: 0),

            // Texture Coordinates
            //VertexElement.TextureCoordinate<Vector2>(semanticIndex: 0),
            //VertexElement.TextureCoordinate<Vector2>(semanticIndex: 1),
        };
        return new VertexDeclaration(vertElems);
    }

    public Vector3 Position;
    //public Vector3 Normal = Vector3.UnitZ;

    public LineVertex(Vector3 position)
    {
        Position = position;
    }

    //public Color Color;

    //public Vector2 TextureCoords0;

    public readonly VertexDeclaration GetLayout() => Layout;

    public void FlipWinding()
    {
        //TextureCoords0.X = (1.0f - TextureCoords0.X);
    }
}
