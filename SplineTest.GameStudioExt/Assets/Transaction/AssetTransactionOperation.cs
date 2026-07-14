using SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;
using Stride.Core.Presentation.Dirtiables;

namespace SplineTest.GameStudioExt.Assets.Transaction;

public class AssetTransactionOperation : DirtyingOperation
{
    private AssetTransaction? _assetTransaction;
    private AssetTransaction? _undoTransaction;

    public AssetTransactionOperation(IEnumerable<IDirtiable> dirtiables, AssetTransaction assetTransaction)
        : base(dirtiables)
    {
        _assetTransaction = assetTransaction;
    }

    protected override void FreezeContent()
    {
        _assetTransaction = null;
        _undoTransaction = null;
    }

    protected override void Undo()
    {
        if (_assetTransaction is null)
        {
            return;
        }

        _undoTransaction ??= _assetTransaction.CreateInverse();
        _undoTransaction.Execute();
    }

    protected override void Redo()
    {
        _assetTransaction?.Execute();
    }
}
