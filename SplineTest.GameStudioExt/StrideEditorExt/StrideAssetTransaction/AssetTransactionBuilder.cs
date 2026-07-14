using SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;
using Stride.Core.Assets.Quantum;
using Stride.Core.Quantum;
using Stride.Core.Reflection;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;

public class AssetTransactionBuilder : IAssetTransactionBuilder
{
    private readonly List<AssetSnapshot> _assetSnapshots = [];
    private readonly List<ITransactionCommand> _transactionCommands = [];
    private List<Action>? _postExecuteActions;

    public static AssetTransactionBuilder Begin(object asset)
    {
        //if (asset is not Asset && asset is not IIdentifiable)
        //{
        //    throw new ArgumentException($"Asset must be of type Asset or IIdentifiable (eg. Entity/EntityComponent)", paramName: nameof(asset));
        //}
        var builder = new AssetTransactionBuilder();
        builder.IncludeSnapshot(asset);
        return builder;
    }

    public void IncludeSnapshot(object asset)
    {
        if (_assetSnapshots.Any(x => x.Asset == asset))
        {
            // Already being tracked
            return;
        }
        //if (asset is not Asset && asset is not IIdentifiable)
        //{
        //    throw new ArgumentException($"Asset must be of type Asset or IIdentifiable (eg. Entity/EntityComponent)", paramName: nameof(asset));
        //}

        var assetMemberCollector = new AssetMemberCollector();
        assetMemberCollector.Visit(asset);
        var assetSnapshot = new AssetSnapshot
        {
            Asset = asset,
            AssetMemberItems = assetMemberCollector.AssetMemberItems
        };
        _assetSnapshots.Add(assetSnapshot);
    }

    public void AddCommand(ITransactionCommand command)
    {
        _transactionCommands.Add(command);
    }

    /// <summary>
    /// Add an <see cref="Action"/> that executes on undo/redo (and executed when <see cref="CreateTransaction"/> is called).
    /// </summary>
    public void AddPostExecuteAction(Action action)
    {
        _postExecuteActions ??= [];
        _postExecuteActions.Add(action);
    }

    /// <summary>
    /// Create the transaction that modifies the asset for undo/redo.
    /// </summary>
    public AssetTransaction CreateTransaction(AssetNodeContainer assetNodeContainer, bool autoRefreshAssetNodes = false)
    {
        var commands = new List<ITransactionCommand>();

        var processedCollectionSet = new HashSet<IObjectNode>();

        foreach (var snapshot in _assetSnapshots)
        {
            var asset = snapshot.Asset;
            var assetObjectNode = assetNodeContainer.GetNode(asset);
            if (assetObjectNode is null)
            {
                throw new InvalidOperationException($"Snapshot object is not a valid object: {asset.GetType().Name}");
            }

            var newStateChangesCollector = new AssetNewStateChangesCollector(snapshot);
            newStateChangesCollector.Visit(asset);

            var memberChanges = newStateChangesCollector.NewStateMemberChanges;
            foreach (var memberChange in memberChanges)
            {
                switch (memberChange.ChangeType)
                {
                    case ContentChangeType.ValueChange:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            var newValue = memberChange.NewValue;
                            var setMemberValueCmd = new SetMemberValueCommand(assetObjectNode, memberPath, oldValue, newValue);
                            commands.Add(setMemberValueCmd);
                            if (autoRefreshAssetNodes)
                            {
                                var graphNodePath = GraphNodePath.From(assetObjectNode, memberPath, out _);
                                var nodeAccessor = graphNodePath.GetAccessor();
                                nodeAccessor.UpdateValue(newValue);
                            }
                        }
                        break;
                    case ContentChangeType.CollectionUpdate:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            var newValue = memberChange.NewValue;
                            var modifyCollectionCmd = new ModifyCollectionCommand(assetObjectNode, memberPath, ModifyCollectionType.Update, oldValue, newValue);
                            commands.Add(modifyCollectionCmd);
                            if (autoRefreshAssetNodes)
                            {
                                var collectionPath = memberPath.Clone();
                                collectionPath.Pop();
                                var graphNodePath = GraphNodePath.From(assetObjectNode, collectionPath, out _);
                                var collectionMemberNode = (IMemberNode)graphNodePath.GetNode();
                                if (collectionMemberNode.Target is not null
                                    && processedCollectionSet.Add(collectionMemberNode.Target))
                                {
                                    collectionMemberNode.Target.ItemReferences!.Refresh(collectionMemberNode, assetNodeContainer);
                                }
                            }
                        }
                        break;
                    case ContentChangeType.CollectionAdd:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            var newValue = memberChange.NewValue;
                            var setMemberValueCmd = new ModifyCollectionCommand(assetObjectNode, memberPath, ModifyCollectionType.Add, oldValue, newValue);
                            commands.Add(setMemberValueCmd);
                            if (autoRefreshAssetNodes)
                            {
                                var collectionPath = memberPath.Clone();
                                collectionPath.Pop();
                                var graphNodePath = GraphNodePath.From(assetObjectNode, collectionPath, out _);
                                var collectionMemberNode = (IMemberNode)graphNodePath.GetNode();
                                if (collectionMemberNode.Target is not null
                                    && processedCollectionSet.Add(collectionMemberNode.Target))
                                {
                                    collectionMemberNode.Target.ItemReferences.Refresh(collectionMemberNode, assetNodeContainer);
                                }
                            }
                        }
                        break;
                    case ContentChangeType.CollectionRemove:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            var newValue = memberChange.NewValue;
                            var setMemberValueCmd = new ModifyCollectionCommand(assetObjectNode, memberPath, ModifyCollectionType.Remove, oldValue, newValue);
                            commands.Add(setMemberValueCmd);
                            if (autoRefreshAssetNodes)
                            {
                                var collectionPath = memberPath.Clone();
                                collectionPath.Pop();
                                var graphNodePath = GraphNodePath.From(assetObjectNode, collectionPath, out _);
                                var collectionMemberNode = (IMemberNode)graphNodePath.GetNode();
                                if (collectionMemberNode.Target is not null
                                    && processedCollectionSet.Add(collectionMemberNode.Target))
                                {
                                    collectionMemberNode.Target.ItemReferences?.Refresh(collectionMemberNode, assetNodeContainer);
                                }
                            }
                        }
                        break;
                    case ContentChangeType.None:
                    default:
                        // ?
                        break;
                }
            }
        }

        commands.AddRange(_transactionCommands);

        var assetTransaction = new AssetTransaction(commands, _postExecuteActions?.ToList());
        return assetTransaction;
    }

    public void RevertAssetState(AssetNodeContainer assetNodeContainer)
    {
        // Similar to CreateTransaction -> Undo, but we only modify the underlying object, ie. not quantum nodes
        var commands = new List<Action>();

        foreach (var snapshot in _assetSnapshots)
        {
            var asset = snapshot.Asset;
            var assetObjectNode = assetNodeContainer.GetNode(asset);
            if (assetObjectNode is null)
            {
                throw new InvalidOperationException($"Snapshot object is not a valid object: {asset.GetType().Name}");
            }
            var rootObj = assetObjectNode.Retrieve();

            var newStateChangesCollector = new AssetNewStateChangesCollector(snapshot);
            newStateChangesCollector.Visit(asset);

            var memberChanges = newStateChangesCollector.NewStateMemberChanges;
            foreach (var memberChange in memberChanges)
            {
                switch (memberChange.ChangeType)
                {
                    case ContentChangeType.CollectionUpdate:
                    case ContentChangeType.ValueChange:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            commands.Add(() => memberPath.Apply(rootObj, MemberPathAction.ValueSet, oldValue));
                        }
                        break;
                    case ContentChangeType.CollectionAdd:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;

                            var collectionPaths = memberPath.Decompose();
                            var lastPath = collectionPaths[^1];
                            var memberPathAction = MemberPathAction.CollectionRemove;
                            if (lastPath is MemberPath.DictionaryPathItem)
                            {
                                memberPathAction = MemberPathAction.DictionaryRemove;
                            }
                            commands.Add(() => memberPath.Apply(rootObj, memberPathAction, oldValue));
                        }
                        break;
                    case ContentChangeType.CollectionRemove:
                        {
                            var memberPath = memberChange.MemberPath;
                            var oldValue = memberChange.OldValue;
                            var newValue = memberChange.NewValue;

                            var collectionPaths = memberPath.Decompose();
                            var lastPath = collectionPaths[^1];
                            var memberPathAction = MemberPathAction.CollectionRemove;
                            if (lastPath is MemberPath.DictionaryPathItem)
                            {
                                memberPathAction = MemberPathAction.DictionaryRemove;
                            }
                            commands.Add(() => memberPath.Apply(rootObj, memberPathAction, newValue));
                        }
                        break;
                    case ContentChangeType.None:
                    default:
                        // ?
                        break;
                }
            }
        }

        // Undo in reverse
        commands.Reverse();
        foreach (var cmd in commands)
        {
            cmd.Invoke();
        }
    }

    public List<AssetMemberValueChange> GetAllChanges()
    {
        var memberChanges = new List<AssetMemberValueChange>();
        foreach (var snapshot in _assetSnapshots)
        {
            var asset = snapshot.Asset;

            var newStateChangesCollector = new AssetNewStateChangesCollector(snapshot);
            newStateChangesCollector.Visit(asset);

            var newStateMemberItems = newStateChangesCollector.NewStateMemberChanges;
            memberChanges.AddRange(newStateMemberItems);
        }
        return memberChanges;
    }

    private class AssetSnapshot
    {
        public required object Asset { get; init; }
        public required List<AssetMemberItem> AssetMemberItems { get; init; }
        /// <summary>
        /// When comparing new state to old state, some paths may have changed,
        /// eg. item shifted in a list
        /// </summary>
        public List<(MemberPath NewStatePath, MemberPath OldStatePath)> ReroutedPaths { get; } = [];
    }

    private class AssetMemberCollector : DataVisitorBase
    {
        public List<AssetMemberItem> AssetMemberItems { get; } = [];

        public override void VisitNull()
        {
            var memberPath = CurrentPath.Clone();
            var memberItem = new AssetMemberItem(memberPath, null);
            AssetMemberItems.Add(memberItem);

            base.VisitNull();
        }

        public override void VisitPrimitive(object primitive, PrimitiveDescriptor descriptor)
        {
            var memberPath = CurrentPath.Clone();
            var memberItem = new AssetMemberItem(memberPath, primitive);
            AssetMemberItems.Add(memberItem);

            base.VisitPrimitive(primitive, descriptor);
        }

        public override void VisitObject(object obj, ObjectDescriptor descriptor, bool visitMembers)
        {
            var memberPath = CurrentPath.Clone();
            AssetMemberItem memberItem;
            // Special cases: need to track the items inside list/collections/set/dictionary
            switch (descriptor.Category)
            {
                case DescriptorCategory.List:
                case DescriptorCategory.Collection:
                case DescriptorCategory.Set:
                    {
                        var memColl = new AssetMemberCollection(memberPath, obj)
                        {
                            IsOrderedList = descriptor.Category != DescriptorCategory.Set
                        };
                        foreach (var collItem in (System.Collections.IEnumerable)obj)
                        {
                            memColl.ObjectList.Add(collItem);
                        }
                        memberItem = memColl;
                    }
                    break;
                case DescriptorCategory.Dictionary:
                    {
                        var dictDesc = (DictionaryDescriptor)descriptor;
                        var memDict = new AssetMemberDictionary(memberPath, obj);
                        var keyValues = dictDesc.GetEnumerator(obj);
                        if (keyValues is not null)
                        {
                            memDict.KeyValueList.AddRange(keyValues);
                        }
                        memberItem = memDict;
                    }
                    break;

                default:
                    memberItem = new AssetMemberItem(memberPath, obj);
                    break;
            }

            AssetMemberItems.Add(memberItem);
            base.VisitObject(obj, descriptor, visitMembers);
        }

        public override void VisitDictionaryKeyValue(object dictionary, DictionaryDescriptor descriptor, object key, ITypeDescriptor? keyDescriptor, object? value, ITypeDescriptor? valueDescriptor)
        {
            // Note: We don't visit the key, and don't track changes within the key
            Visit(value, valueDescriptor);
        }
    }

    private class AssetNewStateChangesCollector : DataVisitorBase
    {
        private readonly AssetSnapshot _oldSnapshot;

        public List<AssetMemberItem> UnvisitedMembers { get; } = [];
        public List<AssetMemberValueChange> NewStateMemberChanges { get; } = [];

        public AssetNewStateChangesCollector(AssetSnapshot oldSnapshot)
        {
            _oldSnapshot = oldSnapshot;
            UnvisitedMembers.AddRange(_oldSnapshot.AssetMemberItems);
        }

        public override void VisitNull()
        {
            MarkVisited(CurrentPath);
            if (!TryGetOldStateValue(CurrentPath, out var oldValue, out _, out var reusableMemberPath)
                || oldValue is not null)
            {
                var memberPath = reusableMemberPath ?? CurrentPath.Clone();
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = ContentChangeType.ValueChange,
                    OldValue = oldValue,
                    NewValue = null
                };
                NewStateMemberChanges.Add(memDiff);
            }
        }

        public override void VisitPrimitive(object primitive, PrimitiveDescriptor descriptor)
        {
            MarkVisited(CurrentPath);
            if (!TryGetOldStateValue(CurrentPath, out var oldValue, out _, out var reusableMemberPath)
                || !Equals(oldValue, primitive))
            {
                var memberPath = reusableMemberPath ?? CurrentPath.Clone();
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = ContentChangeType.ValueChange,
                    OldValue = oldValue,
                    NewValue = primitive
                };
                NewStateMemberChanges.Add(memDiff);
            }
        }

        public override void VisitObject(object obj, ObjectDescriptor descriptor, bool visitMembers)
        {
            if (_oldSnapshot.ReroutedPaths.TryFindIndex(x => x.NewStatePath.Match(CurrentPath), out int reroutedPathIndex))
            {
                var (newStatePath, oldStatePath) = _oldSnapshot.ReroutedPaths[reroutedPathIndex];
                var oldStateMemberItem = UnvisitedMembers.FirstOrDefault(x => x.MemberPath.Match(oldStatePath));
                if (oldStateMemberItem is null)
                {
                    // Already handled
                    return;
                }
            }

            MarkVisited(CurrentPath);
            if (!TryGetOldStateValue(CurrentPath, out var oldValue, out var memberItem, out var reusableMemberPath)
                || !Equals(oldValue, obj))
            {
                var memberPath = reusableMemberPath ?? CurrentPath.Clone();
                var changeType = memberPath.GetIndex() is not null ? ContentChangeType.CollectionUpdate : ContentChangeType.ValueChange;
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = changeType,
                    OldValue = oldValue,
                    NewValue = obj
                };
                NewStateMemberChanges.Add(memDiff);
                return;     // Do not need to visit the object's internals because the entire object has changed
            }

            // Check if this is a list/collection/etc and get the changes
            if (memberItem is AssetMemberCollection memberCollection)
            {
                var newStateEnumerable = (System.Collections.IEnumerable)obj;
                if (memberCollection.IsOrderedList)
                {
                    ProcessOrderedCollectionChanges(memberCollection, newStateEnumerable, descriptor);
                    return;
                }
                else
                {
                    ProcessUnorderedCollectionChanges(memberCollection, newStateEnumerable, descriptor);
                    return;
                }
            }
            else if (memberItem is AssetMemberDictionary memberDictionary)
            {
                ProcessDictionaryChanges(memberDictionary, obj, descriptor);
                return;
            }

            // Visit the object's members
            base.VisitObject(obj, descriptor, visitMembers);
        }

        public override void VisitDictionaryKeyValue(object dictionary, DictionaryDescriptor descriptor, object key, ITypeDescriptor? keyDescriptor, object? value, ITypeDescriptor? valueDescriptor)
        {
            // Note: We don't visit the key, and don't track changes within the key
            Visit(value, valueDescriptor);
        }

        private void ProcessOrderedCollectionChanges(AssetMemberCollection oldStateMemberCollection, System.Collections.IEnumerable newStateEnumerable, ObjectDescriptor collectionObjectDescriptor)
        {
            if (collectionObjectDescriptor is not CollectionDescriptor collDescriptor)
            {
                throw new NotSupportedException($"Unhandled descriptor type: {collectionObjectDescriptor.GetType().Name}");
            }

            var newStateObjectList = newStateEnumerable.Cast<object?>().ToList();
            int oldStateObjectListCount = oldStateMemberCollection.ObjectList.Count;
            var curStateObjectList = oldStateMemberCollection.ObjectList.ToList();      // Make copy to attempt to tranform this list into the new state
            var oldToNewObjectListIndexShiftMap = ArrayPool<int>.Shared.Rent(minimumLength: oldStateObjectListCount);
            Array.Clear(oldToNewObjectListIndexShiftMap);
            for (int i = 0, origOldStateIndex = 0; i < curStateObjectList.Count; i++, origOldStateIndex++)
            {
                var curStateListItem = curStateObjectList[i];
                if (i >= newStateObjectList.Count)
                {
                    // Item was removed
                    var memberPath = CurrentPath.Clone();
                    memberPath.Push(collDescriptor, index: i);
                    var memDiff = new AssetMemberValueChange
                    {
                        Asset = _oldSnapshot.Asset,
                        MemberPath = memberPath,
                        ChangeType = ContentChangeType.CollectionRemove,
                        OldValue = curStateListItem,
                        NewValue = null,
                        IndexKey = i,
                    };
                    NewStateMemberChanges.Add(memDiff);
                    continue;
                }

                var newItem = newStateObjectList[i];
                if (Equals(curStateListItem, newItem))
                {
                    // Check if the object's internals have changed
                    CurrentPath.Push(collDescriptor, index: i);
                    VisitCollectionItem(newStateEnumerable, collDescriptor, i, newItem, TypeDescriptorFactory.Find(newItem?.GetType() ?? collDescriptor.ElementType));
                    CurrentPath.Pop();
                    continue;
                }

                // Check if the old item was shifted right (ie. new item was inserted)
                int shiftedItemNewIndex = -1;
                for (int j = i + 1; j < newStateObjectList.Count; j++)
                {
                    if (Equals(curStateListItem, newStateObjectList[j]))
                    {
                        shiftedItemNewIndex = j;
                        break;
                    }
                }
                if (shiftedItemNewIndex >= 0)
                {
                    // New item has been added
                    int newItemsCount = shiftedItemNewIndex - i;
                    for (int newItemIndex = 0; newItemIndex < newItemsCount; newItemIndex++)
                    {
                        int insertListIndex = i + newItemIndex;
                        var memberPath = CurrentPath.Clone();
                        memberPath.Push(collDescriptor, index: insertListIndex);

                        var insertedNewItem = newStateObjectList[insertListIndex];
                        curStateObjectList.Insert(index: insertListIndex, insertedNewItem);

                        var memDiff = new AssetMemberValueChange
                        {
                            Asset = _oldSnapshot.Asset,
                            MemberPath = memberPath,
                            ChangeType = ContentChangeType.CollectionAdd,
                            OldValue = null,
                            NewValue = insertedNewItem,
                            IndexKey = insertListIndex,
                        };
                        NewStateMemberChanges.Add(memDiff);
                    }
                    // Create reroute paths due to shifted indices
                    for (int j = origOldStateIndex; j < oldStateObjectListCount; j++)
                    {
                        oldToNewObjectListIndexShiftMap[j] += newItemsCount;
                        int oldStateIndex = j;
                        int newStateIndex = j + oldToNewObjectListIndexShiftMap[j];

                        var newStateMemberPath = CurrentPath.Clone();
                        newStateMemberPath.Push(collDescriptor, index: newStateIndex);
                        var oldStateMemberPath = CurrentPath.Clone();
                        oldStateMemberPath.Push(collDescriptor, index: oldStateIndex);

                        if (_oldSnapshot.ReroutedPaths.TryFindIndex(x => x.NewStatePath.Match(newStateMemberPath), out int existingReroutePathIndex))
                        {
                            var existingReroutePath = _oldSnapshot.ReroutedPaths[existingReroutePathIndex];
                            existingReroutePath.OldStatePath = oldStateMemberPath;
                            _oldSnapshot.ReroutedPaths[existingReroutePathIndex] = existingReroutePath;
                        }
                        else
                        {
                            _oldSnapshot.ReroutedPaths.Add((newStateMemberPath, oldStateMemberPath));
                        }
                    }

                    i = shiftedItemNewIndex - 1;   // Move for-loop
                    continue;
                }

                bool removeItem = true;
                if (i < newStateObjectList.Count && collDescriptor.ElementType.IsValueType)
                {
                    // Note: For value types, just treat as item update
                    removeItem = false;
                }

                if (removeItem)
                {
                    var memberPath = CurrentPath.Clone();
                    memberPath.Push(collDescriptor, index: i);
                    var memDiff = new AssetMemberValueChange
                    {
                        Asset = _oldSnapshot.Asset,
                        MemberPath = memberPath,
                        ChangeType = ContentChangeType.CollectionRemove,
                        OldValue = curStateListItem,
                        NewValue = null,
                        IndexKey = i,
                    };
                    NewStateMemberChanges.Add(memDiff);

                    // Create reroute paths due to shifted indices
                    for (int j = origOldStateIndex + 1; j < oldStateObjectListCount; j++)
                    {
                        oldToNewObjectListIndexShiftMap[j] -= 1;
                        int oldStateIndex = j;
                        int newStateIndex = j + oldToNewObjectListIndexShiftMap[j];

                        var newStateMemberPath = CurrentPath.Clone();
                        newStateMemberPath.Push(collDescriptor, index: newStateIndex);
                        var oldStateMemberPath = CurrentPath.Clone();
                        oldStateMemberPath.Push(collDescriptor, index: oldStateIndex);

                        if (_oldSnapshot.ReroutedPaths.TryFindIndex(x => x.NewStatePath.Match(newStateMemberPath), out int existingReroutePathIndex))
                        {
                            var existingReroutePath = _oldSnapshot.ReroutedPaths[existingReroutePathIndex];
                            existingReroutePath.OldStatePath = oldStateMemberPath;
                            _oldSnapshot.ReroutedPaths[existingReroutePathIndex] = existingReroutePath;
                        }
                        else
                        {
                            _oldSnapshot.ReroutedPaths.Add((newStateMemberPath, oldStateMemberPath));
                        }
                    }

                    curStateObjectList.RemoveAt(i);
                    i--;
                }
                else
                {
                    // Check if the object's internals have changed
                    CurrentPath.Push(collDescriptor, index: i);
                    VisitCollectionItem(newStateEnumerable, collDescriptor, i, newItem, TypeDescriptorFactory.Find(newItem?.GetType() ?? collDescriptor.ElementType));
                    CurrentPath.Pop();
                }
            }
            // Any remaining items must be newly added
            for (int i = curStateObjectList.Count; i < newStateObjectList.Count; i++)
            {
                var newItem = newStateObjectList[i];

                var memberPath = CurrentPath.Clone();
                memberPath.Push(collDescriptor, index: i);

                // New item has been added
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = ContentChangeType.CollectionAdd,
                    OldValue = null,
                    NewValue = newItem,
                    IndexKey = i,
                };
                NewStateMemberChanges.Add(memDiff);
            }

            ArrayPool<int>.Shared.Return(oldToNewObjectListIndexShiftMap);
        }

        private void ProcessUnorderedCollectionChanges(AssetMemberCollection oldStateMemberCollection, System.Collections.IEnumerable newStateEnumerable, ObjectDescriptor collectionObjectDescriptor)
        {
            // Only Set type is used as unordered collection
            if (collectionObjectDescriptor is not SetDescriptor setDescriptor)
            {
                throw new NotSupportedException($"Unhandled descriptor type: {collectionObjectDescriptor.GetType().Name}");
            }

            var unprocessedNewStateObjectList = newStateEnumerable.Cast<object>().ToList();
            var oldStateObjectList = oldStateMemberCollection.ObjectList;
            for (int i = 0; i < oldStateObjectList.Count; i++)
            {
                var oldStateListItem = oldStateObjectList[i];
                if (unprocessedNewStateObjectList.TryFindIndex(x => Equals(oldStateListItem, x), out int newStateListIndex))
                {
                    // Check if the object's internals have changed
                    CurrentPath.Push(setDescriptor, index: oldStateListItem!);
                    VisitSetItem(newStateEnumerable, setDescriptor, oldStateListItem, TypeDescriptorFactory.Find(oldStateListItem?.GetType() ?? setDescriptor.ElementType));
                    CurrentPath.Pop();

                    unprocessedNewStateObjectList.RemoveAt(newStateListIndex);
                    continue;
                }
                else
                {
                    // Item was removed
                    var memberPath = CurrentPath.Clone();
                    memberPath.Push(setDescriptor, index: oldStateListItem!);
                    var memDiff = new AssetMemberValueChange
                    {
                        Asset = _oldSnapshot.Asset,
                        MemberPath = memberPath,
                        ChangeType = ContentChangeType.CollectionRemove,
                        OldValue = oldStateListItem,
                        NewValue = null,
                        IndexKey = oldStateListItem,
                    };
                    NewStateMemberChanges.Add(memDiff);

                    unprocessedNewStateObjectList.RemoveAt(newStateListIndex);
                }
            }
            // Any remaining items must be newly added
            for (int i = 0; i < unprocessedNewStateObjectList.Count; i++)
            {
                var newItem = unprocessedNewStateObjectList[i];

                var memberPath = CurrentPath.Clone();
                memberPath.Push(setDescriptor, index: newItem!);

                // New item has been added
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = ContentChangeType.CollectionAdd,
                    OldValue = null,
                    NewValue = newItem,
                    IndexKey = newItem,
                };
                NewStateMemberChanges.Add(memDiff);
            }
        }

        private void ProcessDictionaryChanges(AssetMemberDictionary oldStateMemberDictionary, object newStateDictionary, ObjectDescriptor collectionObjectDescriptor)
        {
            // Only Set type is used as unordered collection
            if (collectionObjectDescriptor is not DictionaryDescriptor dictDescriptor)
            {
                throw new NotSupportedException($"Unhandled descriptor type: {collectionObjectDescriptor.GetType().Name}");
            }

            var unprocessedNewStateKeyValueList = dictDescriptor.GetEnumerator(newStateDictionary).ToList();
            var oldStateKeyValueList = oldStateMemberDictionary.KeyValueList;
            for (int i = 0; i < oldStateKeyValueList.Count; i++)
            {
                var (oldStateListItemKey, oldStateListItemValue) = oldStateKeyValueList[i];
                if (unprocessedNewStateKeyValueList.TryFindIndex(x => Equals(oldStateListItemKey, x.Key), out int newStateListIndex))
                {
                    // Check if the object's internals have changed
                    var keyDescriptor = TypeDescriptorFactory.Find(oldStateListItemKey.GetType() ?? dictDescriptor.KeyType);
                    var valueDescriptor = TypeDescriptorFactory.Find(oldStateListItemValue?.GetType() ?? dictDescriptor.ValueType);

                    CurrentPath.Push(dictDescriptor, key: oldStateListItemKey!);
                    VisitDictionaryKeyValue(newStateDictionary, dictDescriptor, oldStateListItemKey, keyDescriptor, oldStateListItemValue, valueDescriptor);
                    CurrentPath.Pop();

                    unprocessedNewStateKeyValueList.RemoveAt(newStateListIndex);
                    continue;
                }
                else
                {
                    // Item was removed
                    var memberPath = CurrentPath.Clone();
                    memberPath.Push(dictDescriptor, key: oldStateListItemKey);
                    var memDiff = new AssetMemberValueChange
                    {
                        Asset = _oldSnapshot.Asset,
                        MemberPath = memberPath,
                        ChangeType = ContentChangeType.CollectionRemove,
                        OldValue = oldStateListItemKey,
                        NewValue = null,
                        IndexKey = oldStateListItemKey,
                    };
                    NewStateMemberChanges.Add(memDiff);
                }
            }
            // Any remaining items must be newly added
            for (int i = 0; i < unprocessedNewStateKeyValueList.Count; i++)
            {
                var (newStateListItemKey, newStateListItemValue) = unprocessedNewStateKeyValueList[i];

                var memberPath = CurrentPath.Clone();
                memberPath.Push(dictDescriptor, key: newStateListItemKey);

                // New item has been added
                var memDiff = new AssetMemberValueChange
                {
                    Asset = _oldSnapshot.Asset,
                    MemberPath = memberPath,
                    ChangeType = ContentChangeType.CollectionAdd,
                    OldValue = null,
                    NewValue = newStateListItemValue,
                    IndexKey = newStateListItemKey
                };
                NewStateMemberChanges.Add(memDiff);
            }
        }

        private void MarkVisited(MemberPath currentPath)
        {
            // TODO: is there any way to speed this up?
            int index = UnvisitedMembers.FindIndex(x => x.MemberPath.Match(currentPath));
            if (index >= 0)
            {
                UnvisitedMembers.RemoveAt(index);
            }
        }

        private bool TryGetOldStateValue(
            MemberPath currentPath,
            out object? memberValue, [NotNullWhen(true)] out AssetMemberItem? memberItem, [NotNullWhen(true)] out MemberPath? reusableMemberPath)
        {
            memberValue = null;
            memberItem = null;
            reusableMemberPath = _oldSnapshot.AssetMemberItems.FirstOrDefault(x => x.MemberPath.Match(currentPath))?.MemberPath;

            var actualOldStatePath = currentPath;
            for (int i = _oldSnapshot.ReroutedPaths.Count - 1; i >= 0; i--)
            {
                var (newStatePath, oldStatePath) = _oldSnapshot.ReroutedPaths[i];
                if (MemberPathExtensions.IsSameOrSubpath(newStatePath, actualOldStatePath))
                {
                    actualOldStatePath = MemberPathExtensions.BuildReroutedPath(actualOldStatePath, newStatePath, oldStatePath);
                }
            }
            foreach (var oldStateMemberItem in _oldSnapshot.AssetMemberItems)
            {
                if (oldStateMemberItem.MemberPath.Match(actualOldStatePath))
                {
                    memberValue = oldStateMemberItem.MemberValue;
                    memberItem = oldStateMemberItem;
                    reusableMemberPath ??= oldStateMemberItem.MemberPath;
                    return true;
                }
            }

            return false;
        }
    }

    private record AssetMemberItem(MemberPath MemberPath, object? MemberValue);

    private record AssetMemberCollection(MemberPath MemberPath, object? MemberValue)
        : AssetMemberItem(MemberPath, MemberValue)
    {
        /// <summary>
        /// Items that are part of this collection.
        /// </summary>
        public readonly List<object?> ObjectList = [];
        public required bool IsOrderedList { get; init; }
    }

    private record AssetMemberDictionary(MemberPath MemberPath, object? MemberValue)
        : AssetMemberItem(MemberPath, MemberValue)
    {
        /// <summary>
        /// Key-value pairs that are part of this dictionary.
        /// </summary>
        public readonly List<KeyValuePair<object, object?>> KeyValueList = [];
    }

    public class AssetMemberValueChange
    {
        public required object Asset { get; init; }
        public required MemberPath MemberPath { get; init; }
        public required ContentChangeType ChangeType { get; init; }
        public required object? OldValue { get; init; }
        public required object? NewValue { get; init; }
        public object? IndexKey { get; init; }
    }
}
