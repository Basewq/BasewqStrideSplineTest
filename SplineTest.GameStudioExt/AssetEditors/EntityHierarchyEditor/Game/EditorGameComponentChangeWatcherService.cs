// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using Stride.Assets.Entities;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Services;
using Stride.Core.Annotations;
using Stride.Core.Assets.Quantum;
using Stride.Core.Quantum;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
using Stride.Engine.Design;

namespace Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;

// Entity Component type-casted variant of Stride's EditorGameComponentChangeWatcherService
public abstract class EditorGameComponentChangeWatcherService<TEntityComponent> : EditorGameServiceBase
    where TEntityComponent : EntityComponent
{
    private readonly Dictionary<TEntityComponent, GraphNodeChangeListener> registeredListeners = new Dictionary<TEntityComponent, GraphNodeChangeListener>();
    private readonly IEditorGameController controller;

    protected EditorGameComponentChangeWatcherService(IEditorGameController controller)
    {
        this.controller = controller;
    }

    [NotNull]
    public Type ComponentType => typeof(TEntityComponent);

    /// <inheritdoc />
    public override ValueTask DisposeAsync()
    {
        EnsureNotDestroyed(nameof(EditorGameComponentChangeWatcherService<>));
        foreach (var component in registeredListeners.Keys.ToList())
        {
            UnregisterComponent(component);
        }
        return base.DisposeAsync();
    }

    protected override Task<bool> Initialize(EditorServiceGame game)
    {
        game.SceneSystem.SceneInstance.EntityAdded += EntityAdded;
        game.SceneSystem.SceneInstance.EntityRemoved += EntityRemoved;
        game.SceneSystem.SceneInstance.ComponentChanged += ComponentChanged;
        return Task.FromResult(true);
    }

    protected virtual void ComponentPropertyChanged(TEntityComponent component, INodeChangeEventArgs e)
    {
        // Do nothing by default.
    }

    private void RegisterComponent(EntityComponent component)
    {
        if (component is TEntityComponent castedComponent
            && !registeredListeners.ContainsKey(castedComponent))
        {
            var rootNode = controller.GameSideNodeContainer.GetOrCreateNode(component);
            var listener = new AssetGraphNodeChangeListener(rootNode, AssetQuantumRegistry.GetDefinition(typeof(EntityHierarchyAssetBase)));
            listener.Initialize();
            listener.ValueChanged += (sender, e) => ComponentPropertyChanged(castedComponent, e);
            listener.ItemChanged += (sender, e) => ComponentPropertyChanged(castedComponent, e);
            registeredListeners.Add(castedComponent, listener);
        }
    }

    private void UnregisterComponent(EntityComponent component)
    {
        if (component is TEntityComponent castedComponent
            && registeredListeners.Remove(castedComponent, out var listener))
        {
            //listener.ValueChanged -= ComponentPropertyChanged;
            //listener.ItemChanged -= ComponentPropertyChanged;
            listener.Dispose();
        }
    }

    private void EntityAdded(object sender, Entity e)
    {
        foreach (var component in e.Components)
        {
            RegisterComponent(component);
        }
    }

    private void EntityRemoved(object sender, Entity e)
    {
        foreach (var component in e.Components)
        {
            UnregisterComponent(component);
        }
    }

    private void ComponentChanged(object sender, EntityComponentEventArgs e)
    {
        UnregisterComponent(e.PreviousComponent);
        RegisterComponent(e.NewComponent);
    }
}