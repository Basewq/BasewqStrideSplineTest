namespace SplineTest.GameStudioExt.StrideEditorExt;

interface IStrideEditorMouseService
{
    bool IsMouseAvailable { get; }
    void SetIsControllingMouse(bool isControllingMouse, object owner);
    bool IsControllingMouseByOwner(object owner);
}
