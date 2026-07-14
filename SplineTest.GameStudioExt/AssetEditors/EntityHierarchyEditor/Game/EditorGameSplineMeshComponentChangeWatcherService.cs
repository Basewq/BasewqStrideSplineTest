// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Presentation.AssetEditors.GameEditor.Services;
using Stride.Core.Quantum;
using Stride.Engine.Splines.Components;

namespace Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;

public class EditorGameSplineMeshComponentChangeWatcherService : EditorGameComponentChangeWatcherService<SplineMeshComponent>
{
    public EditorGameSplineMeshComponentChangeWatcherService(IEditorGameController controller)
        : base(controller)
    {
    }

    protected override void ComponentPropertyChanged(SplineMeshComponent component, INodeChangeEventArgs e)
    {
        if (e.Node is not IMemberNode memberNode)
        {
            return;
        }

        // Might be overkill to invalidate on any change...
        component.InvalidateMesh();
    }
}