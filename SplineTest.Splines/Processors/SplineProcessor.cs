// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Extensions;
using Stride.Games;
using Stride.Graphics;
using Stride.Graphics.GeometricPrimitives;
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
                splineEntity.Add(data.LineVisualizerComponent);
            }
            else
            {
                data.LineVisualizerComponent.LineSet.Segments.Clear();
            }

            var splineSamplePoints = new List<SplineSample>();
            splineComp.Spline.CollectSplineSamples(splineSamplePoints);

            var lineSet = data.LineVisualizerComponent.LineSet;
            var splineSamplePointsSpan = CollectionsMarshal.AsSpan(splineSamplePoints);
            for (int i = 0; i < splineSamplePointsSpan.Length - 1; i++)
            {
                var lineStartPos = splineSamplePointsSpan[i].Position;
                var lineNextPos = splineSamplePointsSpan[i + 1].Position;

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
            if (data.ControlPointsRootEntity is null)
            {
                data.ControlPointsRootEntity = new Entity("DebugSplineControlPoints");
                data.ControlPointsRootEntity.SetParent(splineComp.Entity);
            }
            if (data.ControlPointsMeshDraw is null)
            {
                var sphereMesh = GeometricPrimitive.Sphere.New(graphicsDevice, radius: 0.1f, tessellation: 6).DisposeBy(data.ControlPointsRootEntity);
                var sphereMeshDraw = sphereMesh.ToMeshDraw();
                data.ControlPointsMeshDraw = sphereMeshDraw;
            }
            if (data.ControlPointsMaterial is null)
            {
                data.ControlPointsMaterial = CreateMaterial(graphicsDevice, renderSettings.ControlPointColor, 0.75f);
            }
            else
            {
                UpdateColor(graphicsDevice, data.ControlPointsMaterial, renderSettings.ControlPointColor, 0.75f);
            }

            var controlPoints = splineComp.Spline.ControlPoints;
            int controlPointCount = controlPoints.Count;
            var controlPointEntityList = data.ControlPointsEntityList;
            if (controlPointEntityList.Count < controlPointCount)
            {
                // Populate new control points
                int entityStartIndex = controlPointEntityList.Count;
                int addEntityCount = controlPointCount - entityStartIndex;
                for (int i = 0; i < addEntityCount; i++)
                {
                    int ctrlPointIndex = entityStartIndex + i;

                    var ctrlPointModelComponent = new ModelComponent
                    {
                        Enabled = true,
                        Model = new Model { data.ControlPointsMaterial, new Mesh { Draw = data.ControlPointsMeshDraw } },
                        RenderGroup = renderSettings.RenderGroup,
                        IsShadowCaster = false,
                    };
                    var controlPointEntity = new Entity($"DebugSplineControlPoint_{ctrlPointIndex}")
                    {
                        ctrlPointModelComponent
                    };
                    // Attach to root control point controlPointEntity
                    controlPointEntity.SetParent(data.ControlPointsRootEntity);

                    controlPointEntityList.Add(controlPointEntity);
                }
            }
            else if (controlPointEntityList.Count > controlPointCount)
            {
                // Remove excess control points
                int entityEndIndex = controlPointCount;
                for (int i = controlPointEntityList.Count - 1; i >= entityEndIndex; i--)
                {
                    var controlPointEntity = controlPointEntityList[i];
                    controlPointEntity.SetParent(null);
                    controlPointEntity.Scene = null;

                    controlPointEntityList.RemoveAt(i);
                }
            }
            // Now update all the positions
            for (int i = 0; i < controlPoints.Count; i++)
            {
                var controlPointEntity = controlPointEntityList[i];
                controlPointEntity.Transform.Position = controlPoints[i].Position;
            }
        }
        else if (data.ControlPointsRootEntity is not null)
        {
            data.ControlPointsRootEntity.SetParent(null);
            data.ControlPointsRootEntity.Scene = null;
            data.ControlPointsRootEntity.Dispose();
            data.ControlPointsRootEntity = null;
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

        internal Entity? ControlPointsRootEntity;
        internal readonly List<Entity> ControlPointsEntityList = [];
        internal MeshDraw? ControlPointsMeshDraw;
        internal Material? ControlPointsMaterial;

        internal Entity? BoundingBoxEntity;
        internal Material? BoundingBoxMaterial;

    }
}
