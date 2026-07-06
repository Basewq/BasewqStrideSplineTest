// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.Splines.Rendering.GizmoMarker;
using Stride.Graphics;
using Stride.Rendering;

namespace SplineTest.Rendering;

public class RenderGizmoMarkerSet : RenderObject
{
    public bool DepthTest;
    public bool RenderOccludedPass;

    public List<GizmoMarkerInstanceData> GizmoMarkerInstanceDataList;
    internal bool IsGizmoMarkerInstanceUpdateRequired;

    public Buffer<GizmoMarkerInstanceData> GizmoMarkerInstanceDataBuffer;
    internal bool IsBufferDataUpdateRequired;
}
