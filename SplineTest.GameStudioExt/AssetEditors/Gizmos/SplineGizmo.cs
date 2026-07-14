// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.AssetEditors.Gizmos;
using SplineTest.Rendering;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Engine.Gizmos;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Graphics.Meshes;
using Stride.Rendering;
using System.Runtime.InteropServices;

namespace Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;

[GizmoComponent(typeof(SplineComponent), isMainGizmo: true)]
public class SplineGizmo : BillboardingGizmo<SplineComponent>
{
    private static readonly Color BoundingBoxColor = Color.Orange;
    private static readonly Color4 CurveColor = Color.Aqua.ToColor4();

    internal const RenderGroup GizmoRenderGroup = DefaultGroup;

    private EditorGameSplineEditorService splineEditorService;

    private bool isSplineCurveUpdateRequired = false;
    private bool isSplineCurveRenderSuppressed = false;
    private bool isBoundingBoxUpdateRequired = false;
    internal Spline Spline => Component.Spline;

    private Material boundingBoxMaterial;
    private Entity boundingBoxEntity;
    private ModelComponent boundingBoxModelComponent;

    public SplineGizmo(EntityComponent component)
        : base(component, gizmoName: "Spline", GizmoResources.SplineGizmo)
    {
        RenderGroup = GizmoRenderGroup;     // Must keep this on DefaultGroup to prevent editor entity selection overriding our selection
    }

    public override void Initialize(IServiceRegistry services, Scene editorScene)
    {
        base.Initialize(services, editorScene);

        splineEditorService = Game.EditorServices.Get<EditorGameSplineEditorService>();

        boundingBoxMaterial = GizmoEmissiveColorMaterial.Create(GraphicsDevice, BoundingBoxColor, 0.75f);

        Component.SplinePropertyChanged += OnSplineChanged;
        Component.ControlPointsChanged += OnSplineChanged;
        OnSplineChanged(Component);
    }

    protected override void Destroy()
    {
        Component.SplinePropertyChanged -= OnSplineChanged;
        Component.ControlPointsChanged -= OnSplineChanged;

        base.Destroy();
    }

    private void OnSplineChanged(SplineComponent splineComponent)
    {
        // Enqueue change on next update
        isSplineCurveUpdateRequired = true;
        isBoundingBoxUpdateRequired = true;
    }

    public override void Update()
    {
        base.Update();
        if (ContentEntity is null || GizmoRootEntity is null)
        {
            return;
        }

        bool prevIsSplineCurveRenderSuppressed = isSplineCurveRenderSuppressed;
        isSplineCurveRenderSuppressed = splineEditorService.ActiveSplineComponent == Component;
        if (prevIsSplineCurveRenderSuppressed != isSplineCurveRenderSuppressed)
        {
            isSplineCurveUpdateRequired = true;
        }
        if (isSplineCurveUpdateRequired)
        {
            UpdateSplineCurveVisualizer();
            isSplineCurveUpdateRequired = false;
        }

        if (isBoundingBoxUpdateRequired)
        {
            UpdateSplineBoundingBox();
            isBoundingBoxUpdateRequired = false;
        }
    }

    private readonly List<Vector3> splineSamplePoints = [];
    private void UpdateSplineCurveVisualizer()
    {
        splineSamplePoints.Clear();
        // If the spline editor is active, we let it render the curve instead
        if (Component.Spline is not null && splineEditorService.ActiveSplineComponent != Component)
        {
            SplineExtensions.CollectSplineSamplePositionsByResolution(Component.Spline, splineSamplePoints, sampleResolutionPerCurve: 64);
        }

        var lineVisualizerComponent = GizmoRootEntity.Get<LineVisualizerComponent>();
        if (lineVisualizerComponent is null)
        {
            lineVisualizerComponent = new LineVisualizerComponent();
            lineVisualizerComponent.LineSet.OccludedStyle = LineOccludedStyle.Checkered;
            GizmoRootEntity.Add(lineVisualizerComponent);
        }
        else
        {
            lineVisualizerComponent.LineSet.Segments.Clear();
        }

        var splineSamplePointsSpan = CollectionsMarshal.AsSpan(splineSamplePoints);
        for (int i = 0; i < splineSamplePointsSpan.Length - 1; i++)
        {
            var lineStartPos = splineSamplePointsSpan[i];
            var lineNextPos = splineSamplePointsSpan[i + 1];

            var instData = new LineSegment
            {
                StartPosition = lineStartPos,
                EndPosition = lineNextPos,
                StartColor = CurveColor,
                EndColor = CurveColor,
                LineThicknessPx = 2
            };
            lineVisualizerComponent.LineSet.Segments.Add(instData);
        }
    }

    private void UpdateSplineBoundingBox()
    {
        if (boundingBoxEntity is null)
        {
            // Add bounding box entity
            var boundingBoxMesh = BoundingBoxMesh.CreateMesh(GraphicsDevice);
            boundingBoxMesh.DisposeBy(this);
            var boundingBoxMeshDraw = boundingBoxMesh.ToMeshDraw();
            boundingBoxModelComponent = new ModelComponent
            {
                Enabled = true,
                Model = new Model { boundingBoxMaterial, new Mesh { Draw = boundingBoxMeshDraw } },
                RenderGroup = RenderGroup,
                IsShadowCaster = false,
            };
            boundingBoxEntity = new Entity("SplineBoundingBox")
            {
                boundingBoxModelComponent
            };
            boundingBoxEntity.SetParent(GizmoRootEntity);
        }

        bool hasBoundingBox = Component.Spline?.CurveCount > 0 && Component.SplineEvaluator is not null;
        boundingBoxModelComponent.Enabled = hasBoundingBox;
        if (hasBoundingBox)
        {
            var boundingBox = Component.SplineEvaluator.CalculateBoundingBox();
            var boxLengths = boundingBox.Maximum - boundingBox.Minimum;
            boundingBoxEntity.Transform.Scale = boxLengths;
            boundingBoxEntity.Transform.Position = boundingBox.Center;
        }
    }
}
