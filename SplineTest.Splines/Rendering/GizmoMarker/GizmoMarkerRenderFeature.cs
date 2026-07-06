// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using SplineTest.Rendering.GizmoMarkerMesh;
using SplineTest.Rendering.LineMesh;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using System.Runtime.InteropServices;
using Buffer = Stride.Graphics.Buffer;

namespace SplineTest.Splines.Rendering.GizmoMarker;

public class GizmoMarkerRenderFeature : RootRenderFeature
{
    private DynamicEffectInstance gizmoMarkerEffect;
    private GizmoMarkerMeshData gizmoMarkerMeshData;
    private Buffer vertexBuffer;
    private Buffer indexBuffer;
    private MutablePipelineState pipelineState;

    /// <summary>
    /// Global marker scaling setting.
    /// </summary>
    public float MarkerScale { get; set; } = 1f;

    /// <summary>
    /// Pixel size of the checkered look for occluded lines.
    /// </summary>
    public int OccludedStyleCheckerSize { get; set; } = 2;

    public override Type SupportedRenderObjectType => typeof(RenderGizmoMarkerSet);

    public GizmoMarkerRenderFeature()
    {
        SortKey = byte.MaxValue;
    }

    protected override void InitializeCore()
    {
        var graphicsDevice = Context.GraphicsDevice;

        gizmoMarkerEffect = new DynamicEffectInstance("StrideGizmoMarkerEffect");
        gizmoMarkerEffect.Initialize(Context.Services);
        gizmoMarkerEffect.UpdateEffect(graphicsDevice);

        gizmoMarkerMeshData = GizmoMarkerMeshData.Generate();
        vertexBuffer = Buffer.Vertex.New(graphicsDevice, gizmoMarkerMeshData.Vertices, GraphicsResourceUsage.Default).DisposeBy(this);
        indexBuffer = Buffer.Index.New(graphicsDevice, gizmoMarkerMeshData.VertexIndices, GraphicsResourceUsage.Default).DisposeBy(this);

        pipelineState = new MutablePipelineState(graphicsDevice);
        pipelineState.State.PrimitiveType = LineMeshData.PrimitiveType;
        pipelineState.State.RootSignature = gizmoMarkerEffect.RootSignature;
        pipelineState.State.EffectBytecode = gizmoMarkerEffect.Effect.Bytecode;
        pipelineState.State.InputElements = LineVertex.Layout.CreateInputElements();
        //pipelineState.State.SetDefaults();
        //pipelineState.State.BlendState = BlendStates.AlphaBlend;
        //pipelineState.State.RasterizerState.CullMode = CullMode.None;
    }

    protected override void OnRemoveRenderObject(RenderObject renderObject)
    {
        if (renderObject is RenderGizmoMarkerSet renderGizmoMarkerSet)
        {
            renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer?.Dispose();
            renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer = null;
        }
    }

    public override void Prepare(RenderDrawContext context)
    {
        // Update the bufffers
        foreach (var renderNode in RenderNodes)
        {
            if (renderNode.RenderObject is not RenderGizmoMarkerSet renderGizmoMarkerSet)
            {
                continue;
            }
            if (!renderGizmoMarkerSet.IsBufferDataUpdateRequired)
            {
                continue;
            }
            var instDataList = renderGizmoMarkerSet.GizmoMarkerInstanceDataList;
            if (renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer is null || renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer.ElementCount < instDataList.Count)
            {
                // Create buffer or recreate to fit new draw size
                renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer?.Dispose();
                if (instDataList.Count > 0)
                {
                    renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer = CreateShaderBuffer<GizmoMarkerInstanceData>(context.GraphicsDevice, instDataList.Count);
                }
            }
            if (instDataList.Count > 0)
            {
                var instDataListSpan = CollectionsMarshal.AsSpan(instDataList);
                renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer.SetData(context.CommandList, instDataListSpan);
            }
            renderGizmoMarkerSet.IsBufferDataUpdateRequired = false;
        }
    }

    private static Buffer<TData> CreateShaderBuffer<TData>(GraphicsDevice graphicsDevice, int elementCount)
     where TData : unmanaged
    {
        return Buffer.New<TData>(graphicsDevice, elementCount, BufferFlags.ShaderResource | BufferFlags.StructuredBuffer, GraphicsResourceUsage.Dynamic);
    }

    public override void Draw(RenderDrawContext context, RenderView renderView, RenderViewStage renderViewStage, int startIndex, int endIndex)
    {
        // Set common effect parameters
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.MarkerScale, MarkerScale);
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.OccludedStyleCheckerSize, (uint)OccludedStyleCheckerSize);
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.View, renderView.View);
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.ViewInverse, Matrix.Invert(renderView.View));
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.Projection, renderView.Projection);
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.ViewSize, renderView.ViewSize);
        var camera = Context.GetCurrentCamera();
        gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.PerspectiveCamera, camera.Projection == Stride.Engine.Processors.CameraProjectionMode.Perspective ? 1 : 0);

        gizmoMarkerEffect.UpdateEffect(context.GraphicsDevice);

        var commandList = context.CommandList;
        for (int index = startIndex; index < endIndex; index++)
        {
            var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
            var renderGizmoMarkerSet = (RenderGizmoMarkerSet)GetRenderNode(renderNodeReference).RenderObject;

            int instanceCount = renderGizmoMarkerSet.GizmoMarkerInstanceDataList.Count;
            if (instanceCount == 0)
            {
                continue;
            }

            bool depthTest = renderGizmoMarkerSet.DepthTest;
            for (int i = 0; i < 2; i++)
            {
                bool isOccludedPass = i > 0;
                if (isOccludedPass && !renderGizmoMarkerSet.RenderOccludedPass)
                {
                    break;
                }
                ConfigurePipeline(commandList, depthTest, isOccludedPass);

                commandList.SetVertexBuffer(0, vertexBuffer, 0, LineVertex.Layout.VertexStride);
                commandList.SetIndexBuffer(indexBuffer, 0, is32bits: false);
                commandList.SetPipelineState(pipelineState.CurrentState);

                gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.GizmoMarkerInstanceArray, renderGizmoMarkerSet.GizmoMarkerInstanceDataBuffer);
                gizmoMarkerEffect.Parameters.Set(GizmoMarkerShaderKeys.PassIndex, (uint)i);

                gizmoMarkerEffect.Apply(context.GraphicsContext);

                int indexCountPerInstance = gizmoMarkerMeshData.VertexIndices.Length;
                commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount);
            }
        }
    }

    private void ConfigurePipeline(CommandList commandList, bool depthTest, bool isOccludedPass)
    {
        pipelineState.State.SetDefaults();
        pipelineState.State.BlendState = BlendStates.NonPremultiplied;    // Always has transparency
        pipelineState.State.RasterizerState.FillMode = FillMode.Solid;
        pipelineState.State.RasterizerState.CullMode = CullMode.None;
        if (isOccludedPass)
        {
            pipelineState.State.DepthStencilState = DepthStencilStates.DepthRead with { DepthBufferFunction = CompareFunction.GreaterEqual };
        }
        else if (depthTest)
        {
            pipelineState.State.DepthStencilState = DepthStencilStates.DepthRead;
        }
        else
        {
            pipelineState.State.DepthStencilState = DepthStencilStates.None;
        }
        pipelineState.State.Output.CaptureState(commandList);
        pipelineState.Update();
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct GizmoMarkerInstanceData
{
    public uint ShapeAndModes;
    public Matrix World;
    public Color4 FillColor;
    public Vector3 Axis;
    public Vector2 SizePx;
    public float OutlineWidthPx;
    public Color4 OutlineColor;
    public float GlowWidthPx;
    public Color4 GlowColor;
}
