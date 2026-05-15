using Stride.Assets;
using Stride.Assets.Entities;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Services;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.ViewModels;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Views;
using Stride.Assets.Presentation.ViewModel;
using Stride.Core.Assets;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Assets.Quantum;
using Stride.Core.IO;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.ViewModels;
using Stride.Core.Quantum;
using Stride.Core.Serialization;
using Stride.Editor.EditorGame.ContentLoader;
using Stride.Engine;
using Stride.GameStudio.View;
using Stride.GameStudio.ViewModels;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace SplineTest.GameStudioExt.StrideEditorExt;

public class StrideEditorService : IStrideEditorService
{
    private EditorViewModels? _editorViewModels = null;

    public bool IsActive
    {
        get
        {
            var gsVm = GameStudioViewModel.GameStudio;
            bool isValid = IsValid(gsVm);
            return isValid;
        }
    }

    public IAssetFinder? AssetFinder
    {
        get
        {
            var returnValue = InvokeOnUI(() =>
            {
                var assetFinder = _editorViewModels?.SessionViewModel as IAssetFinder;
                return assetFinder;
            });
            return returnValue;
        }
    }

    public IViewModelServiceProvider? ViewModelServiceProvider
    {
        get
        {
            var returnValue = InvokeOnUI(() =>
            {
                var serviceProvider = _editorViewModels?.SessionViewModel?.ServiceProvider;
                return serviceProvider;
            });
            return returnValue;
        }
    }

    public void Invoke(Action action)
    {
        var gsVm = GameStudioViewModel.GameStudio;
        if (!IsValid(gsVm))
        {
            return;
        }
        gsVm.StrideAssets.Dispatcher.Invoke(() =>
        {
            _editorViewModels = EditorViewModels.Create();
            action();
            _editorViewModels = null;
        });
    }

    public void Invoke(Action<SessionViewModel> action)
    {
        var gsVm = GameStudioViewModel.GameStudio;
        if (!IsValid(gsVm))
        {
            return;
        }
        gsVm.StrideAssets.Dispatcher.Invoke(() =>
        {
            _editorViewModels = EditorViewModels.Create();
            action(_editorViewModels?.SessionViewModel!);
            _editorViewModels = null;
        });
    }

    public Task InvokeAsync(Func<Task> actionAsync)
    {
        var gsVm = GameStudioViewModel.GameStudio;
        if (!IsValid(gsVm))
        {
            return Task.CompletedTask;
        }
        var task = gsVm.StrideAssets.Dispatcher.InvokeTask(async () =>
        {
            _editorViewModels = EditorViewModels.Create();
            await actionAsync();
            _editorViewModels = null;
        });
        return task;
    }

    public Task InvokeAsync(Func<SessionViewModel, Task> actionAsync)
    {
        var gsVm = GameStudioViewModel.GameStudio;
        if (!IsValid(gsVm))
        {
            return Task.CompletedTask;
        }
        var task = gsVm.StrideAssets.Dispatcher.InvokeTask(async () =>
        {
            _editorViewModels = EditorViewModels.Create();
            await actionAsync(_editorViewModels?.SessionViewModel!);
            _editorViewModels = null;
        });
        return task;
    }

    public Task GameThreadInvokeAsync(Func<Task> actionAsync)
    {
        var sceneEditorController = InvokeOnUI(() =>
        {
            EnsureInsideInvokeCall(_editorViewModels);
            var sceneEditorController = GetSceneEditorController(_editorViewModels.Value.SceneEditorViewModel);
            return sceneEditorController;
        });
        var task = sceneEditorController?.InvokeTask(actionAsync);
        return task ?? Task.CompletedTask;
    }

    public void AddPrefabToScene(UrlReference<Prefab> prefabRef, Vector3 position, Quaternion? rotation, Vector3? scale, Entity? parent, List<Guid>? outputEntityIds)
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneVm = _editorViewModels.Value.SceneViewModel;
        ArgumentNullException.ThrowIfNull(sceneVm);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var package = sceneVm.AssetItem.Package;
        var prefabAssetItem = package.Assets.FirstOrDefault(x => string.Equals(x.Location.FullPath, prefabRef.Url));
        var prefabAsset = prefabAssetItem?.Asset as PrefabAsset;
        var prefabVmRaw = sceneEditorVm.Session.GetAssetById(prefabAsset?.Id ?? default);
        var prefabVm = (PrefabViewModel)prefabVmRaw;
        const AddChildModifiers AddChildMod = AddChildModifiers.Alt;    // Create without a container entity

        // SceneRootViewModel/EntityViewModel has an internal method called AddEntitiesFromAssets which will be used to add a prefab into the scene
        var addEntitiesFromAssets_MethodInfo = typeof(EntityHierarchyItemViewModel).GetMethod("AddEntitiesFromAssets", BindingFlags.NonPublic | BindingFlags.Instance);
        NullCheck(addEntitiesFromAssets_MethodInfo, "EntityHierarchyItemViewModel.AddEntitiesFromAssets method not found.");

        EntityHierarchyItemViewModel entityParentViewModel;
        int entityInsertIndex;
        if (parent is not null)
        {
            entityParentViewModel = FindEntityViewModel(sceneEditorVm, parent.Id);
            entityInsertIndex = entityParentViewModel.EntityHierarchy.GetChildCount(parent);
        }
        else
        {
            // Add to the root scene
            entityParentViewModel = sceneEditorVm.HierarchyRoot;
            entityInsertIndex = sceneEditorVm.HierarchyRoot.EntityHierarchy.Hierarchy.RootParts.Count;
        }

        // Store the original rotation/scale, then update the prefab with new rotation/scale
        List<(Quaternion Rotation, Vector3 Scale)> rootOriginalRotScale = new();
        var prefabRootEntities = prefabVm.Asset.Hierarchy.RootParts;
        try
        {
            foreach (var prefabEnt in prefabRootEntities)
            {
                var transfComp = prefabEnt.Transform;
                rootOriginalRotScale.Add((transfComp.Rotation, transfComp.Scale));
                transfComp.Rotation *= rotation ?? Quaternion.Identity;
                transfComp.Scale += scale ?? Vector3.Zero;
            }
            var param_Assets = new[] { prefabVm };
            int param_Index = entityInsertIndex;
            var param_Modifiers = AddChildMod;
            var param_RootPosition = position;
            var methodParams = new object[] { param_Assets, param_Index, param_Modifiers, param_RootPosition };
            var methodReturnValue = addEntitiesFromAssets_MethodInfo.Invoke(entityParentViewModel, methodParams);
            if (methodReturnValue is IReadOnlyCollection<EntityViewModel> entitiesViewModels)
            {
                foreach (var evm in entitiesViewModels)
                {
                    bool isRotationChanged = rotation.HasValue;
                    bool isScaleChanged = scale.HasValue;
                    if (isRotationChanged || isScaleChanged)
                    {
                        MarkEntityPropertyAsOverridden(sceneEditorVm, evm, isRotationChanged, isScaleChanged);
                    }

                    outputEntityIds?.Add(evm.AssetSideEntity.Id);
                }
            }
        }
        finally
        {
            // Restore original rotation/scale on the original prefab
            for (int i = 0; i < prefabRootEntities.Count; i++)
            {
                var prefabEnt = prefabRootEntities[i];
                var transfComp = prefabEnt.Transform;
                transfComp.Rotation = rootOriginalRotScale[i].Rotation;
                transfComp.Scale = rootOriginalRotScale[i].Scale;
            }
        }

        static void MarkEntityPropertyAsOverridden(
            SceneEditorViewModel sceneEditorVm, EntityViewModel assetEntityViewModel,
            bool isRotationChanged, bool isScaleChanged
            )
        {
            var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
            var assetSideSceneEntityComp = assetSideSceneEntity.Transform;

            var entityCompNode = sceneEditorVm.Session.AssetNodeContainer.GetNode(assetSideSceneEntityComp);
            if (isRotationChanged)
            {
                var entityCompPropertyNodeRaw = entityCompNode[nameof(TransformComponent.Rotation)];
                var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
                NullCheck(entityCompPropertyNode, $"Property '{nameof(TransformComponent.Rotation)}' was not found or invalid type.");
                if (entityCompPropertyNode.BaseNode is not null)
                {
                    entityCompPropertyNode.OverrideContent(true);
                }
            }
            if (isScaleChanged)
            {
                var entityCompPropertyNodeRaw = entityCompNode[nameof(TransformComponent.Scale)];
                var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
                NullCheck(entityCompPropertyNode, $"Property '{nameof(TransformComponent.Scale)}' was not found or invalid type.");
                if (entityCompPropertyNode.BaseNode is not null)
                {
                    entityCompPropertyNode.OverrideContent(true);
                }
            }
        }
    }

    Guid IStrideEditorService.CreateEmptyEntity(Vector3 position, Quaternion? rotation, Vector3? scale, Entity? parent, string entityName)
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        EntityHierarchyItemViewModel entityParentViewModel;
        int entityInsertIndex;
        if (parent is not null)
        {
            entityParentViewModel = FindEntityViewModel(sceneEditorVm, parent.Id);
            entityInsertIndex = entityParentViewModel.EntityHierarchy.GetChildCount(parent);
        }
        else
        {
            //var sceneRootVm = sceneEditorVm.RootPart as SceneRootViewModel;
            //Debug.Assert(sceneRootVm is not null);
            // Add to the root scene
            entityParentViewModel = sceneEditorVm.HierarchyRoot;
            entityInsertIndex = sceneEditorVm.HierarchyRoot.EntityHierarchy.Hierarchy.RootParts.Count;
        }

        var assetSideEntity = new Entity(entityName, position);
        assetSideEntity.Transform.Rotation = rotation ?? assetSideEntity.Transform.Rotation;
        assetSideEntity.Transform.Scale = scale ?? assetSideEntity.Transform.Scale;
        // Create item ids collections for new entity before actually adding them to the asset.
        AssetCollectionItemIdHelper.GenerateMissingItemIds(assetSideEntity);

        var entityDesign = new EntityDesign(assetSideEntity, (entityParentViewModel as EntityFolderViewModel)?.Path ?? "");
        var collection = new AssetPartCollection<EntityDesign, Entity> { entityDesign };
        //parentEntityViewModel.EntityHierarchy.GetChildCount(parent);
        entityParentViewModel.Asset.AssetHierarchyPropertyGraph.AddPartToAsset(
            collection,
            entityDesign,
            (entityParentViewModel.Owner as EntityViewModel)?.AssetSideEntity,
            entityInsertIndex);

        return assetSideEntity.Id;
    }

    public void DeleteEntity(Entity entity)
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entity.Id);
        //var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.

        HashSet<Tuple<Guid, Guid>> mapping;
        bool wasFound = assetEntityViewModel.Asset.Asset.Hierarchy.Parts.TryGetValue(entity.Id, out EntityDesign? entityDesign);
        if (!wasFound)
        {
            throw new InvalidOperationException($"EntityDesign for Entity.Id {entity.Id} not found.");
        }
        var entityDesigns = new[] { entityDesign };
        assetEntityViewModel.Asset.AssetHierarchyPropertyGraph.DeleteParts(entityDesigns, out mapping);
        if (sceneEditorVm?.UndoRedoService.TransactionInProgress ?? false)
        {
            var operation = new DeletedPartsTrackingOperation<EntityDesign, Entity>(assetEntityViewModel.Asset, mapping);
            sceneEditorVm.UndoRedoService.PushOperation(operation);
        }
    }

    public T GetAssetComponent<T>(T runtimeComponent)
        where T : EntityComponent
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var sceneEntity = runtimeComponent.Entity;
        var entityId = sceneEntity.Id;

        // The Game Studio holds a master version of the scene, whereas the processor (which we're currently in) is running on
        // a separate duplicate copy of the scene.
        // We need to find the matching Id (which should be unique) of the entity from the master version, then
        // find the associated asset node which will allow us to update an entity's property on the master version
        // and this will then propagate this back to our copy of the scene.
        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entityId);
        var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
        var assetSideSceneEntityComp = assetSideSceneEntity.Get<T>();
        return assetSideSceneEntityComp;
    }

    public void UpdateAssetComponentData<T>(T runtimeComponent, string propertyName, object? newValue)
        where T : EntityComponent
    {
        var sceneEntity = runtimeComponent.Entity;
        UpdateAssetComponentDataByEntityId<T>(sceneEntity.Id, propertyName, newValue);
    }

    public void UpdateAssetComponentDataByEntityId<T>(Guid entityId, string propertyName, object? newValue)
        where T : EntityComponent
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        // The Game Studio holds a master version of the scene, whereas the processor (which we're currently in) is running on
        // a separate duplicate copy of the scene.
        // We need to find the matching Id (which should be unique) of the entity from the master version, then
        // find the associated asset node which will allow us to update an entity's property on the master version
        // and this will then propagate this back to our copy of the scene.
        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entityId);
        var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
        var assetSideSceneEntityComp = assetSideSceneEntity.Get<T>();

        var entityCompNode = sceneEditorVm.Session.AssetNodeContainer.GetNode(assetSideSceneEntityComp);
        var entityCompPropertyNodeRaw = entityCompNode[propertyName];
        var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
        NullCheck(entityCompPropertyNode, $"Property '{propertyName}' was not found or invalid type.");

        entityCompPropertyNode.Update(newValue);
    }

    public void UpdateAssetComponentArrayData<T>(T runtimeComponent, string propertyName, object? newValue, int arrayIndex)
        where T : EntityComponent
    {
        var sceneEntity = runtimeComponent.Entity;
        UpdateAssetComponentArrayDataByEntityId<T>(sceneEntity.Id, propertyName, newValue, arrayIndex);
    }

    public void UpdateAssetComponentArrayDataByEntityId<T>(Guid entityId, string propertyName, object? newValue, int arrayIndex)
        where T : EntityComponent
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entityId);
        var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
        var assetSideSceneEntityComp = assetSideSceneEntity.Get<T>();

        var entityCompNode = sceneEditorVm.Session.AssetNodeContainer.GetNode(assetSideSceneEntityComp);
        var entityCompPropertyNodeRaw = entityCompNode[propertyName];
        var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
        NullCheck(entityCompPropertyNode, $"Property '{propertyName}' was not found or invalid type.");

        //var arrayData = entityCompPropertyNode.TargetReference.ObjectValue;
        var entityCompPropertyObjectNode = entityCompPropertyNode.Target;
        entityCompPropertyObjectNode.Update(newValue, new NodeIndex(arrayIndex));
    }

    public void UpdateAssetComponentArrayData<T>(T runtimeComponent, string propertyName, string subPropertyName, object? newValue, int arrayIndex)
        where T : EntityComponent
    {
        var sceneEntity = runtimeComponent.Entity;
        UpdateAssetComponentArrayDataByEntityId<T>(sceneEntity.Id, propertyName, subPropertyName, newValue, arrayIndex);
    }

    public void UpdateAssetComponentArrayDataByEntityId<T>(Guid entityId, string propertyName, string subPropertyName, object? newValue, int arrayIndex)
        where T : EntityComponent
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entityId);
        var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
        var assetSideSceneEntityComp = assetSideSceneEntity.Get<T>();

        var entityCompNode = sceneEditorVm.Session.AssetNodeContainer.GetNode(assetSideSceneEntityComp);
        var entityCompPropertyNodeRaw = entityCompNode[propertyName];
        var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
        NullCheck(entityCompPropertyNode, $"Property '{propertyName}' was not found or invalid type.");
        var subPropObjNode = entityCompPropertyNode.TargetReference.TargetNode;
        var subPropNodeRaw = subPropObjNode[subPropertyName];
        NullCheck(entityCompPropertyNode, $"Sub-Property '{subPropertyName}' was not found or invalid type.");
        var subPropNode = subPropNodeRaw as IAssetMemberNode;

        var subPropertyObjectNode = subPropNode.Target;
        subPropertyObjectNode.Update(newValue, new NodeIndex(arrayIndex));
    }

    public void AddAssetComponentArrayData<T>(T runtimeComponent, string propertyName, string subPropertyName, object? newValue, int? arrayIndex = null)
        where T : EntityComponent
    {
        var sceneEntity = runtimeComponent.Entity;
        AddAssetComponentArrayDataByEntityId<T>(sceneEntity.Id, propertyName, subPropertyName, newValue, arrayIndex);
    }

    public void AddAssetComponentArrayDataByEntityId<T>(Guid entityId, string propertyName, string subPropertyName, object? newValue, int? arrayIndex = null)
        where T : EntityComponent
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sceneEditorVm = _editorViewModels.Value.SceneEditorViewModel;
        EnsureInsideInvokeCall(sceneEditorVm);

        var assetEntityViewModel = FindEntityViewModel(sceneEditorVm, entityId);
        var assetSideSceneEntity = assetEntityViewModel.AssetSideEntity;        // This is the entity on the 'master' version.
        var assetSideSceneEntityComp = assetSideSceneEntity.Get<T>();

        var entityCompNode = sceneEditorVm.Session.AssetNodeContainer.GetNode(assetSideSceneEntityComp);
        var entityCompPropertyNodeRaw = entityCompNode[propertyName];
        var entityCompPropertyNode = entityCompPropertyNodeRaw as IAssetMemberNode;
        NullCheck(entityCompPropertyNode, $"Property '{propertyName}' was not found or invalid type.");
        var subPropObjNode = entityCompPropertyNode.TargetReference.TargetNode;
        var subPropNodeRaw = subPropObjNode[subPropertyName];
        NullCheck(entityCompPropertyNode, $"Sub-Property '{subPropertyName}' was not found or invalid type.");
        var subPropNode = subPropNodeRaw as IAssetMemberNode;

        var subPropertyObjectNode = subPropNode.Target;
        if (arrayIndex is int insertAtIndex)
        {
            subPropertyObjectNode.Add(newValue, new NodeIndex(insertAtIndex));
        }
        else
        {
            subPropertyObjectNode.Add(newValue);
        }
    }

    public void UpdateAssetProperty(object assetObject, string propertyName, object? newValue)
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sessionVm = _editorViewModels.Value.SessionViewModel;

        var nodeContainer = sessionVm.AssetNodeContainer;
        var assetObjectNode = nodeContainer.GetNode(assetObject);
        var assetObjectPropertyNodeRaw = assetObjectNode[propertyName];
        var assetObjectPropertyNode = assetObjectPropertyNodeRaw as IAssetMemberNode;
        NullCheck(assetObjectPropertyNode, $"Property '{propertyName}' was not found or invalid type.");

        assetObjectPropertyNode.Update(newValue);
    }

    public void RefreshAssetCollection(object assetCollectionContainerObject, string collectionMemberName)
    {
        EnsureInsideInvokeCall(_editorViewModels);
        var sessionVm = _editorViewModels.Value.SessionViewModel;

        var nodeContainer = sessionVm.AssetNodeContainer;
        var collContainerNode = nodeContainer.GetNode(assetCollectionContainerObject);
        var collectionNode = collContainerNode[collectionMemberName];
        collectionNode.Target.ItemReferences.Refresh(collectionNode, nodeContainer);
    }

    public void PushTransactionOperation(object operation)
    {
        var trxOp = operation as Stride.Core.Transactions.Operation;
        NullCheck(trxOp, $"Must be an Operation.");
        var undoRedoService = _editorViewModels?.SessionViewModel?.UndoRedoService;
        if (undoRedoService?.TransactionInProgress == true)
        {
            undoRedoService.PushOperation(trxOp);
        }
        else
        {
            Debug.Fail($"Transaction was not created.");
        }
    }

    public IDisposable CreateUndoRedoTransaction(string transactionName)
    {
        NullCheck(_editorViewModels, $"Must be called within {nameof(IStrideEditorService.Invoke)}");
        var undoRedoService = _editorViewModels?.SessionViewModel?.UndoRedoService;
        if (undoRedoService is not null)
        {
            var undoRedoTransaction = undoRedoService.CreateTransaction();
            undoRedoService.SetName(undoRedoTransaction, transactionName);
            return undoRedoTransaction;
        }
        else
        {
            return DisposableExtensions.Empty;
        }
    }

    public TAssetViewModel? FindAssetViewModel<TAssetViewModel>(object proxyObject)
        where TAssetViewModel : class
    {
        var returnValue = InvokeOnUI(() =>
        {
            var assetFinder = _editorViewModels?.SessionViewModel as IAssetFinder;
            NullCheck(assetFinder, "assetFinder was null.");
            var assetItem = assetFinder.FindAssetFromProxyObject(proxyObject);

            var sessionVm = _editorViewModels?.SessionViewModel;
            var assetVms = sessionVm?.AllAssets;
            var assetVm = assetVms?.Where(x => x.AssetItem == assetItem).FirstOrDefault();
            var castedVm = assetVm as TAssetViewModel;
            return castedVm;
        });
        return returnValue;
    }

    public TAssetViewModel? FindAssetViewModelByAsset<TAssetViewModel>(object asset)
        where TAssetViewModel : class
    {
        var returnValue = InvokeOnUI(() =>
        {
            var assetFinder = _editorViewModels?.SessionViewModel as IAssetFinder;
            NullCheck(assetFinder, "assetFinder was null.");

            var sessionVm = _editorViewModels?.SessionViewModel;
            var assetVms = sessionVm?.AllAssets;
            var assetVm = assetVms?.Where(x => x.Asset == asset).FirstOrDefault();
            var castedVm = assetVm as TAssetViewModel;
            return castedVm;
        });
        return returnValue;
    }

    public TAssetViewModel? FindAssetViewModelByUrl<TAssetViewModel>(string url)
        where TAssetViewModel : class
    {
        var returnValue = InvokeOnUI(() =>
        {
            var assetFinder = _editorViewModels?.SessionViewModel as IAssetFinder;
            NullCheck(assetFinder, "assetFinder was null.");

            var sessionVm = _editorViewModels?.SessionViewModel;
            var assetVms = sessionVm?.AllAssets;
            var urlUFile = new UFile(url);
            var assetVm = assetVms?.Where(x => x.AssetItem.Location == urlUFile).FirstOrDefault();
            var castedVm = assetVm as TAssetViewModel;
            return castedVm;
        });
        return returnValue;
    }

    public object? FindEntityViewModel(Guid entityId)
    {
        var returnValue = InvokeOnUI(() =>
        {
            var sceneEditorVm = _editorViewModels?.SceneEditorViewModel;
            if (sceneEditorVm is null)
            {
                return null;
            }
            var entityVm = FindEntityViewModel(sceneEditorVm, entityId);
            return entityVm;
        });
        return returnValue;
    }

    public IEditorContentLoader? GetEditorContentLoader()
    {
        var returnValue = InvokeOnUI(() =>
        {
            EnsureInsideInvokeCall(_editorViewModels);
            var sceneEditorController = GetSceneEditorController(_editorViewModels.Value.SceneEditorViewModel);
            return sceneEditorController?.Loader;
        });
        return returnValue;
    }

    public IObjectNode? GetOrCreateGameSideNode(object rootObject)
    {
        var returnValue = InvokeOnUI(() =>
        {
            EnsureInsideInvokeCall(_editorViewModels);
            var sceneEditorController = GetSceneEditorController(_editorViewModels.Value.SceneEditorViewModel);
            var rootNode = sceneEditorController?.GameSideNodeContainer?.GetOrCreateNode(rootObject);
            return rootNode;
        });
        return returnValue;
    }

    public TAsset? FindGameSettingsAsset<TAsset>()
        where TAsset : class
    {
        var returnValue = InvokeOnUI(() =>
        {
            var sessionVm = _editorViewModels?.SessionViewModel;
            var gameSettingsAsset = sessionVm?.CurrentProject?.Package.GetGameSettingsAsset();
            if (gameSettingsAsset is null && sessionVm is not null)
            {
                var gamePkgViewModel = sessionVm.LocalPackages.FirstOrDefault(x => x.Name.EndsWith(".Game", StringComparison.OrdinalIgnoreCase));
                gameSettingsAsset = gamePkgViewModel?.Package?.GetGameSettingsAsset();
                if (gameSettingsAsset is null)
                {
                    // Search all packages
                    foreach (var pkgVm in sessionVm.LocalPackages)
                    {
                        gameSettingsAsset = pkgVm?.Package?.GetGameSettingsAsset();
                        if (gameSettingsAsset is not null)
                        {
                            break;
                        }
                    }
                }
            }
            return gameSettingsAsset;
        });
        return returnValue as TAsset;
    }

    private static SceneEditorController? GetSceneEditorController(SceneEditorViewModel? sceneEditorVm)
    {
        if (sceneEditorVm is null)
        {
            return null;
        }
        var getController_PropertyInfo = typeof(SceneEditorViewModel).GetProperty("Controller", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        NullCheck(getController_PropertyInfo, "SceneEditorViewModel.Controller is missing.");
        var getController_MethodInfo = getController_PropertyInfo.GetGetMethod(nonPublic: true);
        var sceneEditorController = getController_MethodInfo?.Invoke(sceneEditorVm, []) as SceneEditorController;
        return sceneEditorController;
    }

    private void EnsureInsideInvokeCall([NotNull] EditorViewModels? _editorViewModels)
    {
        if (!_editorViewModels.HasValue)
        {
            throw new InvalidOperationException($"Ensure this is called within {nameof(IStrideEditorService.Invoke)}/{nameof(IStrideEditorService.InvokeAsync)}.");
        }
    }

    private void EnsureInsideInvokeCall([NotNull] SceneEditorViewModel? sceneEditorVm)
    {
        if (sceneEditorVm is null)
        {
            throw new InvalidOperationException($"Ensure this is called within {nameof(IStrideEditorService.Invoke)}/{nameof(IStrideEditorService.InvokeAsync)}.");
        }
    }

    private TReturnValue? InvokeOnUI<TReturnValue>(Func<TReturnValue> func)
    {
        if (_editorViewModels.HasValue)
        {
            // Already on UI thread
            var obj = func();
            return obj;
        }

        var gsVm = GameStudioViewModel.GameStudio;
        if (!IsValid(gsVm))
        {
            return default;
        }
        var returnValue = gsVm.StrideAssets.Dispatcher.Invoke(() =>
        {
            _editorViewModels = EditorViewModels.Create();
            var obj = func();
            _editorViewModels = null;
            return obj;
        });
        return returnValue;
    }

    private static EntityViewModel FindEntityViewModel(SceneEditorViewModel sceneEditorVm, Guid sceneEntityId)
    {
        // The entity in the game scene is NOT the same instance as the entity in the *asset file*,
        // so we need to find the 'master' entity

        var sceneRootVm = sceneEditorVm.RootPart as SceneRootViewModel;
        NullCheck(sceneRootVm, "SceneRootViewModel was null.");
        if (TryFindEntityViewModel(sceneEntityId, sceneRootVm, out var assetEntity))
        {
            return assetEntity;
        }

        Debug.Fail($"Entity ID {sceneEntityId} not found.");
        throw new ArgumentException($"Entity ID {sceneEntityId} not found.", paramName: nameof(sceneEntityId));

        static bool TryFindEntityViewModel(Guid sceneEntityId, EntityHierarchyItemViewModel entityHierarchy, out EntityViewModel entityViewModel)
        {
            if (entityHierarchy is EntityViewModel evm && evm.AssetSideEntity.Id == sceneEntityId)
            {
                entityViewModel = evm;
                return true;
            }
            // This will also look into child scenes since they're part of the hierarchy
            foreach (var childHierarchy in entityHierarchy.Children)
            {
                if (TryFindEntityViewModel(sceneEntityId, childHierarchy, out entityViewModel))
                {
                    return true;
                }
            }

            entityViewModel = null!;
            return false;
        }
    }

    private static Func<GameStudioViewModel, bool>? IsGameStudioViewModelDestroyed;
    private static bool IsValid(GameStudioViewModel gsVm)
    {
        // HACK: When closing the editor, UI events may be triggered causing your own code
        // to try invoke UI methods. We check the GameStudioViewModel if its IsDestroyed value
        // which is true when the editor is closing.
        if (IsGameStudioViewModelDestroyed is null)
        {
            var isDestroyed_PropertyInfo = typeof(GameStudioViewModel).GetProperty("IsDestroyed", BindingFlags.NonPublic | BindingFlags.Instance);
            Debug.Assert(isDestroyed_PropertyInfo is not null);
            var isDestroyed_MethodInfo = isDestroyed_PropertyInfo.GetGetMethod(nonPublic: true);
            Debug.Assert(isDestroyed_MethodInfo is not null);
            IsGameStudioViewModelDestroyed = isDestroyed_MethodInfo.CreateDelegate<Func<GameStudioViewModel, bool>>();
        }
        return !IsGameStudioViewModelDestroyed(gsVm);
    }

    private static void NullCheck<T>([NotNull] T? objectToCheck, string errorMessage)
    {
        if (objectToCheck is null)
        {
            throw new InvalidOperationException(errorMessage);
        }
    }

    private struct EditorViewModels
    {
        public SceneViewModel? SceneViewModel;
        public SceneEditorViewModel? SceneEditorViewModel;
        public SessionViewModel SessionViewModel;

        public static EditorViewModels Create()
        {
            var viewModels = new EditorViewModels();

            //var editorVm = Stride.Core.Assets.Editor.ViewModel.EditorViewModel.Instance as Stride.GameStudio.GameStudioViewModel;     // Unused

            // Application.Current must be accessed on the UI thread
            var window = (GameStudioWindow)System.Windows.Application.Current.MainWindow;
            var sceneEditorView = window.GetChildOfType<SceneEditorView>();
            var sceneEditorVm = sceneEditorView?.DataContext as SceneEditorViewModel;
            var sceneVm = sceneEditorVm?.Asset;

            var gsVm = GameStudioViewModel.GameStudio;

            viewModels.SceneViewModel = sceneVm;
            viewModels.SceneEditorViewModel = sceneEditorVm;
            viewModels.SessionViewModel = gsVm.Session;

            return viewModels;
        }
    }
}

static class WpfExt
{
    public static T? GetChildOfType<T>(this System.Windows.DependencyObject depObj)
        where T : System.Windows.DependencyObject
    {
        if (depObj is null) return null;

        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);

            var result = child as T ?? child.GetChildOfType<T>();
            if (result is not null) return result;
        }
        return null;
    }
}
