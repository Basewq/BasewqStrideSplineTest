using Stride.Core.Quantum;
using Stride.Core.Reflection;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;

public class SetMemberValueCommand : ITransactionCommand
{
    private readonly IObjectNode _rootObjectNode;
    private readonly MemberPath _memberPath;
    private readonly object? _oldValue;
    private readonly object? _newValue;

    public SetMemberValueCommand(IObjectNode rootObject, MemberPath memberPath, object? oldValue, object? newValue)
    {
        _rootObjectNode = rootObject;
        _memberPath = memberPath;
        _oldValue = oldValue;
        _newValue = newValue;
    }

    public void Execute()
    {
        var graphNodePath = GraphNodePath.From(_rootObjectNode, _memberPath, out _);
        var nodeAccessor = graphNodePath.GetAccessor();
        nodeAccessor.UpdateValue(_newValue!);
    }

    public ITransactionCommand CreateInverse()
    {
        var cmd = new SetMemberValueCommand(_rootObjectNode, _memberPath, oldValue: _newValue, newValue: _oldValue);
        return cmd;
    }
}
