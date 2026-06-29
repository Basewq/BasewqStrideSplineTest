// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Core;
using Stride.Engine.Design;
using Stride.Rendering;

namespace SplineTest.Rendering;

[DefaultEntityComponentRenderer(typeof(LineVisualizerRenderProcessor))]
[AllowMultipleComponents]
[Display(Browsable = false)]    // Only allow creating at run-time
[DataContractIgnore]
public class LineVisualizerComponent : ActivableEntityComponent
{
    public LineSet LineSet { get; set; } = new();

    /// <summary>
    /// True if the lines's position is dependant on this component's entity's position.
    /// </summary>
    public bool UseEntityWorldTransform { get; set; } = true;

    public RenderGroup RenderGroup { get; set; } = RenderGroup.Group31;
    public bool DepthTest { get; set; } = true;
}
