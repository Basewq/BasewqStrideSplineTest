using SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;

public class AssetTransaction
{
    private readonly List<ITransactionCommand> _transactionCommands;
    private readonly List<Action>? _postExecuteActions;

    public bool IsEmpty => _transactionCommands.Count == 0;

    public AssetTransaction(List<ITransactionCommand> commands, List<Action>? postExecuteActions)
    {
        _transactionCommands = commands;
        _postExecuteActions = postExecuteActions;
    }

    public void Execute()
    {
        foreach (var cmd in _transactionCommands)
        {
            cmd.Execute();
        }
        if (_postExecuteActions is not null)
        {
            foreach (var postExeAction in _postExecuteActions)
            {
                postExeAction();
            }
        }
    }

    public AssetTransaction CreateInverse()
    {
        var reversedCommands = _transactionCommands.Select(x => x.CreateInverse())
                                .Reverse()
                                .ToList();
        var transaction = new AssetTransaction(reversedCommands, _postExecuteActions);
        return transaction;
    }
}
