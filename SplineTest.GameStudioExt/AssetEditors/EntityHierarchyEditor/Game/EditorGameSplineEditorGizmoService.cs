// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.StrideEditorExt;
using SplineTest.Splines.Rendering.GizmoMarker;
using SplineTest.Splines.Rendering.LineVisualizer;
using Stride.Assets.Presentation.AssetEditors.Gizmos;
using Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Rendering;

namespace Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;

/// <summary>
/// A class that manages selection in the entity hierarchy editor. It provides methods to modify the selection from the game thread
/// and handles changes in the selection that occurs in the view model.
/// </summary>
public class EditorGameSplineEditorGizmoService : EditorGameServiceBase
{
    private StrideEditorService strideEditorService;

    private EntityHierarchyEditorGame game;
    private Scene editorScene;
    private IEditorGameEntitySelectionService entitySelectionService;

    private TranslationGizmo tangentTransformGizmo;

    public SplineComponent ActiveSplineComponent { get; private set; }
    private int activeControlPointIndex = -1;
    private SplineControlPointEditingSelectionType activeControlPointEditingSelectionType;
    private Entity activeTangentAnchorEntity = null;
    private readonly List<Entity> activeTangentAnchorEntityList = [];   // Only used for tangentTransformGizmo.ModifiedEntities
    private bool refreshAnchorPosition = false;

    private bool isSplineChangedUpdateRequired = false;

    public EditorGameSplineEditorGizmoService(StrideEditorService strideEditorService)
    {
        this.strideEditorService = strideEditorService;
    }

    protected override Task<bool> Initialize([NotNull] EditorServiceGame editorGame)
    {
        game = (EntityHierarchyEditorGame)editorGame;
        editorScene = game.EditorScene;

        entitySelectionService = game.EditorServices.Get<IEditorGameEntitySelectionService>();
        entitySelectionService?.SelectionUpdated += OnEntitySelectionService_SelectionUpdated;

        activeTangentAnchorEntity = new Entity("Edit Control Point Tangent Anchor");     // Entity to be moved by the TranslationGizmo
        activeTangentAnchorEntityList.Add(activeTangentAnchorEntity);
        activeTangentAnchorEntity.Scene = editorScene;

        tangentTransformGizmo = new TranslationGizmo();
        tangentTransformGizmo.Initialize(game.Services, editorScene);
        tangentTransformGizmo.IsEnabled = false;    // Must disable AFTER Initialize
        tangentTransformGizmo.AnchorEntity = activeTangentAnchorEntity;
        tangentTransformGizmo.TransformationEnded += OnTransformGizmo_TransformationEnded;
        tangentTransformGizmo.ModifiedEntities = activeTangentAnchorEntityList;

        game.Script.AddTask(OnGameUpdate, priority: 1000);

        return Task.FromResult(true);
    }

    private void OnEntitySelectionService_SelectionUpdated(object sender, EntitySelectionEventArgs e)
    {
        var activeSplineEntity = ActiveSplineComponent?.Entity;
        if (activeSplineEntity is not null
            && !e.NewSelection.Contains(activeSplineEntity))
        {
            DeactivateEditTangent();
        }
        else if (e.NewSelection.Count == 1)
        {
            var selectedEntity = e.NewSelection.First();
            var splineComp = selectedEntity.Get<SplineComponent>();
            if (splineComp is not null && activeSplineEntity != selectedEntity)
            {
                if (activeSplineEntity is not null)
                {
                    DeactivateEditTangent();
                }
                ActivateSplineControlPointEditing(splineComp, controlPointIndex: -1, editingSelectionType: SplineControlPointEditingSelectionType.None);
            }
        }
    }

    private void OnTransformGizmo_TransformationEnded(object sender, EventArgs e)
    {
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Update Tangent Position"))
            {
                var splineEntityId = ActiveSplineComponent.Entity.Id;

                var controlPoint = ActiveSplineComponent.Spline[activeControlPointIndex];
                strideEditorService.UpdateAssetComponentArrayDataByEntityId<SplineComponent>(splineEntityId, nameof(SplineComponent.Spline), nameof(Engine.Splines.Models.Spline.ControlPoints), controlPoint, activeControlPointIndex);
            }
        });
    }

    public override ValueTask DisposeAsync()
    {
        if (ActiveSplineComponent is not null)
        {
            DeactivateEditTangent();
        }
        entitySelectionService?.SelectionUpdated -= OnEntitySelectionService_SelectionUpdated;
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

                if (isSplineChangedUpdateRequired)
                {
                    if (activeControlPointIndex >= ActiveSplineComponent.Spline.Count)
                    {
                        DeactivateEditTangent(deselectSpline: false);
                    }
                    else if (activeControlPointIndex >= 0)
                    {
                        refreshAnchorPosition = true;   // Refresh changes in the next update
                    }

                    isSplineChangedUpdateRequired = false;
                }

                if (tangentTransformGizmo.IsEnabled && activeControlPointIndex < ActiveSplineComponent.Spline.Count)
                {
                    var splinePosition = ActiveSplineComponent.Entity.Transform.WorldMatrix.TranslationVector;

                    var controlPoint = ActiveSplineComponent.Spline[activeControlPointIndex];
                    if (refreshAnchorPosition)
                    {
                        UpdateAnchorEntityPosition(controlPoint);
                        refreshAnchorPosition = false;
                    }

                    await tangentTransformGizmo.Update();

                    var anchorEntityPos = activeTangentAnchorEntity.Transform.Position;
                    Matrix.Invert(ref ActiveSplineComponent.Entity.Transform.WorldMatrix, out var splineWorldInverseMatrix);
                    Vector3.Transform(in anchorEntityPos, in splineWorldInverseMatrix, out Vector3 anchorLocalPos);
                    if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint)
                    {
                        if (controlPoint.Position != anchorLocalPos)
                        {
                            controlPoint.Position = anchorLocalPos;
                            ActiveSplineComponent.Spline[activeControlPointIndex] = controlPoint;
                        }
                    }
                    else if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.TangentIn)
                    {
                        if (controlPoint.TangentInPosition != anchorLocalPos)
                        {
                            controlPoint.TangentIn = anchorLocalPos - controlPoint.Position;
                            // Mirror the other handle
                            controlPoint.TangentOut = -controlPoint.TangentIn;
                            ActiveSplineComponent.Spline[activeControlPointIndex] = controlPoint;
                        }
                    }
                    else if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.TangentOut)
                    {
                        if (controlPoint.TangentOutPosition != anchorLocalPos)
                        {
                            controlPoint.TangentOut = anchorLocalPos - controlPoint.Position;
                            // Mirror the other handle
                            controlPoint.TangentIn = -controlPoint.TangentOut;
                            ActiveSplineComponent.Spline[activeControlPointIndex] = controlPoint;
                        }
                    }
                }
            }

            await game.Script.NextFrame();
        }
    }

    private void DeactivateEditTangent(bool deselectSpline = true)
    {
        if (deselectSpline)
        {
            ActiveSplineComponent.ControlPointsChanged -= OnSplineChanged;
            ActiveSplineComponent = null;
        }
        activeControlPointIndex = -1;
        activeControlPointEditingSelectionType = SplineControlPointEditingSelectionType.None;
        activeTangentAnchorEntity.Scene = null;
        tangentTransformGizmo.IsEnabled = false;
    }

    private EditorGameEntityTransformService transformService;
    internal void ActivateSplineControlPointEditing(SplineComponent splineComponent, int controlPointIndex, SplineControlPointEditingSelectionType editingSelectionType)
    {
        bool hasChangedActiveSpline = ActiveSplineComponent != splineComponent;
        ActiveSplineComponent = splineComponent;
        if (hasChangedActiveSpline)
        {
            ActiveSplineComponent.ControlPointsChanged += OnSplineChanged;
        }

        activeControlPointIndex = controlPointIndex;
        activeControlPointEditingSelectionType = editingSelectionType;

        if (editingSelectionType != SplineControlPointEditingSelectionType.None)
        {
            tangentTransformGizmo.IsEnabled = true;

            var splinePosition = ActiveSplineComponent.Entity.Transform.WorldMatrix.TranslationVector;
            activeTangentAnchorEntity.Transform.Position = splinePosition;

            var controlPoint = ActiveSplineComponent.Spline[activeControlPointIndex];
            UpdateAnchorEntityPosition(controlPoint);

            transformService ??= game.EditorServices.Get<EditorGameEntityTransformService>();
            if (transformService is not null)
            {
                transformService.ActiveTransformationGizmo.IsEnabled = false;
            }
        }
    }

    private void UpdateAnchorEntityPosition(Engine.Splines.Models.SplineControlPoint controlPoint)
    {
        if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.ControlPoint)
        {
            var selectedControlPosition = controlPoint.Position;
            Vector3.Transform(in selectedControlPosition, in ActiveSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activeTangentAnchorEntity.Transform.Position = anchorPosition;
        }
        else if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.TangentIn)
        {
            var selectedControlPosition = controlPoint.TangentInPosition;
            Vector3.Transform(in selectedControlPosition, in ActiveSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activeTangentAnchorEntity.Transform.Position = anchorPosition;
        }
        else if (activeControlPointEditingSelectionType == SplineControlPointEditingSelectionType.TangentOut)
        {
            var selectedControlPosition = controlPoint.TangentOutPosition;
            Vector3.Transform(in selectedControlPosition, in ActiveSplineComponent.Entity.Transform.WorldMatrix, out Vector3 anchorPosition);
            activeTangentAnchorEntity.Transform.Position = anchorPosition;
        }
    }

    private void OnSplineChanged(SplineComponent splineComponent)
    {
        isSplineChangedUpdateRequired = true;
    }

    public void AddControlPoint(Vector3 controlPointPosition)
    {
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Add Spline Control Point"))
            {
                var splineEntityId = ActiveSplineComponent.Entity.Id;

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
}
