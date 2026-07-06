// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Games;
using Stride.Graphics;
using Stride.Graphics.Meshes;
using Stride.Rendering;
using Stride.Rendering.Materials;
using Stride.Rendering.Materials.ComputeColors;
using System.Runtime.InteropServices;

namespace Stride.Engine.Splines.Processors;

public class SplineProcessor : EntityProcessor<SplineComponent, SplineProcessor.AssociatedData>
{
    private IGraphicsDeviceService graphicsDeviceService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SplineTransformProcessor"/> class.
    /// </summary>
    public SplineProcessor()
        : base(typeof(TransformComponent))
    {
    }

    protected override void OnSystemAdd()
    {
        graphicsDeviceService = Services.GetSafeServiceAs<IGraphicsDeviceService>();
    }

    protected override AssociatedData GenerateComponentData(Entity entity, SplineComponent component)
    {
        return new AssociatedData
        {
        };
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] SplineComponent component, [NotNull] AssociatedData data)
    {
        if (data.LineVisualizerComponent is not null)
        {
            entity.Remove(data.LineVisualizerComponent);
        }
        if (data.GizmoMarkerSetComponent is not null)
        {
            entity.Remove(data.GizmoMarkerSetComponent);
        }
        if (data.BoundingBoxEntity is not null)
        {
            data.BoundingBoxEntity.SetParent(null);
            data.BoundingBoxEntity.Scene = null;
            data.BoundingBoxEntity.Dispose();
        }
    }

    protected override bool IsAssociatedDataValid(Entity entity, SplineComponent component, AssociatedData associatedData)
    {
        return true;
    }

    public override void Update(GameTime time)
    {
        foreach (var (splineComp, data) in ComponentDatas)
        {
            if (splineComp.HasDebugRenderSettingsChanged)
            {
                UpdateDebugRendering(splineComp, data);
            }
        }
    }

    private void UpdateDebugRendering(SplineComponent splineComp, AssociatedData data)
    {
        var renderSettings = splineComp.DebugRenderSettings;
        var graphicsDevice = graphicsDeviceService.GraphicsDevice;

        if (renderSettings.ShowCurves)
        {
            var splineEntity = splineComp.Entity;
            if (data.LineVisualizerComponent is null)
            {
                data.LineVisualizerComponent = new LineVisualizerComponent();
                data.LineVisualizerComponent.LineSet.OccludedStyle = LineOccludedStyle.Checkered;
                splineEntity.Add(data.LineVisualizerComponent);
            }
            else
            {
                data.LineVisualizerComponent.LineSet.Segments.Clear();
            }

            var splineSamplePoints = new List<Vector3>();
            SplineExtensions.CollectSplineSamplePoints(splineComp.Spline, splineSamplePoints);

            var lineSet = data.LineVisualizerComponent.LineSet;
            var splineSamplePointsSpan = CollectionsMarshal.AsSpan(splineSamplePoints);
            for (int i = 0; i < splineSamplePointsSpan.Length - 1; i++)
            {
                var lineStartPos = splineSamplePointsSpan[i];
                var lineNextPos = splineSamplePointsSpan[i + 1];

                var lineColor = renderSettings.CurveColor.ToColor4();
                lineSet.AddWorldLine(lineStartPos, lineNextPos, lineColor, lineThicknessPx: 3, emissiveScale: 1);
            }
        }
        else
        {
            data.LineVisualizerComponent?.Enabled = false;
        }

        if (renderSettings.ShowControlPoints)
        {
            var splineEntity = splineComp.Entity;
            if (data.GizmoMarkerSetComponent is null)
            {
                data.GizmoMarkerSetComponent = new GizmoMarkerSetComponent();
                data.GizmoMarkerSetComponent.GizmoMarkerSet.OccludedStyle = GizmoMarkerOccludedStyle.Checkered;
                splineEntity.Add(data.GizmoMarkerSetComponent);
            }
            else
            {
                data.GizmoMarkerSetComponent.GizmoMarkerSet.Markers.Clear();
            }

            var controlPointFillColor = AddEmissiveScale(Color.White.ToColor4(), emissiveScale: 1);
            var controlPointOutlineColor = Color.Black.ToColor4();
            const float ShapeSize = 13;
            const float OutlineWidthPx = 1f;

            var markerSet = data.GizmoMarkerSetComponent.GizmoMarkerSet;
            var controlPoints = splineComp.Spline.ControlPoints;
            for (int i = 0; i < controlPoints.Count; i++)
            {
                var ctrlPointMarker = new GizmoMarkerData
                {
                    Shape = GizmoMarkerShape.Circle,
                    OrientationMode = GizmoMarkerOrientationMode.Billboard,
                    ScaleMode = GizmoMarkerScaleMode.FixedScreenSize,
                    Position = controlPoints[i].Position,
                    FillColor = controlPointFillColor,
                    SizePx = new Vector2(ShapeSize),
                    OutlineWidthPx = OutlineWidthPx,
                    OutlineColor = controlPointOutlineColor,
                };
                markerSet.Markers.Add(ctrlPointMarker);
            }
        }
        else
        {
            data.GizmoMarkerSetComponent?.Enabled = false;
        }

        if (renderSettings.ShowBoundingBox)
        {
            if (data.BoundingBoxEntity is null)
            {
                data.BoundingBoxEntity = new Entity("DebugSplineBoundingBox");

                var boundingBoxMesh = BoundingBoxMesh.CreateMesh(graphicsDevice).DisposeBy(data.BoundingBoxEntity);
                var boundingBoxMeshDraw = boundingBoxMesh.ToMeshDraw();
                data.BoundingBoxMaterial = CreateMaterial(graphicsDevice, renderSettings.BoundingBoxColor, 0.75f);
                var boundingBoxModelComponent = new ModelComponent
                {
                    Enabled = true,
                    Model = new Model { data.BoundingBoxMaterial, new Mesh { Draw = boundingBoxMeshDraw } },
                    RenderGroup = renderSettings.RenderGroup,
                    IsShadowCaster = false,
                };
                data.BoundingBoxEntity.Add(boundingBoxModelComponent);
                data.BoundingBoxEntity.SetParent(splineComp.Entity);
            }
            else
            {
                UpdateColor(graphicsDevice, data.BoundingBoxMaterial, renderSettings.BoundingBoxColor, 0.75f);
            }

            // Update bounding box size
            {
                var boundingBox = splineComp.Spline.CalculateBoundingBox();
                var boxLengths = boundingBox.Maximum - boundingBox.Minimum;
                data.BoundingBoxEntity.Transform.Scale = boxLengths;
                data.BoundingBoxEntity.Transform.Position = boundingBox.Center;
            }
        }
        else if (data.BoundingBoxEntity is not null)
        {
            data.BoundingBoxEntity.SetParent(null);
            data.BoundingBoxEntity.Scene = null;
            data.BoundingBoxEntity.Dispose();
            data.BoundingBoxEntity = null;
        }

        splineComp.HasDebugRenderSettingsChanged = false;
    }

    private static Color4 AddEmissiveScale(Color4 color, float emissiveScale)
    {
        color.R += color.R * emissiveScale;
        color.G += color.G * emissiveScale;
        color.B += color.B * emissiveScale;
        return color;
    }

    private static Material CreateMaterial(GraphicsDevice device, Color color, float intensity = 1f)
    {
        MaterialTransparencyBlendFeature? transparencyBlendFeature = null;
        if (color.A <= 255)
        {
            transparencyBlendFeature = new MaterialTransparencyBlendFeature()
            {
                Alpha = new ComputeFloat(color.A / 255f)
            };
        }
        var material = Material.New(device, new MaterialDescriptor
        {
            Attributes =
            {
                Diffuse = new MaterialDiffuseMapFeature(new ComputeColor()),
                DiffuseModel = new MaterialDiffuseLambertModelFeature(),
                Emissive = new MaterialEmissiveMapFeature(new ComputeColor()),
                Transparency = transparencyBlendFeature
            }
        });

        // set the color to the material
        UpdateColor(device, material, color, intensity);

        // set the transparency property to the material if necessary
        if (color.A < byte.MaxValue)
        {

            material.Passes[0].HasTransparency = true;
        }

        return material;
    }

    private static void UpdateColor(GraphicsDevice device, Material material, Color color, float intensity = 1f)
    {
        // set the color to the material
        material.Passes[0].Parameters.Set(MaterialKeys.DiffuseValue, new Color4(color).ToColorSpace(device.ColorSpace));

        material.Passes[0].Parameters.Set(MaterialKeys.EmissiveIntensity, intensity);
        material.Passes[0].Parameters.Set(MaterialKeys.EmissiveValue, new Color4(color).ToColorSpace(device.ColorSpace));
    }

    public class AssociatedData
    {
        internal LineVisualizerComponent LineVisualizerComponent;
        internal GizmoMarkerSetComponent GizmoMarkerSetComponent;

        internal Entity? BoundingBoxEntity;
        internal Material? BoundingBoxMaterial;

    }
}
