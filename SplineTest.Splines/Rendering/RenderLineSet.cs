// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Splines.Rendering.LineVisualizer;
using Stride.Graphics;
using Stride.Rendering;

namespace SplineTest.Rendering;

public class RenderLineSet : RenderObject
{
    public bool DepthTest;
    public bool HasTransparency;

    public List<LineInstanceData> LineInstanceDataList;
    internal bool IsLineInstanceListUpdateRequired;

    public Buffer<LineInstanceData> LineInstanceDataBuffer;
    internal bool IsBufferDataUpdateRequired;
}
