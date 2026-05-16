// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.StrideEditorExt;
using SplineTest.Splines.Components;
using Stride.Assets.Presentation.AssetEditors.Gizmos;
using Stride.Assets.Presentation.AssetEditors.Gizmos.Spline;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Mathematics;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
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

    private TranslationGizmo nodeTangentTransformGizmo;

    public SplineComponent ActiveSplineComponent { get; private set; }
    private int activeNodeIndex = -1;
    private SplineNodeEditingSelectionType activeNodeEditingSelectionType;
    private Entity activeNodeTangentAnchorEntity = null;
    private readonly List<Entity> activeNodeTangentAnchorEntityList = [];   // Only used for nodeTangentTransformGizmo.ModifiedEntities
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
        entitySelectionService.SelectionUpdated += OnEntitySelectionService_SelectionUpdated;

        activeNodeTangentAnchorEntity = new Entity("Edit Node Tangent Anchor");     // Entity to be moved by the TranslationGizmo
        activeNodeTangentAnchorEntityList.Add(activeNodeTangentAnchorEntity);
        activeNodeTangentAnchorEntity.Scene = editorScene;

        nodeTangentTransformGizmo = new TranslationGizmo();
        nodeTangentTransformGizmo.Initialize(game.Services, editorScene);
        nodeTangentTransformGizmo.IsEnabled = false;    // Must disable AFTER Initialize
        nodeTangentTransformGizmo.AnchorEntity = activeNodeTangentAnchorEntity;
        nodeTangentTransformGizmo.TransformationEnded += OnTransformGizmo_TransformationEnded;
        nodeTangentTransformGizmo.ModifiedEntities = activeNodeTangentAnchorEntityList;

        game.Script.AddTask(OnGameUpdate, priority: 1000);

        return Task.FromResult(true);
    }

    private void OnEntitySelectionService_SelectionUpdated(object sender, EntitySelectionEventArgs e)
    {
        var activeSplineEntity = ActiveSplineComponent?.Entity;
        if (activeSplineEntity is not null
            && !e.NewSelection.Contains(activeSplineEntity))
        {
            DeactivateEditNodeTangent();
        }
        else if (e.NewSelection.Count == 1)
        {
            var selectedEntity = e.NewSelection.First();
            var splineComp = selectedEntity.Get<SplineComponent>();
            if (splineComp is not null && activeSplineEntity != selectedEntity)
            {
                if (activeSplineEntity is not null)
                {
                    DeactivateEditNodeTangent();
                }
                ActivateSplineNodeEditing(splineComp, nodeIndex: -1, editingSelectionType: SplineNodeEditingSelectionType.None);
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

                var node = ActiveSplineComponent.Spline[activeNodeIndex];
                strideEditorService.UpdateAssetComponentArrayDataByEntityId<SplineComponent>(splineEntityId, nameof(SplineComponent.Spline), "SplineNodes", node, activeNodeIndex);
            }
        });
    }

    public override ValueTask DisposeAsync()
    {
        if (ActiveSplineComponent is not null)
        {
            DeactivateEditNodeTangent();
        }
        entitySelectionService?.SelectionUpdated -= OnEntitySelectionService_SelectionUpdated;
        return base.DisposeAsync();
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
                    if (activeNodeIndex >= ActiveSplineComponent.Spline.Count)
                    {
                        DeactivateEditNodeTangent(deselectSpline: false);
                    }
                    else if (activeNodeIndex >= 0)
                    {
                        refreshAnchorPosition = true;   // Refresh changes in the next update
                    }

                    isSplineChangedUpdateRequired = false;
                }

                if (nodeTangentTransformGizmo.IsEnabled && activeNodeIndex < ActiveSplineComponent.Spline.Count)
                {
                    var splinePosition = ActiveSplineComponent.Entity.Transform.WorldMatrix.TranslationVector;

                    var node = ActiveSplineComponent.Spline[activeNodeIndex];
                    if (refreshAnchorPosition)
                    {
                        activeNodeTangentAnchorEntity.Transform.Position = splinePosition;
                        if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.SplineNode)
                        {
                            activeNodeTangentAnchorEntity.Transform.Position += node.Position;
                        }
                        else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentIn)
                        {
                            activeNodeTangentAnchorEntity.Transform.Position += node.TangentInPosition;
                        }
                        else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentOut)
                        {
                            activeNodeTangentAnchorEntity.Transform.Position += node.TangentOutPosition;
                        }
                        refreshAnchorPosition = false;
                    }

                    await nodeTangentTransformGizmo.Update();

                    var anchorEntityPos = activeNodeTangentAnchorEntity.Transform.Position;
                    var anchorLocalPos = anchorEntityPos - splinePosition;
                    if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.SplineNode)
                    {
                        if (node.Position != anchorLocalPos)
                        {
                            var posOffset = anchorLocalPos - node.Position;
                            node.Position = anchorLocalPos;
                            node.TangentInPosition += posOffset;
                            node.TangentOutPosition += posOffset;
                            ActiveSplineComponent.Spline[activeNodeIndex] = node;
                        }
                    }
                    else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentIn)
                    {
                        if (node.TangentInPosition != anchorLocalPos)
                        {
                            node.TangentInPosition = anchorLocalPos;
                            // Mirror the other handle
                            var nodeToTangentHandlePos = anchorLocalPos - node.Position;
                            node.TangentOutPosition = node.Position - nodeToTangentHandlePos;
                            ActiveSplineComponent.Spline[activeNodeIndex] = node;
                        }
                    }
                    else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentOut)
                    {
                        if (node.TangentOutPosition != anchorLocalPos)
                        {
                            node.TangentOutPosition = anchorLocalPos;
                            // Mirror the other handle
                            var nodeToTangentHandlePos = anchorLocalPos - node.Position;
                            node.TangentInPosition = node.Position - nodeToTangentHandlePos;
                            ActiveSplineComponent.Spline[activeNodeIndex] = node;
                        }
                    }
                }
            }

            await game.Script.NextFrame();
        }
    }

    private void DeactivateEditNodeTangent(bool deselectSpline = true)
    {
        if (deselectSpline)
        {
            ActiveSplineComponent.SplineNodeChanged -= OnSplineChanged;
            ActiveSplineComponent.RenderSettingsChanged -= OnSplineRenderSettingsChanged;
            ActiveSplineComponent = null;
        }
        activeNodeIndex = -1;
        activeNodeEditingSelectionType = SplineNodeEditingSelectionType.None;
        activeNodeTangentAnchorEntity.Scene = null;
        nodeTangentTransformGizmo.IsEnabled = false;
    }

    private EditorGameEntityTransformService transformService;
    internal void ActivateSplineNodeEditing(SplineComponent splineComponent, int nodeIndex, SplineNodeEditingSelectionType editingSelectionType)
    {
        bool hasChangedActiveSpline = ActiveSplineComponent != splineComponent;
        ActiveSplineComponent = splineComponent;
        if (hasChangedActiveSpline)
        {
            ActiveSplineComponent.SplineNodeChanged += OnSplineChanged;
            ActiveSplineComponent.RenderSettingsChanged += OnSplineRenderSettingsChanged;
        }

        activeNodeIndex = nodeIndex;
        activeNodeEditingSelectionType = editingSelectionType;

        if (editingSelectionType != SplineNodeEditingSelectionType.None)
        {
            nodeTangentTransformGizmo.IsEnabled = true;

            var splinePosition = ActiveSplineComponent.Entity.Transform.WorldMatrix.TranslationVector;
            activeNodeTangentAnchorEntity.Transform.Position = splinePosition;

            var node = ActiveSplineComponent.Spline[activeNodeIndex];
            if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.SplineNode)
            {
                activeNodeTangentAnchorEntity.Transform.Position += node.Position;
            }
            else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentIn)
            {
                activeNodeTangentAnchorEntity.Transform.Position += node.TangentInPosition;
            }
            else if (activeNodeEditingSelectionType == SplineNodeEditingSelectionType.TangentOut)
            {
                activeNodeTangentAnchorEntity.Transform.Position += node.TangentOutPosition;
            }

            transformService ??= game.EditorServices.Get<EditorGameEntityTransformService>();
            if (transformService is not null)
            {
                transformService.ActiveTransformationGizmo.IsEnabled = false;
            }
        }
    }

    private void OnSplineChanged(SplineComponent splineComponent)
    {
        isSplineChangedUpdateRequired = true;
    }

    private void OnSplineRenderSettingsChanged(SplineComponent splineComponent)
    {
        // ?
    }

    public void AddSplineNode(Vector3 splineNodePosition)
    {
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Add Spline Node"))
            {
                var splineEntityId = ActiveSplineComponent.Entity.Id;

                var node = new Engine.Splines.Models.SplineNode
                {
                    Position = splineNodePosition,
                    TangentInPosition = splineNodePosition,
                    TangentOutPosition = splineNodePosition,
                };
                strideEditorService.AddAssetComponentArrayDataByEntityId<SplineComponent>(splineEntityId, nameof(SplineComponent.Spline), "SplineNodes", node);
            }
        });
    }
}
