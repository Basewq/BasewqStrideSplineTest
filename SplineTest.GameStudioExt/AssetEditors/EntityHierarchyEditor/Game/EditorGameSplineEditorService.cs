// Copyright (c) .NET Foundation and Contributors (https://dotnetfoundation.org/ & https://stride3d.net)
// Distributed under the MIT license. See the LICENSE.md file in the project root for more information.

using SplineTest.GameStudioExt.Assets;
using SplineTest.GameStudioExt.StrideEditorExt;
using SplineTest.GameStudioExt.StrideEditorExt.StrideAssetTransaction;
using SplineTest.Rendering;
using SplineTest.Splines.Rendering.GizmoMarker;
using SplineTest.Splines.Rendering.LineVisualizer;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Services;
using Stride.Assets.Presentation.AssetEditors.Gizmos;
using Stride.Assets.Presentation.AssetEditors.Gizmos.Splines;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Services;
using Stride.Assets.Presentation.ViewModel;
using Stride.Core;
using Stride.Core.Annotations;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Assets.Quantum;
using Stride.Core.Mathematics;
using Stride.Core.Presentation.Services;
using Stride.Core.Quantum;
using Stride.Editor.EditorGame.Game;
using Stride.Engine;
using Stride.Engine.Splines.Components;
using Stride.Engine.Splines.Models;
using Stride.Games;
using Stride.Input;
using Stride.Rendering;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;

/// <summary>
/// A class that manages spline control & tangent points editing.
/// </summary>
public class EditorGameSplineEditorService : EditorGameServiceBase
{
    private static readonly Color4 CurveColor = Color.Aqua.ToColor4();

    private readonly StrideEditorService strideEditorService;
    private readonly SceneEditorController sceneEditorController;
    private Interaction inputInteraction;
    private bool isInputInteractionFinished;

    private EntityHierarchyEditorGame game;
    private Scene editorScene;
    private IUndoRedoService undoRedoService;
    private AssetPropertyGraph scenePropertyGraph;

    private InputManager inputManager;
    private IEditorGameEntitySelectionService entitySelectionService;
    private IStrideEditorMouseService editorMouseService;
    private IEditorGameCameraService cameraService;
    private IEditorGameComponentGizmoService editorGameComponentGizmoService;

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
        typeof(IEditorGameCameraService),
        typeof(IEditorGameComponentGizmoService)
    ];

    public EditorGameSplineEditorService(StrideEditorService strideEditorService, SceneEditorController sceneEditorController)
    {
        this.strideEditorService = strideEditorService;
        this.sceneEditorController = sceneEditorController;
    }

    protected override Task<bool> Initialize([NotNull] EditorServiceGame editorGame)
    {
        if (IsInitialized)
        {
            return Task.FromResult(true);
        }
        game = (EntityHierarchyEditorGame)editorGame;
        editorScene = game.EditorScene;

        undoRedoService = SessionViewModel.Instance.UndoRedoService;
        inputManager = game.Services.GetService<InputManager>();
        entitySelectionService = game.EditorServices.Get<IEditorGameEntitySelectionService>();
        entitySelectionService?.SelectionUpdated += OnEntitySelectionService_SelectionUpdated;
        editorMouseService = StrideEditorMouseService.GetOrCreate(game.Services);

        splineEditingGizmoRootEntity = new Entity("Spline Editing Root");
        editorScene.Entities.Add(splineEditingGizmoRootEntity);

        activePointAnchorEntity = new Entity("Edit Spline Point Anchor");     // Entity to be moved by the TranslationGizmo
        activePointAnchorEntityList.Add(activePointAnchorEntity);
        activePointAnchorEntity.Scene = editorScene;

        pointTransformGizmo = new TranslationGizmo();
        pointTransformGizmo.Initialize(game.Services, editorScene);
        pointTransformGizmo.IsEnabled = false;    // Must disable AFTER Initialize
        pointTransformGizmo.AnchorEntity = activePointAnchorEntity;
        pointTransformGizmo.TransformationEnded += OnTransformGizmo_TransformationEnded;
        pointTransformGizmo.ModifiedEntities = activePointAnchorEntityList;

        game.Script.AddTask(OnGameUpdate, priority: 1000);

        // HACK: Take AssetViewModel/SceneViewModel from game controller because we can't get it ourselves
        {
            var getAssetViewModel_FieldInfo = typeof(EditorGameController<EntityHierarchyEditorGame>).GetField("Asset", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var sceneViewModel = getAssetViewModel_FieldInfo.GetValue(sceneEditorController) as SceneViewModel;

            scenePropertyGraph = sceneViewModel.PropertyGraph;
            scenePropertyGraph.Changed += OnScenePropertyGraphChanged;
            scenePropertyGraph.ItemChanged += OnScenePropertyGraphItemChanged;
        }

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
            using (strideEditorService.CreateUndoRedoTransaction("Update Spline " + activePointEditingSelectionType.ToString()))
            {
                var runtimeSpline = activeSplineComponent.Spline;
                var assetSplineComp = strideEditorService.GetAssetComponent(activeSplineComponent);
                var assetSpline = assetSplineComp.Spline;

                var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                if (runtimeSpline.TryGetPreviousControlPointIndex(activeControlPointIndex, out int prevIndex))
                {
                    assetSpline[prevIndex] = runtimeSpline[prevIndex];
                }
                assetSpline[activeControlPointIndex] = runtimeSpline[activeControlPointIndex];
                if (runtimeSpline.TryGetNextControlPointIndex(activeControlPointIndex, out int nextIndex))
                {
                    assetSpline[nextIndex] = runtimeSpline[nextIndex];
                }

                var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                assetTransactionBuilder.RevertAssetState(nodeContainer);
                assetTransaction.Execute();
            }
        });
    }

    private void OnScenePropertyGraphChanged(object sender, AssetMemberNodeChangeEventArgs e)
    {
        if (undoRedoService.TransactionInProgress)
        {
            string memberName = e.Member.Name;
            if (e.ChangeType == ContentChangeType.ValueChange)
            {
                if (IsControlPointModification(e.Member, out var assetSplineComp, out int controlPointIndex))
                {
                    if (memberName == nameof(SplineControlPoint.Position))
                    {
                        var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                        bool hasChanged = TryUpdateAutoTangents(assetSplineComp.Spline, controlPointIndex);
                        if (hasChanged)
                        {
                            var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                            var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                            assetTransactionBuilder.RevertAssetState(nodeContainer);
                            assetTransaction.Execute();
                        }
                    }
                    else if (memberName == nameof(SplineControlPoint.TangentIn)
                        || memberName == nameof(SplineControlPoint.TangentOut))
                    {
                        var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                        var spline = assetSplineComp.Spline;
                        bool isTangentIn = memberName == nameof(SplineControlPoint.TangentIn);
                        bool hasChanged = TryUpdateTangentsConstraint(isTangentIn, spline, controlPointIndex);
                        if (hasChanged)
                        {
                            var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                            var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                            assetTransactionBuilder.RevertAssetState(nodeContainer);
                            assetTransaction.Execute();
                        }
                    }
                    else if (memberName == nameof(SplineControlPoint.Roll)
                        || memberName == nameof(SplineControlPoint.OverrideUpDirection))
                    {
                        // No real additional modifications, just reevaluated the curve
                        isSplineChangedUpdateRequired = true;
                    }
                    else if (memberName == nameof(SplineControlPoint.Type))
                    {
                        var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                        var spline = assetSplineComp.Spline;
                        var newControlPointType = (SplineControlPointType)e.NewValue;
                        bool hasChanged = false;
                        switch (newControlPointType)
                        {
                            case SplineControlPointType.Auto:
                                hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, controlPointIndex);
                                break;
                            case SplineControlPointType.Linear:
                                hasChanged = TryUpdateLinearTangents(spline, controlPointIndex);
                                break;
                            case SplineControlPointType.Mirrored:
                            case SplineControlPointType.Aligned:
                                bool isTangentIn = false;       // Arbitrary handle to pick...
                                hasChanged = TryUpdateTangentsConstraint(isTangentIn, spline, controlPointIndex);
                                break;
                            case SplineControlPointType.Free:
                            default:
                                // Nothing
                                break;
                        }
                        if (hasChanged)
                        {
                            var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                            var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                            assetTransactionBuilder.RevertAssetState(nodeContainer);
                            assetTransaction.Execute();
                        }
                        isSplineChangedUpdateRequired = true;
                    }
                }
            }
        }
    }

    private void OnScenePropertyGraphItemChanged(object sender, AssetItemNodeChangeEventArgs e)
    {
        if (undoRedoService.TransactionInProgress)
        {
            if (e.ChangeType == ContentChangeType.CollectionRemove)
            {
                if (e.OldValue is SplineControlPoint
                    && TryGetSplineByModifiedControlPoints(e.Collection, e.Index, out var assetSplineComp, out int removedControlPointIndex))
                {
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                    var spline = assetSplineComp.Spline;
                    bool hasChanged = false;
                    if (spline.TryGetPreviousControlPointIndex(removedControlPointIndex, out int prevIndex))
                    {
                        if (spline[prevIndex].Type == SplineControlPointType.Auto)
                        {
                            hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, prevIndex) || hasChanged;
                        }
                    }
                    if (spline.TryGetNextControlPointIndex(removedControlPointIndex - 1, out int nextIndex) && nextIndex != prevIndex)
                    {
                        if (spline[nextIndex].Type == SplineControlPointType.Auto)
                        {
                            hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, nextIndex) || hasChanged;
                        }
                    }
                    if (hasChanged)
                    {
                        var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                        var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                        assetTransactionBuilder.RevertAssetState(nodeContainer);
                        assetTransaction.Execute();
                    }
                }
            }
            else if (e.ChangeType == ContentChangeType.CollectionAdd)
            {
                if (e.NewValue is SplineControlPoint
                    && TryGetSplineByModifiedControlPoints(e.Collection, e.Index, out var assetSplineComp, out int addedControlPointIndex))
                {
                    var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                    var spline = assetSplineComp.Spline;
                    bool hasChanged = TryUpdateAutoTangents(spline, addedControlPointIndex);
                    if (hasChanged)
                    {
                        var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                        var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                        assetTransactionBuilder.RevertAssetState(nodeContainer);
                        assetTransaction.Execute();
                    }
                }
            }
        }
    }

    private bool IsControlPointModification(
        IMemberNode memberNode,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SplineComponent? assetSplineComponent,
        out int controlPointIndex)
    {
        assetSplineComponent = null;
        controlPointIndex = -1;

        if (memberNode.Parent.Type != typeof(SplineControlPoint))
        {
            return false;
        }
        string memberName = memberNode.Name;
        if (memberName != nameof(SplineControlPoint.Position)
            && memberName != nameof(SplineControlPoint.TangentIn)
            && memberName != nameof(SplineControlPoint.TangentOut)
            && memberName != nameof(SplineControlPoint.Roll)
            && memberName != nameof(SplineControlPoint.OverrideUpDirection)
            && memberName != nameof(SplineControlPoint.Scale)
            && memberName != nameof(SplineControlPoint.Type))
        {
            return false;
        }

        var nodePathFinderGraphVisitor = new NodePathFinderGraphVisitor(memberNode);
        nodePathFinderGraphVisitor.Visit(scenePropertyGraph.RootNode);
        if (nodePathFinderGraphVisitor.FoundPath is null)
        {
            return false;
        }
        var subPaths = nodePathFinderGraphVisitor.FoundPath.Decompose();
        // SplineComponent.Spline.ControlPoints[i].Position -> at least 5 sub-paths required
        if (subPaths.Count < 5 || subPaths[^1].MemberDescriptor?.DeclaringType != typeof(SplineControlPoint))
        {
            return false;
        }
        // This really is SplineControlPoint.Property being edited
        var splineCompObjPath = nodePathFinderGraphVisitor.FoundPath.Clone();
        splineCompObjPath.Pop();    // SplineComponent.Spline.ControlPoints[i]
        controlPointIndex = (int)splineCompObjPath.GetIndex();
        splineCompObjPath.Pop();    // SplineComponent.Spline.ControlPoints
        splineCompObjPath.Pop();    // SplineComponent.Spline
        splineCompObjPath.Pop();    // SplineComponent

        assetSplineComponent = splineCompObjPath.GetValue(scenePropertyGraph.RootNode.Retrieve()) as SplineComponent;
        return assetSplineComponent is not null;
    }

    private bool TryGetSplineByModifiedControlPoints(
        IObjectNode collection, NodeIndex nodeIndex,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out SplineComponent? assetSplineComponent,
        out int controlPointIndex)
    {
        assetSplineComponent = null;
        controlPointIndex = -1;

        if (!nodeIndex.IsInt)
        {
            return false;
        }
        controlPointIndex = nodeIndex.Int;

        var nodePathFinderGraphVisitor = new NodePathFinderGraphVisitor(collection);
        nodePathFinderGraphVisitor.Visit(scenePropertyGraph.RootNode);
        if (nodePathFinderGraphVisitor.FoundPath is null)
        {
            return false;
        }
        var subPaths = nodePathFinderGraphVisitor.FoundPath.Decompose();
        // SplineComponent.Spline.ControlPoints -> at least 3 sub-paths required
        if (subPaths.Count < 3 || subPaths[^1].MemberDescriptor?.Name != nameof(Spline.ControlPoints))
        {
            return false;
        }
        // This really is SplineComponent.Spline.ControlPoints
        var splineCompObjPath = nodePathFinderGraphVisitor.FoundPath.Clone();
        splineCompObjPath.Pop();    // SplineComponent.Spline
        splineCompObjPath.Pop();    // SplineComponent

        assetSplineComponent = splineCompObjPath.GetValue(scenePropertyGraph.RootNode.Retrieve()) as SplineComponent;
        return assetSplineComponent is not null;
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
        scenePropertyGraph?.Changed -= OnScenePropertyGraphChanged;
        scenePropertyGraph?.ItemChanged -= OnScenePropertyGraphItemChanged;
        scenePropertyGraph = null;

        return base.DisposeAsync();
    }

    public override void UpdateGraphicsCompositor(EditorServiceGame game)
    {
        base.UpdateGraphicsCompositor(game);

        if (game is SceneEditorGame sceneEditorGame)
        {
            var gfxComp = sceneEditorGame.EditorSceneSystem?.GraphicsCompositor;
            var fwdRenderer = gfxComp?.Editor as Stride.Rendering.Compositing.ForwardRenderer;
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
                            var activeCtrlPoint = activeSplineComponent.Spline.ControlPoints[activeControlPointIndex];
                            bool isEditingTangent = activePointEditingSelectionType == SplinePointEditingSelectionType.TangentIn
                                                    || activePointEditingSelectionType == SplinePointEditingSelectionType.TangentOut;
                            if (isEditingTangent && !activeCtrlPoint.Type.IsTangentUserControllable())
                            {
                                DeactivateEditSpline(deselectSpline: false);
                            }
                            else
                            {
                                refreshAnchorPosition = true;   // Refresh changes in the next update
                            }
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
            SplineExtensions.CollectSplineSamplePositionsByResolution(activeSplineComponent.Spline, splineSamplePoints, sampleResolutionPerCurve: 64);
        }

        var lineVisualizerComponent = splineEditingGizmoRootEntity.Get<LineVisualizerComponent>();
        if (lineVisualizerComponent is null)
        {
            lineVisualizerComponent = new LineVisualizerComponent();
            lineVisualizerComponent.LineSet.OccludedStyle = LineOccludedStyle.Checkered;
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

        for (int i = 0; i < controlPointGizmos.Count; i++)
        {
            var controlPointGizmo = controlPointGizmos[i];
            controlPointGizmo.InvalidateVisual();
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

        bool isShiftKeyDown = inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift);
        bool isAltKeyDown = inputManager.IsKeyDown(Keys.LeftAlt) || inputManager.IsKeyDown(Keys.RightAlt);
        bool isCtrlKeyDown = inputManager.IsKeyDown(Keys.LeftCtrl) || inputManager.IsKeyDown(Keys.RightCtrl);
        // Hover
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
            else if (mouseHoverControlPointGizmo is not null
                && mouseHoverControlPointGizmo.EditingSelectionType != raycastHitControlPointEditingSelectionType)
            {
                mouseHoverControlPointGizmo.EditingSelectionType = raycastHitControlPointEditingSelectionType;
                mouseHoverControlPointGizmo.InvalidateVisual();
            }
        }

        bool canControlMouse = editorMouseService.IsMouseAvailable;
        bool isControllingMouse = canControlMouse && (isShiftKeyDown || isAltKeyDown || isCtrlKeyDown);

        if (canControlMouse && inputManager.IsMouseButtonPressed(MouseButton.Left))
        {
            editorGameComponentGizmoService ??= Services.Get<IEditorGameComponentGizmoService>();
            var gizmoCompEnity = editorGameComponentGizmoService.GetContentEntityUnderMouse();

            if (gizmoCompEnity is null && inputInteraction is null && !isAddingControlPoint)
            {
                inputInteraction = new Interaction(this);
                inputInteraction.Start();
            }
        }
        if (isInputInteractionFinished)
        {
            inputInteraction = null;
            isInputInteractionFinished = false;
            editorMouseService.SetIsControllingMouse(false, owner: this);
        }
        if (inputInteraction is not null)
        {
            bool isContinuing = inputInteraction.Update(game.UpdateTime);
            if (!isContinuing)
            {
                inputInteraction.End();
                isInputInteractionFinished = true;
            }
        }

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

        var spline = activeSplineComponent.Spline;
        var controlPoint = spline[activeControlPointIndex];
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
                TryMoveControlPoint(spline, activeControlPointIndex, anchorLocalPos);
            }
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentIn)
        {
            if (controlPoint.TangentInPosition != anchorLocalPos)
            {
                var newPosition = anchorLocalPos - controlPoint.Position;
                TryMoveTangentHandle(isTangentIn: true, spline, activeControlPointIndex, newPosition);
            }
        }
        else if (activePointEditingSelectionType == SplinePointEditingSelectionType.TangentOut)
        {
            if (controlPoint.TangentOutPosition != anchorLocalPos)
            {
                var newPosition = anchorLocalPos - controlPoint.Position;
                TryMoveTangentHandle(isTangentIn: false, spline, activeControlPointIndex, newPosition);
            }
        }
    }

    private static bool TryMoveControlPoint(Spline spline, int controlPointIndex, Vector3 newPosition)
    {
        var controlPoint = spline[controlPointIndex];
        bool hasChanged = false;
        SetIfChanged(ref controlPoint.Position, newPosition, ref hasChanged);
        if (!hasChanged)
        {
            return false;
        }

        spline[controlPointIndex] = controlPoint;
        TryUpdateAutoTangents(spline, controlPointIndex);
        return true;
    }

    private static bool TryMoveTangentHandle(bool isTangentIn, Spline spline, int controlPointIndex, Vector3 newPosition)
    {
        var controlPoint = spline[controlPointIndex];
        bool hasChanged = false;
        if (isTangentIn)
        {
            SetIfChanged(ref controlPoint.TangentIn, newPosition, ref hasChanged);
        }
        else
        {
            SetIfChanged(ref controlPoint.TangentOut, newPosition, ref hasChanged);
        }
        if (hasChanged)
        {
            spline[controlPointIndex] = controlPoint;
            TryUpdateTangentsConstraint(isTangentIn, spline, controlPointIndex);
        }
        return hasChanged;
    }

    private static bool TryUpdateAutoTangents(Spline spline, int centerControlPointIndex)
    {
        bool hasChanged = false;
        if (spline.TryGetPreviousControlPointIndex(centerControlPointIndex, out int prevIndex))
        {
            if (spline[prevIndex].Type == SplineControlPointType.Auto)
            {
                hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, prevIndex) || hasChanged;
            }
        }
        if (spline[centerControlPointIndex].Type == SplineControlPointType.Auto)
        {
            hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, centerControlPointIndex) || hasChanged;
        }
        if (spline.TryGetNextControlPointIndex(centerControlPointIndex, out int nextIndex))
        {
            if (spline[nextIndex].Type == SplineControlPointType.Auto)
            {
                hasChanged = TryUpdateAutoTangentsSingleControlPoint(spline, nextIndex) || hasChanged;
            }
        }
        return hasChanged;
    }

    private static bool TryUpdateAutoTangentsSingleControlPoint(Spline spline, int controlPointIndex)
    {
        Vector3? prevCtrlPointPosition = null;
        Vector3? nextCtrlPointPosition = null;

        if (spline.TryGetPreviousControlPointIndex(controlPointIndex, out int prevCtrlPointIndex))
        {
            prevCtrlPointPosition = spline[prevCtrlPointIndex].Position;
        }
        if (spline.TryGetNextControlPointIndex(controlPointIndex, out int nextCtrlPointIndex))
        {
            nextCtrlPointPosition = spline[nextCtrlPointIndex].Position;
        }
        var controlPoint = spline[controlPointIndex];

        SplineUtil.CalculateAutoTangents(
            controlPoint.Position, prevCtrlPointPosition, nextCtrlPointPosition, strength: SplineUtil.DefaultAutoTangentStrength,
            out var newTangentIn, out var newTangentOut);

        bool hasChanged = false;
        SetIfChanged(ref controlPoint.TangentIn, newTangentIn, ref hasChanged);
        SetIfChanged(ref controlPoint.TangentOut, newTangentOut, ref hasChanged);
        if (hasChanged)
        {
            spline[controlPointIndex] = controlPoint;
        }
        return hasChanged;
    }

    private static bool TryUpdateLinearTangents(Spline spline, int controlPointIndex)
    {
        var controlPoint = spline[controlPointIndex];
        bool hasChanged = false;
        // Tangents are not relevant for linear type, so just set to zero
        SetIfChanged(ref controlPoint.TangentIn, Vector3.Zero, ref hasChanged);
        SetIfChanged(ref controlPoint.TangentOut, Vector3.Zero, ref hasChanged);
        if (hasChanged)
        {
            spline[controlPointIndex] = controlPoint;
        }
        return hasChanged;
    }

    private static bool TryUpdateTangentsConstraint(bool isTangentInModified, Spline spline, int controlPointIndex)
    {
        bool hasChanged = false;
        var controlPoint = spline[controlPointIndex];
        switch (controlPoint.Type)
        {
            case SplineControlPointType.Mirrored:
                // Mirror the other handle
                if (isTangentInModified)
                {
                    SetIfChanged(ref controlPoint.TangentOut, -controlPoint.TangentIn, ref hasChanged);
                }
                else
                {
                    SetIfChanged(ref controlPoint.TangentIn, -controlPoint.TangentOut, ref hasChanged);
                }
                break;

            case SplineControlPointType.Aligned:
                if (isTangentInModified)
                {
                    var newTangentPosition = SplineUtil.CalculateAlignedHandle(controlPoint.TangentIn, controlPoint.TangentOut);
                    SetIfChanged(ref controlPoint.TangentOut, newTangentPosition, ref hasChanged);
                }
                else
                {
                    var newTangentPosition = SplineUtil.CalculateAlignedHandle(controlPoint.TangentOut, controlPoint.TangentIn);
                    SetIfChanged(ref controlPoint.TangentIn, newTangentPosition, ref hasChanged);
                }
                break;

            case SplineControlPointType.Auto:
            case SplineControlPointType.Linear:
            case SplineControlPointType.Free:
            default:
                // Not applicable
                return false;
        }

        if (hasChanged)
        {
            spline[controlPointIndex] = controlPoint;
        }
        return hasChanged;
    }

    private static void SetIfChanged<T>(ref T backingField, T newValue, ref bool hasChanged)
        where T : IEquatable<T>
    {
        if (!backingField.Equals(newValue))
        {
            backingField = newValue;
            hasChanged = true;
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

    private void UpdateAnchorEntityPosition(SplineControlPoint controlPoint)
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
        isAddingControlPoint = true;
        strideEditorService.Invoke(() =>
        {
            using (strideEditorService.CreateUndoRedoTransaction("Add Spline Control Point"))
            {
                // HACK: AssetTransactionBuilder is used to handle the asset change into the quantum system.
                // We do this by doing the following:
                // 1. Save the initial state of the *asset side* spline
                // 2. Modify the changes we want in the asset side data
                // 3. Create the transaction that will actually modify the asset (done by diff'ing the initial state from current state)
                // 4. Revert back to the initial state (required so it can be properly modified by the quantum system)
                // 5. Execute the transaction to participate properly within the quantum system
                var assetSplineComp = strideEditorService.GetAssetComponent(activeSplineComponent);
                var assetTransactionBuilder = AssetTransactionBuilder.Begin(assetSplineComp);

                var spline = assetSplineComp.Spline;
                var controlPoint = new SplineControlPoint
                {
                    Position = controlPointPosition,
                    Type = SplineControlPointType.Auto,
                };
                int controlPointIndex = assetSplineComp.Spline.Count;
                assetSplineComp.Spline.Add(controlPoint);
                TryUpdateAutoTangents(assetSplineComp.Spline, controlPointIndex);

                var nodeContainer = SessionViewModel.Instance.AssetNodeContainer;
                var assetTransaction = assetTransactionBuilder.CreateTransaction(nodeContainer);
                assetTransactionBuilder.RevertAssetState(nodeContainer);
                assetTransaction.Execute();
            }

            isAddingControlPoint = false;
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


    private class Interaction(EditorGameSplineEditorService EditorService) /*: IInputInteraction*/
    {
        public object Owner => EditorService;

        public void Start()
        {
            EditorService.editorMouseService.SetIsControllingMouse(true, owner: EditorService);
        }

        public bool Update(GameTime gameTime)
        {
            var inputManager = EditorService.inputManager;
            if (inputManager.IsMouseButtonDown(MouseButton.Left))
            {
                return true;
            }
            return false;
        }

        public void End()
        {
            System.Diagnostics.Debug.WriteLine($"{GetType().Name} End - {GetHashCode()}");
            var mouseHoverControlPointGizmo = EditorService.mouseHoverControlPointGizmo;

            var inputManager = EditorService.inputManager;
            bool isShiftKeyDown = inputManager.IsKeyDown(Keys.LeftShift) || inputManager.IsKeyDown(Keys.RightShift);

            var spline = EditorService.activeSplineComponent.Spline;
            if (isShiftKeyDown)
            {
                // Add new spline node
                if (EditorService.TryGetMouseRay(out var mouseRay))
                {
                    // Plane = Ax + By + Cz + D = 0 => Positive Height = Negative D
                    Plane plane;
                    if (spline.Count > 0)
                    {
                        // Raycast on XZ plane where height is the same as the last control point's height
                        var controlPoint = spline[spline.Count - 1];
                        plane = new Plane(Vector3.UnitY, d: -controlPoint.Position.Y);
                    }
                    else
                    {
                        // Raycast on XZ plane where height is the same as the last control point's height
                        var controlPoint = spline[spline.Count - 1];
                        plane = new Plane(Vector3.UnitY, d: -controlPoint.Position.Y);
                    }
                    if (CollisionHelper.RayIntersectsPlane(in mouseRay, in plane, out Vector3 hitPoint))
                    {
                        EditorService.AddControlPoint(hitPoint);
                    }
                }
            }
            else if (mouseHoverControlPointGizmo is not null)
            {
                // Try select existing spline node
                EditorService.ActivateSplinePointEditing(EditorService.activeSplineComponent, EditorService.mouseHoverControlPointIndex, mouseHoverControlPointGizmo.EditingSelectionType);
            }

            // HACK: we don't release mouse control yet
        }

        //public void Cancel()
        //{
        //}
    }
}
