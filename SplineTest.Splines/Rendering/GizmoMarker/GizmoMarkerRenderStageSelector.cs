// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Rendering;
using System.ComponentModel;

namespace SplineTest.Splines.Rendering.GizmoMarker;

public class GizmoMarkerRenderStageSelector : RenderStageSelector
{
    [DefaultValue(RenderGroupMask.All)]
    public RenderGroupMask RenderGroup { get; set; } = RenderGroupMask.All;

    [DefaultValue(null)]
    public RenderStage TransparentRenderStage { get; set; }

    public string EffectName { get; set; }

    public override void Process(RenderObject renderObject)
    {
        if (((RenderGroupMask)(1U << (int)renderObject.RenderGroup) & RenderGroup) != 0)
        {
            var renderStage = TransparentRenderStage;
            if (renderStage is not null)
            {
                renderObject.ActiveRenderStages[renderStage.Index] = new ActiveRenderStage(EffectName);
            }
        }
    }
}
