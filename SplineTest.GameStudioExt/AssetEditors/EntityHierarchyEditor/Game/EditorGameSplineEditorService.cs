// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.StrideEditorExt;
using SplineTest.Rendering;
using SplineTest.Splines.Rendering.GizmoMarker;
using SplineTest.Splines.Rendering.LineVisualizer;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.Gizmos;
using Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Input;
using Stride.Rendering;
using System.Runtime.InteropServices;

namespace Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;

/// <summary>
/// A class that manages spline control & tangent points editing.
/// </summary>
public class EditorGameSplineEditorService : EditorGameServiceBase
{
    private static readonly Color4 CurveColor = Color.Aqua.ToColor4();

    private readonly StrideEditorService strideEditorService;

    private EntityHierarchyEditorGame game;
    private Scene editorScene;
    private InputManager inputManager;
    private IEditorGameEntitySelectionService entitySelectionService;
    private IStrideEditorMouseService editorMouseService;
    private IEditorGameCameraService cameraService;

    private TranslationGizmo pointTransformGizmo;

    private SplineComponent? activeSplineComponent;
    private int activeControlPointIndex = -1;
    private SplinePointEditingSelectionType activePointEditingSelectionType;
    private Entity activePointAnchorEntity = null;
    private readonly List<Entity> activePointAnchorEntityList = [];   // Only used for pointTransformGizmo.ModifiedEntities
    private bool refreshAnchorPosition = false;

    private bool isSplineChangedUpdateRequired = false;

    private Vector3 prevGizmoRootPosition = new Vector3(float.MinValue);
    public Entity splineEditingGizmoRootEntity;

    private readonly List<SplineControlPointGizmo> controlPointGizmos = [];

    private bool isAddingControlPoint = false;      // HACK: need to prevent multiple adds in a single 'click'
    private SplineControlPointGizmo mouseHoverControlPointGizmo = null;
    private int mouseHoverControlPointIndex = -1;

    private readonly List<Vector3> splineSamplePoints = [];

    internal SplineComponent? ActiveSplineComponent => activeSplineComponent;

    public override IEnumerable<Type> Dependencies => [
        typeof(IEditorGameCameraService)
    ];

    public EditorGameSplineEditorService(StrideEditorService strideEditorService)
    {
        this.strideEditorService = strideEditorService;
    }

    protected override Task<bool> Initialize([NotNull] EditorServiceGame editorGame)
    {
        game = (EntityHierarchyEditorGame)editorGame;
        editorScene = game.EditorScene;

        inputManager = game.Services.GetService<InputManager>();
        entitySelectionService = game.EditorServices.Get<IEditorGameEntitySelectionService>();
        entitySelectionService?.SelectionUpdated += OnEntitySelectionService_SelectionUpdated;
        editorMouseService = StrideEditorMouseService.GetOrCreate(game.Services);

        splineEditingGizmoRootEntity = new Entity("spline Editing Root");
        editorScene.Entities.Add(splineEditingGizmoRootEntity);

        activePointAnchorEntity = new Entity("Edit spline Point Anchor");     // Entity to be moved by the TranslationGizmo
        activePointAnchorEntityList.Add(activePointAnchorEntity);
        activePointAnchorEntity.Scene = editorScene;

        pointTransformGizmo = new TranslationGizmo();
        pointTransformGizmo.Initialize(game.Services, editorScene);
        pointTransformGizmo.IsEnabled = false;    // Must disable AFTER Initialize
        pointTransformGizmo.AnchorEntity = activePointAnchorEntity;
        pointTransformGizmo.TransformationEnded += OnTransformGizmo_TransformationEnded;
        pointTransformGizmo.ModifiedEntities = activePointAnchorEntityList;

        game.Script.AddTask(OnGameUpdate, priority: 1000);

        return Task.FromResult(true);
    }

    private void OnEntitySelectionService_SelectionUpdated(object sender, EntitySelectionEventArgs e)
    {
        var activeSplineEntity = activeSplineComponent?.Entity;
        if (activeSplineEntity is not null
            && !e.NewSelection.Contains(activeSplineEntity))
        {
            DeactivateEditSpline();
        }
        else if (e.NewSelection.Count == 1)
        {
            var selectedEntity = e.NewSelection.First();
            var splineComp = selectedEntity.Get<SplineComponent>();
            if (splineComp is not null && activeSplineEntity != selectedEntity)
            {
                if (activeSplineEntity is not null)
                {
                    DeactivateEditSpline();
                }
                ActivateSplinePointEditing(splineComp, controlPointIndex: -1, editingSelectionType: SplinePointEditingSelectionType.None);
            }
        }
    }

    private void OnTransformGizmo_TransformationEnded(object sender, EventArgs e)
    {
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Update Tangent Position"))
            {
                var splineEntityId = activeSplineComponent.Entity.Id;

                var controlPoint = activeSplineComponent.Spline[activeControlPointIndex];
                strideEditorService.UpdateAssetComponentArrayDataByEntityId<SplineComponent>(splineEntityId, nameof(SplineComponent.Spline), nameof(Engine.Splines.Models.Spline.ControlPoints), controlPoint, activeControlPointIndex);
            }
        });
    }

    public override ValueTask DisposeAsync()
    {
        if (activeSplineComponent is not null)
        {
            DeactivateEditSpline();
        }
        entitySelectionService?.SelectionUpdated -= OnEntitySelectionService_SelectionUpdated;

        if (splineEditingGizmoRootEntity is not null)
        {
            splineEditingGizmoRootEntity.Scene = null;
            splineEditingGizmoRootEntity.Dispose();
            splineEditingGizmoRootEntity = null;
        }

        return base.DisposeAsync();
    }

    public override void UpdateGraphicsCompositor(EditorServiceGame game)
    {
        base.UpdateGraphicsCompositor(game);

        if (game is SceneEditorGame sceneEditorGame)
        {
            var gfxComp = sceneEditorGame.EditorSceneSystem?.GraphicsCompositor;
            var fwdRenderer = (gfxComp.Editor as Stride.Rendering.Compositing.ForwardRenderer);
            if (gfxComp is not null && fwdRenderer is not null)
            {
                if (!gfxComp.RenderFeatures.Any(x => x is LineVisualizerRenderFeature))
                {
                    gfxComp.RenderFeatures.Add(new LineVisualizerRenderFeature
                    {
                        RenderStageSelectors =
                        {
                            new LineVisualizerRenderStageSelector
                            {
                                EffectName = "Test",
                                OpaqueRenderStage = fwdRenderer?.OpaqueRenderStage,
                                TransparentRenderStage = fwdRenderer?.TransparentRenderStage,
                                RenderGroup =  RenderGroupMask.All,
                            },
                        },
                    });
                }
                if (!gfxComp.RenderFeatures.Any(x => x is GizmoMarkerRenderFeature))
                {
                    gfxComp.RenderFeatures.Add(new GizmoMarkerRenderFeature
                    {
                        RenderStageSelectors =
                        {
                            new GizmoMarkerRenderStageSelector
                            {
                                EffectName = "Test",
                                //OpaqueRenderStage = fwdRenderer?.OpaqueRenderStage,
                                TransparentRenderStage = fwdRenderer?.TransparentRenderStage,
                                RenderGroup =  RenderGroupMask.All,
                            },
                        },
                    });
                }
            }
        }
    }

    private async Task OnGameUpdate()
    {
        while (!IsDisposed)
        {
            if (IsActive)
            {
                //if (IsMouseAvailable)
                //{
                //}

                if (activeSplineComponent is not null && activeSplineComponent.Entity is null)
                {
                    // Component was removed from the entity
                    DeactivateEditSpline();
                }

                if (activeSplineComponent?.Entity is Entity splineEntity)
                {
                    splineEntity.Transform.WorldMatrix.Decompose(out var scale, out Quaternion rotation, out var rootPosition);
                    splineEditingGizmoRootEntity.Transform.Position = rootPosition;
                    splineEditingGizmoRootEntity.Transform.Rotation = rotation;
                    splineEditingGizmoRootEntity.Transform.Scale = scale;
                    splineEditingGizmoRootEntity.Transform.UpdateWorldMatrix();

                    if (prevGizmoRootPosition != rootPosition)
                    {
                        isSplineChangedUpdateRequired = true;
                    }
                    prevGizmoRootPosition = rootPosition;
                }

                UpdateActiveControlPointGizmos();

                if (isSplineChangedUpdateRequired)
                {
                    if (activeSplineComponent is not null)
                    {
                        if (activeControlPointIndex >= activeSplineComponent.Spline.Count)
                        {
                            DeactivateEditSpline(deselectSpline: false);
                        }
                        else if (activeControlPointIndex >= 0)
                        {
                            refreshAnchorPosition = true;   // Refresh changes in the next update
                        }

                    }
                    RegenerateSplineVisualizer();

                    isSplineChangedUpdateRequired = false;
                }

                if (activeSplineComponent is not null)
                {
                    await UpdateTransformGizmoChangesAsync();
                    UpdateControlPointEditingState();
                }
            }

            await game.Script.NextFrame();
        }
    }

    private void UpdateActiveControlPointGizmos()
    {
        int ctrlPointCount = activeSplineComponent?.Spline?.Count ?? 0;
        if (controlPointGizmos.Count != ctrlPointCount)
        {
            isSplineChangedUpdateRequired = true;
        }
        if (controlPointGizmos.Count < ctrlPointCount)
        {
            // Populate new gizmos
            int controlPointStartIndex = controlPointGizmos.Count;
            int addControlPointCount = ctrlPointCount - controlPointStartIndex;
            for (int i = 0; i < addControlPointCount; i++)
            {
                var controlPointGizmo = new SplineControlPointGizmo(controlPointStartIndex + i, activeSplineComponent);
                controlPointGizmo.Initialize(game.Services, editorScene);
                controlPointGizmos.Add(controlPointGizmo);
                controlPointGizmo.IsEnabled = true;
                if (controlPointGizmo.IsEnabled)
                {
                    controlPointGizmo.GizmoRootEntity?.SetParent(splineEditingGizmoRootEntity);
                }
            }
        }
        else if (controlPointGizmos.Count > ctrlPointCount)
        {
            // Remove excess gizmos
            int controlPointEndIndex = ctrlPointCount;
            for (int i = controlPointGizmos.Count - 1; i >= controlPointEndIndex; i--)
            {
                var controlPointGizmo = controlPointGizmos[i];
                controlPointGizmo.IsEnabled = false;
                controlPointGizmo.GizmoRootEntity?.SetParent(null);
                controlPointGizmo.Dispose();
                if (mouseHoverControlPointGizmo == controlPointGizmo)
                {
                    mouseHoverControlPointGizmo = null;
                }

                controlPointGizmos.RemoveAt(i);
            }
        }
    }

    private void RegenerateSplineVisualizer()
    {
        splineSamplePoints.Clear();
        if (activeSplineComponent is not null)
        {
            SplineExtensions.CollectSplineSamplePoints(activeSplineComponent.Spline, splineSamplePoints, sampleResolutionPerCurve: 64);
        }

        var lineVisualizerComponent = splineEditingGizmoRootEntity.Get<LineVisualizerComponent>();
        if (lineVisualizerComponent is null)
        {
            lineVisualizerComponent = new LineVisualizerComponent();
            splineEditingGizmoRootEntity.Add(lineVisualizerComponent);
        }
        else
        {
            lineVisualizerComponent.LineSet.Segments.Clear();
        }

        var splineSamplePointsSpan = CollectionsMarshal.AsSpan(splineSamplePoints);
        for (int i = 0; i < splineSamplePointsSpan.Length - 1; i++)
        {
            var lineStartPos = splineSamplePointsSpan[i];
            var lineNextPos = splineSamplePointsSpan[i + 1];

            var instData = new LineSegment
            {
                StartPosition = lineStartPos,
                EndPosition = lineNextPos,
                StartColor = CurveColor,
                EndColor = CurveColor,
                LineThicknessPx = 3
            };
            lineVisualizerComponent.LineSet.Segments.Add(instData);
        }
    }

    private void UpdateControlPointEditingState()
    {
        var spline = activeSplineComponent.Spline;
        if (spline is null)
        {
            return;
        }

        // Lazy get camera service for TryGetMouseRay
        cameraService ??= Services.Get<IEditorGameCameraService>();

        bool isAltKeyDown = inputManager.IsKeyDown(Keys.LeftAlt) || inputManager.IsKeyDown(Keys.RightAlt);
        bool isCtrlKeyDown = inputManager.IsKeyDown(Keys.LeftCtrl) || inputManager.IsKeyDown(Keys.RightCtrl);
        if (!inputManager.IsMouseButtonDown(MouseButton.Left))
        {
            SplineControlPointGizmo? raycastHitControlPointGizmo = null;
            int raycastHitControlPointIndex = -1;
            var raycastHitControlPointEditingSelectionType = SplinePointEditingSelectionType.None;
            if (TryGetMouseRay(out var mouseRay))
            {
                var raycastFilterFlags = SplinePointRaycastFilterFlags.All;
                if (isAltKeyDown)
                {
                    raycastFilterFlags = SplinePointRaycastFilterFlags.ControlPoint;
                }
                else if (isCtrlKeyDown)
                {
                    raycastFilterFlags = SplinePointRaycastFilterFlags.Tangents;
                }
                bool raycastOnControlPointOnly = isAltKeyDown;
                float minHitDistance = float.PositiveInfinity;
                for (int i = 0; i < controlPointGizmos.Count; i++)
                {
                    var controlPointGizmo = controlPointGizmos[i];
                    if (controlPointGizmo.TryRaycastOnHandle(mouseRay, raycastFilterFlags, ref minHitDistance, ref raycastHitControlPointEditingSelectionType))
                    {
                        raycastHitControlPointGizmo = controlPointGizmo;
                        raycastHitControlPointIndex = i;
                    }
                }
            }

            if (mouseHoverControlPointGizmo != raycastHitControlPointGizmo)
            {
                mouseHoverControlPointGizmo?.EditingSelectionType = SplinePointEditingSelectionType.None;

                mouseHoverControlPointGizmo = raycastHitControlPointGizmo;
                mouseHoverControlPointIndex = raycastHitControlPointIndex;

                mouseHoverControlPointGizmo?.EditingSelectionType = raycastHitControlPointEditingSelectionType;
            }
        }

        bool isShiftKeyDown = inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift);
        bool canControlMouse = editorMouseService.IsMouseAvailable;
        bool isControllingMouse = canControlMouse && (isShiftKeyDown || isAltKeyDown || isCtrlKeyDown);

        if (canControlMouse && inputManager.IsMouseButtonPressed(MouseButton.Left))
        {
            if (isShiftKeyDown)
            {
                if (!isAddingControlPoint && TryGetMouseRay(out var mouseRay))
                {
                    // Plane = Ax + By + Cz + D = 0 => Positive Height = Negative D
                    Plane plane;
                    if (spline.Count == 0)
                    {
                        // No existing control points, so just raycast on XZ plane where height is the same as the spline
                        plane = new Plane(Vector3.UnitY, d: -activeSplineComponent.Entity.Transform.Position.Y);
                    }
                    else
                    {
                        // Raycast on XZ plane where height is the same as the last control point's height
                        var controlPoint = spline[spline.Count - 1];
                        plane = new Plane(Vector3.UnitY, d: -controlPoint.Position.Y);
                    }
                    if (CollisionHelper.RayIntersectsPlane(in mouseRay, in plane, out Vector3 hitPoint))
                    {
                        AddControlPoint(hitPoint);
                        isAddingControlPoint = true;
                    }
                    isControllingMouse = true;
                }
            }
            else if (mouseHoverControlPointGizmo is not null)
            {
                ActivateSplinePointEditing(activeSplineComponent, mouseHoverControlPointIndex, mouseHoverControlPointGizmo.EditingSelectionType);
                isControllingMouse = true;
            }
        }
        if (!inputManager.IsMouseButtonDown(MouseButton.Left))
        {
            isAddingControlPoint = false;
        }
        if (editorMouseService.IsControllingMouseByOwner(this)
            && (inputManager.IsMouseButtonDown(MouseButton.Left) || inputManager.IsMouseButtonReleased(MouseButton.Left)))
        {
            isControllingMouse = true;  // Continue controlling
        }

        editorMouseService.SetIsControllingMouse(isControllingMouse, owner: this);
        System.Diagnostics.Debug.WriteLineIf(isControllingMouse, $"{GetType().Name} isControllingMouse");

        for (int i = 0; i < controlPointGizmos.Count; i++)
        {
            var controlPointGizmo = controlPointGizmos[i];
            controlPointGizmo.Update();
        }
    }

    private async Task UpdateTransformGizmoChangesAsync()
    {
        if (!pointTransformGizmo.IsEnabled
            || activeControlPointIndex < 0 || activeControlPointIndex >= activeSplineComponent.Spline.Count)
        {
            return;
        }

        var controlPoint = activeSplineComponent.Spline[activeControlPointIndex];
        if (refreshAnchorPosition)
        {
            UpdateAnchorEntityPosition(controlPoint);
            refreshAnchorPosition = false;
        }

        await pointTransformGizmo.Update();

        var anchorEntityPos = activePointAnchorEntity.Transform.Position;
        Matrix.Invert(ref activeSplineComponent.Entity.Transform.WorldMatrix, out var splineWorldInverseMatrix);
        Vector3.Transform(in anchorEntityPos, in splineWorldInverseMatrix, out Vector3 anchorLocalPos);
        if (activePointEditingSelectionType == SplinePointEditingSelectionType.ControlPoint)
        {
            if (controlPoint.Position != anchorLocalPos)
            {
                controlPoint.Position = anchorLocalPos;
                activeSplineComponent.Spline[activeControlPointIndex] = controlPoint;
            }
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentIn)
        {
            if (controlPoint.TangentInPosition != anchorLocalPos)
            {
                controlPoint.TangentIn = anchorLocalPos - controlPoint.Position;
                // Mirror the other handle
                controlPoint.TangentOut = -controlPoint.TangentIn;
                activeSplineComponent.Spline[activeControlPointIndex] = controlPoint;
            }
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentOut)
        {
            if (controlPoint.TangentOutPosition != anchorLocalPos)
            {
                controlPoint.TangentOut = anchorLocalPos - controlPoint.Position;
                // Mirror the other handle
                controlPoint.TangentIn = -controlPoint.TangentOut;
                activeSplineComponent.Spline[activeControlPointIndex] = controlPoint;
            }
        }
    }

    private EditorGameEntityTransformService transformService;
    private void ActivateSplinePointEditing(SplineComponent splineComponent, int controlPointIndex, SplinePointEditingSelectionType editingSelectionType)
    {
        bool hasChangedActiveSpline = activeSplineComponent != splineComponent;
        activeSplineComponent = splineComponent;
        if (hasChangedActiveSpline)
        {
            activeSplineComponent.SplinePropertyChanged += OnSplineChanged;
            activeSplineComponent.ControlPointsChanged += OnSplineChanged;
        }

        activeControlPointIndex = controlPointIndex;
        activePointEditingSelectionType = editingSelectionType;

        if (editingSelectionType != SplinePointEditingSelectionType.None)
        {
            pointTransformGizmo.IsEnabled = true;

            var splinePosition = activeSplineComponent.Entity.Transform.WorldMatrix.TranslationVector;
            activePointAnchorEntity.Transform.Position = splinePosition;

            var controlPoint = activeSplineComponent.Spline[activeControlPointIndex];
            UpdateAnchorEntityPosition(controlPoint);

            transformService ??= game.EditorServices.Get<EditorGameEntityTransformService>();
            if (transformService is not null)
            {
                transformService.ActiveTransformationGizmo.IsEnabled = false;
            }
        }
    }

    private void DeactivateEditSpline(bool deselectSpline = true)
    {
        if (deselectSpline)
        {
            activeSplineComponent.SplinePropertyChanged -= OnSplineChanged;
            activeSplineComponent.ControlPointsChanged -= OnSplineChanged;
            activeSplineComponent = null;
        }
        activeControlPointIndex = -1;
        activePointEditingSelectionType = SplinePointEditingSelectionType.None;
        activePointAnchorEntity.Scene = null;
        pointTransformGizmo.IsEnabled = false;

        for (int i = 0; i < controlPointGizmos.Count; i++)
        {
            var controlPointGizmo = controlPointGizmos[i];
            controlPointGizmo.IsEnabled = false;
            controlPointGizmo.GizmoRootEntity?.SetParent(null);
            controlPointGizmo.Dispose();
        }
        controlPointGizmos.Clear();

        isSplineChangedUpdateRequired = true;
    }

    private void UpdateAnchorEntityPosition(Engine.Splines.Models.SplineControlPoint controlPoint)
    {
        if (activePointEditingSelectionType == SplinePointEditingSelectionType.ControlPoint)
        {
            var selectedControlPosition = controlPoint.Position;
            Vector3.Transform(in selectedControlPosition, in activeSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activePointAnchorEntity.Transform.Position = anchorPosition;
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentIn)
        {
            var selectedControlPosition = controlPoint.TangentInPosition;
            Vector3.Transform(in selectedControlPosition, in activeSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activePointAnchorEntity.Transform.Position = anchorPosition;
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentOut)
        {
            var selectedControlPosition = controlPoint.TangentOutPosition;
            Vector3.Transform(in selectedControlPosition, in activeSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activePointAnchorEntity.Transform.Position = anchorPosition;
        }
    }

    private void OnSplineChanged(SplineComponent splineComponent)
    {
        isSplineChangedUpdateRequired = true;
    }

    private void AddControlPoint(Vector3 controlPointPosition)
    {
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Add spline Control Point"))
            {
                var splineEntityId = activeSplineComponent.Entity.Id;

                var controlPoint = new Engine.Splines.Models.SplineControlPoint
                {
                    Position = controlPointPosition,
                    TangentIn = Vector3.Zero,
                    TangentOut = Vector3.Zero,
                };
                strideEditorService.AddAssetComponentArrayDataByEntityId<SplineComponent>(splineEntityId, nameof(SplineComponent.Spline), "ControlPoints", controlPoint);
            }
        });
    }

    private bool TryGetMouseRay(out Ray mouseRay)
    {
        // Calculate the ray in the gizmo space
        var gizmoMatrix = splineEditingGizmoRootEntity.Transform.WorldMatrix;
        var gizmoWorldViewInverse = Matrix.Invert(gizmoMatrix * cameraService.ViewMatrix);

        // Check if the inverted View Matrix is valid (since it will be use for mouse picking, check the rootPosition vector only)
        var translationVec = gizmoWorldViewInverse.TranslationVector;
        if (float.IsNaN(translationVec.X)
            || float.IsNaN(translationVec.Y)
            || float.IsNaN(translationVec.Z))
        {
            mouseRay = default;
            return false;
        }

        mouseRay = EditorGameHelper.CalculateRayFromMousePosition(cameraService.Component, inputManager.MousePosition, gizmoWorldViewInverse);
        return true;
    }
}
