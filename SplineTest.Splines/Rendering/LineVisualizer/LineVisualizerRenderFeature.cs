// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using SplineTest.Rendering.LineMesh;
using Stride.Core;
using Stride.Core.Mathematics;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Rendering.Materials;
using System.Runtime.InteropServices;
using Buffer = Stride.Graphics.Buffer;

namespace SplineTest.Splines.Rendering.LineVisualizer;

public class LineVisualizerRenderFeature : RootRenderFeature
{
    private DynamicEffectInstance lineVisualizerEffect;
    private LineMeshData lineMeshData;
    private Buffer vertexBuffer;
    private Buffer indexBuffer;
    private MutablePipelineState pipelineState;

    /// <summary>
    /// Global line width scaling setting.
    /// </summary>
    public float LineThicknessScale { get; set; } = 1f;

    /// <summary>
    /// Pixel size of the checkered look for occluded lines.
    /// </summary>
    public int OccludedStyleCheckerSize { get; set; } = 2;

    public override Type SupportedRenderObjectType => typeof(RenderLineSet);

    public LineVisualizerRenderFeature()
    {
        SortKey = byte.MaxValue;
    }

    protected override void InitializeCore()
    {
        var graphicsDevice = Context.GraphicsDevice;

        lineVisualizerEffect = new DynamicEffectInstance("StrideLineVisualizerEffect");
        lineVisualizerEffect.Initialize(Context.Services);
        lineVisualizerEffect.UpdateEffect(graphicsDevice);

        lineMeshData = LineMeshData.Generate();
        vertexBuffer = Buffer.Vertex.New(graphicsDevice, lineMeshData.Vertices, GraphicsResourceUsage.Default).DisposeBy(this);
        indexBuffer = Buffer.Index.New(graphicsDevice, lineMeshData.VertexIndices, GraphicsResourceUsage.Default).DisposeBy(this);

        pipelineState = new MutablePipelineState(graphicsDevice);
        pipelineState.State.PrimitiveType = LineMeshData.PrimitiveType;
        pipelineState.State.RootSignature = lineVisualizerEffect.RootSignature;
        pipelineState.State.EffectBytecode = lineVisualizerEffect.Effect.Bytecode;
        pipelineState.State.InputElements = LineVertex.Layout.CreateInputElements();
        //pipelineState.State.SetDefaults();
        //pipelineState.State.BlendState = BlendStates.AlphaBlend;
        //pipelineState.State.RasterizerState.CullMode = CullMode.None;
    }

    protected override void OnRemoveRenderObject(RenderObject renderObject)
    {
        if (renderObject is RenderLineSet renderLineSet)
        {
            renderLineSet.LineInstanceDataBuffer?.Dispose();
            renderLineSet.LineInstanceDataBuffer = null;
        }
    }

    public override void Prepare(RenderDrawContext context)
    {
        // Update the bufffers
        foreach (var renderNode in RenderNodes)
        {
            if (renderNode.RenderObject is not RenderLineSet renderLineSet)
            {
                continue;
            }
            if (!renderLineSet.IsBufferDataUpdateRequired)
            {
                continue;
            }
            var instDataList = renderLineSet.LineInstanceDataList;
            if (renderLineSet.LineInstanceDataBuffer is null || renderLineSet.LineInstanceDataBuffer.ElementCount < instDataList.Count)
            {
                // Create buffer or recreate to fit new draw size
                renderLineSet.LineInstanceDataBuffer?.Dispose();
                if (instDataList.Count > 0)
                {
                    renderLineSet.LineInstanceDataBuffer = CreateShaderBuffer<LineInstanceData>(context.GraphicsDevice, instDataList.Count);
                }
            }
            if (instDataList.Count > 0)
            {
                var instDataListSpan = CollectionsMarshal.AsSpan(instDataList);
                renderLineSet.LineInstanceDataBuffer.SetData(context.CommandList, instDataListSpan);
            }
            renderLineSet.IsBufferDataUpdateRequired = false;
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
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.LineThicknessScale, LineThicknessScale);
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.OccludedStyleCheckerSize, (uint)OccludedStyleCheckerSize);
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.ViewProjection, renderView.ViewProjection);
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.ViewProjectionInverse, Matrix.Invert(renderView.ViewProjection));
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.ViewSize, renderView.ViewSize);
        Matrix.Invert(ref renderView.View, out var viewInverse);
        var eyeVec = new Vector3(viewInverse.M41, viewInverse.M42, viewInverse.M43);
        lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.Eye, eyeVec);

        lineVisualizerEffect.UpdateEffect(context.GraphicsDevice);

        var commandList = context.CommandList;
        for (int index = startIndex; index < endIndex; index++)
        {
            var renderNodeReference = renderViewStage.SortedRenderNodes[index].RenderNode;
            var renderLineSet = (RenderLineSet)GetRenderNode(renderNodeReference).RenderObject;

            int instanceCount = renderLineSet.LineInstanceDataList.Count;
            if (instanceCount == 0)
            {
                continue;
            }

            bool depthTest = renderLineSet.DepthTest;
            bool hasTransparency = renderLineSet.HasTransparency;
            for (int i = 0; i < 2; i++)
            {
                bool isOccludedPass = i > 0;
                if (isOccludedPass && !renderLineSet.RenderOccludedPass)
                {
                    break;
                }
                ConfigurePipeline(commandList, depthTest, hasTransparency, isOccludedPass);

                commandList.SetVertexBuffer(0, vertexBuffer, 0, LineVertex.Layout.VertexStride);
                commandList.SetIndexBuffer(indexBuffer, 0, is32bits: false);
                commandList.SetPipelineState(pipelineState.CurrentState);

                lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.LineInstanceArray, renderLineSet.LineInstanceDataBuffer);
                lineVisualizerEffect.Parameters.Set(LineVisualizerShaderKeys.PassIndex, (uint)i);

                lineVisualizerEffect.Apply(context.GraphicsContext);

                int indexCountPerInstance = lineMeshData.VertexIndices.Length;
                commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount);
            }
        }
    }

    private void ConfigurePipeline(CommandList commandList, bool depthTest, bool hasTransparency, bool isOccludedPass)
    {
        pipelineState.State.SetDefaults();
        pipelineState.State.BlendState = (hasTransparency || isOccludedPass) ? BlendStates.NonPremultiplied : BlendStates.Opaque;
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
public struct LineInstanceData
{
    public uint LineModeAndStyles;
    public Vector3 LinePositionA;
    public Vector3 LinePositionB;
    public Color4 LineColorA;
    public Color4 LineColorB;
    public float LineThicknessPx;
    public float FixedLengthPx;
}
