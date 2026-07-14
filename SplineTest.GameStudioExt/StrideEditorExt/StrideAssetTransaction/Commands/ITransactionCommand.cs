namespace SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction.Commands;

/// <summary>
/// The command to execute in an asset modification transaction.
/// </summary>
public interface ITransactionCommand
{
    public void Execute();
    /// <summary>
    /// Creates the command that will undo this command's execution.
    /// </summary>
    public ITransactionCommand CreateInverse();
}
