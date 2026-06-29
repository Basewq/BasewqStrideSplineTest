// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Splines.Rendering.LineVisualizer;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NotNull = Stride.Core.Annotations.NotNullAttribute;

namespace SplineTest.Rendering;

class LineVisualizerRenderProcessor : EntityProcessor<LineVisualizerComponent, LineVisualizerRenderProcessor.AssociatedData>, IEntityComponentRenderProcessor
{
    private IGraphicsDeviceService graphicsDeviceService;

    private readonly List<RenderLineSet> renderLineSets = [];

    public VisibilityGroup VisibilityGroup { get; set; } = default!;

    public LineVisualizerRenderProcessor()
    {
    }

    protected override void OnSystemAdd()
    {
        graphicsDeviceService = Services.GetSafeServiceAs<IGraphicsDeviceService>();
    }

    protected override void OnSystemRemove()
    {
        foreach (var renderLineSet in renderLineSets)
        {
            VisibilityGroup.RenderObjects.Remove(renderLineSet);
        }
    }

    protected override AssociatedData GenerateComponentData([NotNull] Entity entity, [NotNull] LineVisualizerComponent component)
    {
        return new AssociatedData
        {

        };
    }

    protected override bool IsAssociatedDataValid([NotNull] Entity entity, [NotNull] LineVisualizerComponent component, [NotNull] AssociatedData data)
    {
        return true;
    }

    protected override void OnEntityComponentRemoved(Entity entity, [NotNull] LineVisualizerComponent component, [NotNull] AssociatedData data)
    {
        data.RenderLineSet?.IsLineInstanceListUpdateRequired = true;
    }

    public override void Draw(RenderContext context)
    {
        // Note that we do not bother with bounds checking/culling.
        // In practice, we should not expect too many lines and using instancing/buffers should be fast enough
        foreach (var (component, data) in ComponentDatas)
        {
            var lineSet = component.LineSet;

            bool hasLineSetObjectChanged = lineSet is null || data.PrevLineSetVersion != lineSet.Version;

            var renderGroup = component.RenderGroup;
            bool depthTest = component.DepthTest;
            bool hasTransparency = hasLineSetObjectChanged
                ? (lineSet?.HasTransparency() ?? false)
                : data.PrevHasTransparency;

            bool hasRenderTypeChanged = renderGroup != data.PrevRenderGroup
                || depthTest != data.PrevDepthTest
                || hasTransparency != data.PrevHasTransparency;
            if (hasRenderTypeChanged)
            {
                data.PrevRenderGroup = renderGroup;
                data.PrevDepthTest = depthTest;
                data.PrevHasTransparency = hasTransparency;
                // Invalidate the old one
                data.RenderLineSet?.IsLineInstanceListUpdateRequired = true;
            }

            if (lineSet is null || lineSet.Segments.Count == 0)
            {
                continue;
            }

            var renderLineSet = GetOrCreateRenderLineSet(renderLineSets, renderGroup, depthTest, hasTransparency);
            data.RenderLineSet = renderLineSet;

            var worldMatrix = component.UseEntityWorldTransform ? component.Entity.Transform.WorldMatrix : Matrix.Identity;
            bool hasLinesChanged = renderLineSet.IsLineInstanceListUpdateRequired
                || lineSet.Version != data.PrevLineSetVersion
                || worldMatrix != data.PrevWorldMatrix;
            if (hasLinesChanged)
            {
                data.PrevLineSetVersion = lineSet.Version;
                data.PrevWorldMatrix = worldMatrix;
                renderLineSet.IsLineInstanceListUpdateRequired = true;
            }
        }

        // Rebuild lines
        foreach (var renderLineSet in renderLineSets)
        {
            if (renderLineSet.IsLineInstanceListUpdateRequired)
            {
                renderLineSet.LineInstanceDataList.Clear();
                renderLineSet.IsBufferDataUpdateRequired = true;
            }
        }
        var graphicsDevice = graphicsDeviceService.GraphicsDevice;
        var colorSpace = graphicsDevice.ColorSpace;
        foreach (var (component, data) in ComponentDatas)
        {
            var lineSet = component.LineSet;
            var renderLineSet = data.RenderLineSet;
            if (lineSet is null
                || renderLineSet is null || !renderLineSet.IsLineInstanceListUpdateRequired)
            {
                continue;
            }

            var lineSegments = lineSet.Segments;
            var worldMatrix = data.PrevWorldMatrix;     // Still 'current' at this point
            for (int i = 0; i < lineSegments.Count; i++)
            {
                var segment = lineSegments[i];
                var linePosA = Vector3.Transform(segment.StartPosition, worldMatrix).XYZ();
                var linePosB = Vector3.Transform(segment.EndPosition, worldMatrix).XYZ();
                var lineInstData = new LineInstanceData
                {
                    LineMode = (uint)segment.LineMode,
                    LinePositionA = linePosA,
                    LinePositionB = linePosB,
                    LineColorA = segment.StartColor.ToColorSpace(colorSpace),
                    LineColorB = segment.EndColor.ToColorSpace(colorSpace),
                    LineThicknessPx = segment.LineThicknessPx,
                    FixedLengthPx = segment.FixedLengthPx,
                };
                renderLineSet.LineInstanceDataList.Add(lineInstData);
            }
        }

        foreach (var renderLineSet in renderLineSets)
        {
            renderLineSet.IsLineInstanceListUpdateRequired = false;
            // Update which render objects are visible
            if (!renderLineSet.Enabled || renderLineSet.LineInstanceDataList.Count == 0)
            {
                VisibilityGroup.RenderObjects.Remove(renderLineSet);
            }
            else
            {
                VisibilityGroup.RenderObjects.Add(renderLineSet);
            }
        }
    }

    private static bool TryGetRenderLineSet(
        List<RenderLineSet> renderLineSetList, RenderGroup renderGroup, bool depthTest, bool hasTransparency,
        [NotNullWhen(true)] out RenderLineSet? renderLineSet)
    {
        var renderLineSetListSpan = CollectionsMarshal.AsSpan(renderLineSetList);
        for (int i = 0; i < renderLineSetListSpan.Length; i++)
        {
            if (renderLineSetListSpan[i].RenderGroup == renderGroup
                && renderLineSetListSpan[i].DepthTest == depthTest
                && renderLineSetListSpan[i].HasTransparency == hasTransparency)
            {
                renderLineSet = renderLineSetListSpan[i];
                return true;
            }
        }
        renderLineSet = null;
        return false;
    }

    private static RenderLineSet GetOrCreateRenderLineSet(
        List<RenderLineSet> renderLineSetList, RenderGroup renderGroup, bool depthTest, bool hasTransparency)
    {
        if (!TryGetRenderLineSet(renderLineSetList, renderGroup, depthTest, hasTransparency, out var renderLineSet))
        {
            renderLineSet = new RenderLineSet
            {
                RenderGroup = renderGroup,
                DepthTest = depthTest,
                HasTransparency = hasTransparency,

                LineInstanceDataList = [],
                IsLineInstanceListUpdateRequired = true,
                IsBufferDataUpdateRequired = true,
            };
            renderLineSetList.Add(renderLineSet);
        }
        return renderLineSet;
    }

    public class AssociatedData
    {
        internal uint PrevLineSetVersion = uint.MaxValue;
        internal Matrix PrevWorldMatrix;

        internal RenderGroup PrevRenderGroup;
        internal bool PrevDepthTest;
        internal bool PrevHasTransparency;

        internal RenderLineSet RenderLineSet;
    }
}
