using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Spline;

public static class GizmoModelHelper
{
    /// <summary>
    /// Creates a cylinder line model that lies in XZ plane starting at origin and extends in +Z axis direction with length <paramref name="length"/>.
    /// </summary>
    public static GeometricPrimitive CreateLine(GraphicsDevice graphicsDevice, float length = 1.0f, float radius = 0.5f, int tessellation = 32)
    {
        var meshData = GeometricPrimitive.Cylinder.New(length, radius, tessellation);
        // Mesh data is vertical and centered, we want to change this so it starts at the origin and lies in XZ plane and points in +Z axis
        var scale = Vector3.One;
        var rotation = Quaternion.RotationX(MathUtil.PiOverTwo);
        var positionOffset = new Vector3(0, 0, length * 0.5f);
        Matrix.Transformation(ref scale, ref rotation, ref positionOffset, out var transformMatrix);
        for (int i = 0; i < meshData.Vertices.Length; i++)
        {
            var oldPos = meshData.Vertices[i].Position;
            var newPos = Vector3.Transform(oldPos, transformMatrix);
            meshData.Vertices[i].Position = newPos.XYZ();
        }
        var geometricPrimitive = new GeometricPrimitive(graphicsDevice, meshData);
        return geometricPrimitive;
    }
}
