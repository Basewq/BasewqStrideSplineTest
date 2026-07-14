using Stride.Core.Quantum;
using Stride.Core.Reflection;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;

public class ModifyCollectionCommand : ITransactionCommand
{
    private readonly IObjectNode _rootObjectNode;
    private readonly MemberPath _memberPath;
    private readonly ModifyCollectionType _modifyCollectionType;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public ModifyCollectionCommand(IObjectNode rootObject, MemberPath memberPath, ModifyCollectionType modifyCollectionType, object? oldValue, object? newValue)
    {
        _rootObjectNode = rootObject;
        _memberPath = memberPath;
        _modifyCollectionType = modifyCollectionType;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        var collectionPath = _memberPath.Clone();
        collectionPath.Pop();

        var collectionIndex = _memberPath.GetIndex();
        var nodeIndex = new NodeIndex(collectionIndex);

        var graphNodePath = GraphNodePath.From(_rootObjectNode, collectionPath, out _);
        var collectionMemberNode = (IMemberNode)graphNodePath.GetNode();
        var collectionNode = collectionMemberNode.Target!;
        if (_modifyCollectionType == ModifyCollectionType.Remove)
        {
            collectionNode.Remove(_oldValue!, nodeIndex);
        }
        else if (_modifyCollectionType == ModifyCollectionType.Add)
        {
            collectionNode.Add(_newValue!, nodeIndex);
        }
        else
        {
            collectionNode.Update(_newValue, nodeIndex);
        }
    }

    public ITransactionCommand CreateInverse()
    {
        var inverseModifyType = _modifyCollectionType switch
        {
            ModifyCollectionType.Remove => ModifyCollectionType.Add,
            ModifyCollectionType.Add => ModifyCollectionType.Remove,
            ModifyCollectionType.Update => ModifyCollectionType.Update,
            _ => throw new NotImplementedException($"ModifyCollectionType '{_modifyCollectionType}' not implemented.")
        };
        var cmd = new ModifyCollectionCommand(_rootObjectNode, _memberPath, inverseModifyType, oldValue: _newValue, newValue: _oldValue);
        return cmd;
    }
}

public enum ModifyCollectionType
{
    Add,
    Remove,
    Update,
}
