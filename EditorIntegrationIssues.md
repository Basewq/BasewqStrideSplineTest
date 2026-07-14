# Editor Integration Issues

### List of issues:

- [Scene Editing Control](#scene-editing-control)
- [Editor Mouse Control](#editor-mouse-control)
- [Editor Keyboard Control](#editor-keyboard-control)
- [Update Entity or Asset Properties](#update-entity-or-asset-properties)

---
### Scene Editing Control

#### Problem:

No obvious way to register your own editor control and also ensure that your editor control is fully in control of the scene.

This is required as the spline editor needs to be able to select nodes & handles and not be affected by things like mouse clicking in the scene being treated as entity selection/deselection.

#### Discussion:

`IEditorGameEntitySelectionService` is always active, which is fine if selecting entities via the tree view, however it competes with your own control when trying to click within the scene.
It is possible to prevent this via inheriting `EditorGameMouseServiceBase` and set `IsControllingMouse = true`, however doing this also blocks `IEditorGameCameraService` which prevents you from moving the camera within the scene, so you need to be selective on when to block.

Need to establish deterministic rules/order of execution around editor controls.
May also need some clearer mechanism on blocking/consuming inputs (both mouse input & keyboard input).

A proposed solution is for an 'interaction capture request service' that tools submits to, which can accept or reject based on prioritized context.

Most likely place to add registration is in inheriting `StrideAssetsPlugin` then `AssetsPlugin.RegisterPlugin(typeof(MyEditorPlugin))` in a `[ModuleInitializer]` method, eg. see [EditorModule.cs](SplineTest.GameStudioExt/EditorModule.cs)

---
### Editor Mouse Control

#### Problem:

There exists an `EditorGameMouseServiceBase` which is extended so you can prevent others from taking mouse control.

The problem is that these are only registered in `EntityHierarchyEditorController.InitializeServices` which is hardcoded:

https://github.com/stride3d/stride/blob/3f175bd1d64bc1c7444656a7c44f101168533c89/sources/editor/Stride.Assets.Presentation/AssetEditors/EntityHierarchyEditor/Services/EntityHierarchyEditorController.cs#L54

Then each mouse service holds a *local* copy of every other mouse service (used to check if another service has control or not), and no further mouse services can be attached that will be recognized by the other services:

https://github.com/stride3d/stride/blob/3f175bd1d64bc1c7444656a7c44f101168533c89/sources/editor/Stride.Assets.Presentation/AssetEditors/EntityHierarchyEditor/Game/EntityHierarchyEditorGame.cs#L315


#### Discussion:

Workaround requires C# reflection and manually inject your mouse service into every other mouse service, eg. [StrideEditorMouseService.cs](SplineTest.GameStudioExt/StrideEditorExt/StrideEditorMouseService.cs)

The order of execution of each mouse services are not clear without looking into each implementation.

---
### Editor Keyboard Control

#### Problem:

There is no keyboard equivalent of `EditorGameMouseServiceBase` to block keyboard 'actions'.

It needs to be able to take priority with keyboard input, eg. Delete key used to delete spline nodes instead of deleting the entity from the scene.
Alternatively, keyboard input is blocked based on whether the editor control is active or not.

#### Discussion:

Solution should probably be solved as a whole as per [Scene Editing Control](#scene-editing-control) issue.

---
### Update Entity or Asset Properties

#### Problem:

Updating entity or asset properties are not user friendly.
It requires deep knowledge of the Quantum node system, especially if trying to modify sub-object properties and easily breaks if the object structure changes.

#### Discussion:

Need to provide a way for developers to not need to be aware of the Quantum node system, eg. just edit the entity component property 'normally' and somehow get the editor to detect this change.

Current workaround is have a dedicated class that records all property values for a given object (via reflection), then diffs the changes (via reflection again), eg.

[AssetTransactionBuilder.cs](https://github.com/Basewq/StrideEdExt/blob/main/StrideEdExt.StrideAssetExt/Assets/Transaction/AssetTransactionBuilder.cs) - tracks property/fields at the start and diffs at the end.
This only tracks serializable fields/properties like `[DataContract]` objects since it uses Stride's `DataVisitorBase` internally.

This partially simplifies the code:
```
using (var undoRedoTransaction = UndoRedoService.CreateTransaction())
{
    UndoRedoService.SetName(undoRedoTransaction, "Edit Asset");
    var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetObject);
    // Modify Asset object in normal way...
    var assetTransaction = assetTransactionBuilder.CreateTransaction(Session.AssetNodeContainer);
    var trxOp = new AssetTransactionOperation(dirtiables: [this], assetTransaction);
    UndoRedoService.PushOperation(trxOp);
}
```

[ObjectPlacementMapAssetViewModel.cs](https://github.com/Basewq/StrideEdExt/blob/83df1737b4781f535eff9da50b255a47d144be8b/StrideEdExt.GameStudioExt/AssetViewModels/ObjectPlacementMapAssetViewModel.cs#L347-L364) - actual example of how it's used.

---
