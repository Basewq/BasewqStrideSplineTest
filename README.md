# Stride Spline Editor Proof of Concept

This repository contains a basic proof of concept to create a Spline editing system into Stride & its Editor.

The purpose of this project examine the problems with editor integration, as of Stride version `4.3.0.2507`.

Please refer to [Editor Integration Issues](EditorIntegrationIssues.md) for outstanding issues.

---
### Demonstration

https://github.com/user-attachments/assets/201bc08c-eda5-4654-afd1-1f225d132b62

---
### Project Structure

The projects are linked in the following way:
```
SplineTest.Windows ----> SplineTest ----> SplineTest.Splines
                                        /
                                       /
          SplineTest.GameStudioExt ----
```

Note that `SplineTest.GameStudioExt` is only connected to `SplineTest.Splines` and not connected to the Windows/Game project.
This will be loaded within the Stride Editor since the editor loads all projects within the `.sln` file, but will not be included in the run-time output folder.

---
### Editor usage

- In scene, create Entity
- Select entity -> Add component -> Splines -> Spline

While the entity is selected, the spline editor tool should (mostly) be active.
> Note a current bug is that clicking within the scene may select another entity or deselect when clicking into an empty space.

Editor controls within the scene:
- Selecting a node or tangent handle: standard left click.
- If node & tangents are overlapping: Hold Alt to only select nodes, hold Ctrl to only select tangent handles.
- To append new nodes, hold Shift then left click in the scene.
- To delete a node, you must use the component properties sidebar to remove from the list.

---
### Points of Interest

- `SplineTest.GameStudioExt`
  - [EditorModule.cs](SplineTest.GameStudioExt/EditorModule.cs): Registers editor services within `Game.GameStarted` event.
  - [EditorGameSplineEditorGizmoService.cs](SplineTest.GameStudioExt/AssetEditors/EntityHierarchyEditor/Game/EditorGameSplineEditorGizmoService.cs): Main coordinator of which gizmos are active when modifying a spline, and commits the data changes back to the asset when the user has finished their action.
  - [SplineGizmo.cs](SplineTest.GameStudioExt/AssetEditors/Gizmos/SplineGizmo.cs): Handles the visual of the spline, coordinates which gizmo is active when editing a node.
  - [SplineNodeGizmo.cs](SplineTest.GameStudioExt/AssetEditors/Gizmos/SplineNodeGizmo.cs): Handles the visual of node points and tangent handles when editing the node.
- `SplineTest.Splines`
  - [SplineComponent.cs](SplineTest.Splines/Components/SplineComponent.cs): The spline properties as it appears in the editor.
  - [BezierSpline.cs](SplineTest.Splines/Splines/Models/BezierSpline.cs): The actual implementation of the spline. Can be used independently.

---
## Credits/Thanks
- [Aggror](https://github.com/Aggror): Original Spline Implementation. See [Github PR.](https://github.com/stride3d/stride/pull/1287)
