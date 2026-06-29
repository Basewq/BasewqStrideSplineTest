// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Rendering;
using Stride.Rendering;

namespace SplineTest.Splines.Rendering.LineVisualizer;

public class LineVisualizerRenderStageSelector : TransparentRenderStageSelector
{
    public override void Process(RenderObject renderObject)
    {
        if (((RenderGroupMask)(1U << (int)renderObject.RenderGroup) & RenderGroup) != 0)
        {
            var debugObject = (RenderLineSet)renderObject;
            var renderStage = debugObject.HasTransparency ? TransparentRenderStage : OpaqueRenderStage;
            if (renderStage is not null)
            {
                renderObject.ActiveRenderStages[renderStage.Index] = new ActiveRenderStage(EffectName);
            }
        }
    }
}
