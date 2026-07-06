// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Splines.Rendering.GizmoMarker;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NotNull = Stride.Core.Annotations.NotNullAttribute;

namespace SplineTest.Rendering;

class GizmoMarkerSetRenderProcessor : EntityProcessor<GizmoMarkerSetComponent, GizmoMarkerSetRenderProcessor.AssociatedData>, IEntityComponentRenderProcessor
{
    private IGraphicsDeviceService graphicsDeviceService;

    private readonly List<RenderGizmoMarkerSet> renderGizmoMarkerSets = [];

    public VisibilityGroup VisibilityGroup { get; set; } = default!;

    public GizmoMarkerSetRenderProcessor()
    {
    }

    protected override void OnSystemAdd()
    {
        graphicsDeviceService = Services.GetSafeServiceAs<IGraphicsDeviceService>();
    }

    protected override void OnSystemRemove()
    {
        foreach (var renderGizmoMarkerSet in renderGizmoMarkerSets)
        {
            VisibilityGroup.RenderObjects.Remove(renderGizmoMarkerSet);
        }
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] GizmoMarkerSetComponent component)
    {
        return new AssociatedData
        {

        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] GizmoMarkerSetComponent component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] GizmoMarkerSetComponent component, [NotNull] AssociatedData data)
    {
        data.RenderGizmoMarkerSet?.IsGizmoMarkerInstanceUpdateRequired = true;
    }

    public override void Draw(RenderContext context)
    {
        // Note that we do not bother with bounds checking/culling.
        // In practice, we should not expect too many markers and using instancing/buffers should be fast enough
        foreach (var (component, data) in ComponentDatas)
        {
            var markerSet = component.GizmoMarkerSet;

            bool hasGizmoMarkerSetObjectChanged = markerSet is null || data.PrevGizmoMarkerSetVersion != markerSet.Version;

            var renderGroup = component.RenderGroup;
            bool depthTest = component.DepthTest;
            bool hasTransparency = markerSet?.HasTransparency ?? false;
            var occludedStyle = markerSet?.OccludedStyle ?? GizmoMarkerOccludedStyle.None;

            bool hasRenderTypeChanged = renderGroup != data.PrevRenderGroup
                || depthTest != data.PrevDepthTest
                || hasTransparency != data.PrevHasTransparency
                || occludedStyle != data.PrevOccludedStyle;
            if (hasGizmoMarkerSetObjectChanged || hasRenderTypeChanged)
            {
                data.PrevRenderGroup = renderGroup;
                data.PrevDepthTest = depthTest;
                data.PrevHasTransparency = hasTransparency;
                data.PrevOccludedStyle = occludedStyle;
                // Invalidate the old one
                data.RenderGizmoMarkerSet?.IsGizmoMarkerInstanceUpdateRequired = true;
            }

            if (markerSet is null || markerSet.Markers.Count == 0)
            {
                continue;
            }

            var renderGizmoMarkerSet = GetOrCreateRenderGizmoMarkerSet(renderGizmoMarkerSets, renderGroup, depthTest);
            data.RenderGizmoMarkerSet = renderGizmoMarkerSet;

            var worldMatrix = component.UseEntityWorldTransform ? component.Entity.Transform.WorldMatrix : Matrix.Identity;
            bool hasMarkersChanged = renderGizmoMarkerSet.IsGizmoMarkerInstanceUpdateRequired
                || markerSet.Version != data.PrevGizmoMarkerSetVersion
                || worldMatrix != data.PrevWorldMatrix;
            if (hasMarkersChanged)
            {
                data.PrevGizmoMarkerSetVersion = markerSet.Version;
                data.PrevWorldMatrix = worldMatrix;
                renderGizmoMarkerSet.IsGizmoMarkerInstanceUpdateRequired = true;
            }
        }

        // Rebuild markers
        foreach (var renderGizmoMarkerSet in renderGizmoMarkerSets)
        {
            if (renderGizmoMarkerSet.IsGizmoMarkerInstanceUpdateRequired)
            {
                renderGizmoMarkerSet.GizmoMarkerInstanceDataList.Clear();
                renderGizmoMarkerSet.IsBufferDataUpdateRequired = true;
            }
        }
        var graphicsDevice = graphicsDeviceService.GraphicsDevice;
        var colorSpace = graphicsDevice.ColorSpace;
        foreach (var (component, data) in ComponentDatas)
        {
            var markerSet = component.GizmoMarkerSet;
            var renderGizmoMarkerSet = data.RenderGizmoMarkerSet;
            if (markerSet is null
                || renderGizmoMarkerSet is null || !renderGizmoMarkerSet.IsGizmoMarkerInstanceUpdateRequired)
            {
                continue;
            }

            var markers = markerSet.Markers;
            var worldMatrix = data.PrevWorldMatrix;     // Still 'current' at this point
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                uint shapeAndModes = 0;
                shapeAndModes |= (uint)marker.Shape;
                shapeAndModes |= (uint)marker.OrientationMode << 8;
                shapeAndModes |= (uint)marker.ScaleMode << 16;
                shapeAndModes |= (uint)markerSet.OccludedStyle << 24;

                Matrix.Transformation(scaling: in Vector3.One, rotation: in marker.Rotation, translation: in marker.Position, out var markerWorldMatrix);
                markerWorldMatrix = markerWorldMatrix * worldMatrix;
                var lineInstData = new GizmoMarkerInstanceData
                {
                    ShapeAndModes = shapeAndModes,
                    World = markerWorldMatrix,
                    FillColor = marker.FillColor.ToColorSpace(colorSpace),
                    Axis = marker.Axis,
                    SizePx = marker.SizePx,
                    OutlineWidthPx = marker.OutlineWidthPx,
                    OutlineColor = marker.OutlineColor.ToColorSpace(colorSpace),
                    GlowWidthPx = marker.GlowWidthPx,
                    GlowColor = marker.GlowColor.ToColorSpace(colorSpace),
                };
                renderGizmoMarkerSet.GizmoMarkerInstanceDataList.Add(lineInstData);
            }

            if (markerSet.OccludedStyle != GizmoMarkerOccludedStyle.None)
            {
                renderGizmoMarkerSet.RenderOccludedPass = true;
            }
        }

        foreach (var renderGizmoMarkerSet in renderGizmoMarkerSets)
        {
            renderGizmoMarkerSet.IsGizmoMarkerInstanceUpdateRequired = false;
            // Update which render objects are visible
            if (!renderGizmoMarkerSet.Enabled || renderGizmoMarkerSet.GizmoMarkerInstanceDataList.Count == 0)
            {
                VisibilityGroup.RenderObjects.Remove(renderGizmoMarkerSet);
            }
            else
            {
                VisibilityGroup.RenderObjects.Add(renderGizmoMarkerSet);
            }
        }
    }

    private static bool TryGetRenderGizmoMarkerSet(
        List<RenderGizmoMarkerSet> renderGizmoMarkerSetList, RenderGroup renderGroup, bool depthTest,
        [NotNullWhen(true)] out RenderGizmoMarkerSet? renderGizmoMarkerSet)
    {
        var renderGizmoMarkerSetListSpan = CollectionsMarshal.AsSpan(renderGizmoMarkerSetList);
        for (int i = 0; i < renderGizmoMarkerSetListSpan.Length; i++)
        {
            if (renderGizmoMarkerSetListSpan[i].RenderGroup == renderGroup
                && renderGizmoMarkerSetListSpan[i].DepthTest == depthTest)
            {
                renderGizmoMarkerSet = renderGizmoMarkerSetListSpan[i];
                return true;
            }
        }
        renderGizmoMarkerSet = null;
        return false;
    }

    private static RenderGizmoMarkerSet GetOrCreateRenderGizmoMarkerSet(
        List<RenderGizmoMarkerSet> renderGizmoMarkerSetList, RenderGroup renderGroup, bool depthTest)
    {
        if (!TryGetRenderGizmoMarkerSet(renderGizmoMarkerSetList, renderGroup, depthTest, out var renderGizmoMarkerSet))
        {
            renderGizmoMarkerSet = new RenderGizmoMarkerSet
            {
                RenderGroup = renderGroup,
                DepthTest = depthTest,

                GizmoMarkerInstanceDataList = [],
                IsGizmoMarkerInstanceUpdateRequired = true,
                IsBufferDataUpdateRequired = true,
            };
            renderGizmoMarkerSetList.Add(renderGizmoMarkerSet);
        }
        return renderGizmoMarkerSet;
    }

    public class AssociatedData
    {
        internal uint PrevGizmoMarkerSetVersion = uint.MaxValue;
        internal Matrix PrevWorldMatrix;

        internal RenderGroup PrevRenderGroup;
        internal bool PrevDepthTest;
        internal bool PrevHasTransparency;
        internal GizmoMarkerOccludedStyle PrevOccludedStyle;

        internal RenderGizmoMarkerSet RenderGizmoMarkerSet;
    }
}
