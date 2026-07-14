using SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;

namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;

public interface IAssetTransactionBuilder
{
    void IncludeSnapshot(object asset);

    void AddCommand(ITransactionCommand command);
}
