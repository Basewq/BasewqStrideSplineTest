# Stride Spline Editor Proof of Concept

This repository contains a basic proof of concept to create a Spline editing system into Stride & its Editor.

The purpose of this project examine the problems with editor integration, as of Stride version `4.3.0.2507`.

Please refer to [Editor Integration Issues](EditorIntegrationIssues.md) for outstanding issues.

---
### Demonstration

https://github.com/user-attachments/assets/987a06b4-58c3-4229-abd2-956d2d785604

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
- Selecting a control point or tangent handle: standard left click.
- If control point & tangents are overlapping: Hold Alt to only select control points, hold Ctrl to only select tangent handles.
- To append new control points, hold Shift then left click in the scene.
- To delete a control point, you must use the component properties sidebar to remove from the list.

---
### Points of Interest

- `SplineTest.GameStudioExt`
  - [EditorModule.cs](SplineTest.GameStudioExt/EditorModule.cs): Registers editor services within `Game.GameStarted` event.
  - [EditorGameSplineEditorGizmoService.cs](SplineTest.GameStudioExt/AssetEditors/EntityHierarchyEditor/Game/EditorGameSplineEditorGizmoService.cs): Main coordinator of which gizmos are active when modifying a spline, and commits the data changes back to the asset when the user has finished their action.
  - [SplineGizmo.cs](SplineTest.GameStudioExt/AssetEditors/Gizmos/SplineGizmo.cs): Handles the visual of the spline, coordinates which gizmo is active when editing a control point.
  - [SplineControlPointGizmo.cs](SplineTest.GameStudioExt/AssetEditors/Gizmos/SplineControlPointGizmo.cs): Handles the visual of control point points and tangent handles when editing the control point.
- `SplineTest.Splines`
  - [SplineComponent.cs](SplineTest.Splines/Components/SplineComponent.cs): The spline properties as it appears in the editor.
  - [Spline.cs](SplineTest.Splines/Splines/Models/Spline.cs): The data store of the spline. Can be used independently.
  - [SplineEvaluator.cs](SplineTest.Splines/Splines/Models/SplineEvaluator.cs): The default implementation of how to read the spline. `Spline` uses this implementation as its default, but you can use implement your own `SplineEvaluator` and set it via `Spline.SplineEvaluator`, or altenatively just use the evaluator directly.
  - [LineVisualizerComponent.cs](SplineTest.Splines/Rendering/LineVisualizerComponent.cs)
  - [LineVisualizerRenderFeature.cs](SplineTest.Splines/Rendering/LineVisualizer/LineVisualizerRenderFeature.cs): Required by `LineVisualizerComponent` to render the spline lines. **Must be registered in the Graphics Compositor.**
  - [GizmoMarkerSetComponent.cs](SplineTest.Splines/Rendering/GizmoMarkerSetComponent.cs)
  - [GizmoMarkerRenderFeature.cs](SplineTest.Splines/Rendering/GizmoMarker/GizmoMarkerRenderFeature.cs): Required by `GizmoMarkerSetComponent` to render the control points & tangent points in the editor. **Must be registered in the Graphics Compositor.**

---
## Credits/Thanks
- [Aggror](https://github.com/Aggror): Original Spline Implementation. See [Github PR.](https://github.com/stride3d/stride/pull/1287)
