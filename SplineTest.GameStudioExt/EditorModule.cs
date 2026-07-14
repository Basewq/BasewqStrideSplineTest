using SplineTest.GameStudioExt.StrideEditorExt;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.GameEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Services;
using Stride.Core;
using Stride.Core.Extensions;
using Stride.Core.Reflection;
using Stride.Engine;
using System.Diagnostics;
using System.Reflection;

namespace SplineTest.GameStudioExt;

internal class EditorModule
{
    [ModuleInitializer]
    public static void Initialize()
    {
        const string AssemblyCommonCategoriesName = "assets";
        // Can't use Stride.Core.Reflection.AssemblyCommonCategories.Assets because
        // this class is duplicated in two different dlls causing namespace conflict
        AssemblyRegistry.Register(typeof(EditorModule).Assembly, AssemblyCommonCategoriesName);

        Game.GameStarted += OnGameStarted;
        Game.GameDestroyed += OnGameDestroyed;
        //AssetsPlugin.RegisterPlugin(typeof(SplineEditorPlugin));
    }

    private static Dictionary<Game, List<IDisposable>> _gameToDisposables = [];
    private static Dictionary<Game, List<IAsyncDisposable>> _gameToAsyncDisposables = [];
    private static void OnGameStarted(object? sender, EventArgs e)
    {
        Debug.WriteLineIf(condition: true, $"OnGameStarted: {sender?.GetType().Name}");
        if (sender is not Game game)
        {
            return;
        }

        string gameTypeName = sender.GetType().Name;
        if (string.Equals(gameTypeName, "PreviewGame", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (sender is SceneEditorGame sceneEditorGame)
        {
            var disposables = new List<IDisposable>();
            _gameToDisposables[game] = disposables;
            var asyncDisposables = new List<IAsyncDisposable>();
            _gameToAsyncDisposables[game] = asyncDisposables;

            // Note that the editor can run multiple SceneEditorGames since these are the opened scene assets
            var strideEditorService = new StrideEditorService();
            game.Services.AddService<IStrideEditorService>(strideEditorService);
            var viewModelServiceProvider = strideEditorService.ViewModelServiceProvider;
            if (viewModelServiceProvider is null)
            {
                throw new Exception("ViewModelServiceProvider was not set.");
            }

            // HACK: Take IEditorGameController/SceneEditorController from another service because we can't get it ourselves
            var cameraService = sceneEditorGame.EditorServices.Get<IEditorGameCameraService>();
            var getEditorController_PropertyInfo = typeof(EditorGameCameraService).GetProperty("Controller", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            var getEditorController_MethodInfo = getEditorController_PropertyInfo.GetGetMethod(nonPublic: true);
            var sceneEditorController = getEditorController_MethodInfo?.Invoke(cameraService, []) as SceneEditorController;

            var splineEditorService = new EditorGameSplineEditorService(strideEditorService, sceneEditorController);
            sceneEditorGame.EditorServices.Add(splineEditorService);
            asyncDisposables.Add(splineEditorService);

            var splineMeshCompChangeWatcherService = new EditorGameSplineMeshComponentChangeWatcherService(sceneEditorController);
            sceneEditorGame.EditorServices.Add(splineMeshCompChangeWatcherService);
            asyncDisposables.Add(splineMeshCompChangeWatcherService);


            // HACK: forced to do a late service registration
            sceneEditorGame.Script.AddTask(async () =>
            {
                await splineEditorService.InitializeService(sceneEditorGame);
                await splineMeshCompChangeWatcherService.InitializeService(sceneEditorGame);
            });
        }
    }

    private static void OnGameDestroyed(object? sender, EventArgs e)
    {
        if (sender is not Game game)
        {
            return;
        }

        if (_gameToDisposables.Remove(game, out var disposables))
        {
            foreach (var disp in disposables)
            {
                disp.Dispose();
            }
        }
        if (_gameToAsyncDisposables.Remove(game, out var asyncDisposables))
        {
            Task.Run(async () =>
            {
                foreach (var disp in asyncDisposables)
                {
                    await disp.DisposeAsync();
                }
            }).Forget();
        }
    }
}

//internal class SplineEditorPlugin : StrideAssetsPlugin
//{
//    protected override void Initialize(ILogger logger)
//    {
//    }

//    /// <inheritdoc />
//    public override void InitializeSession([Stride.Core.Annotations.NotNull] SessionViewModel session)
//    {

//    }

//    public override void RegisterAssetPreviewViewTypes(IDictionary<Type, Type> assetPreviewViewTypes)
//    {
//    }
//}
