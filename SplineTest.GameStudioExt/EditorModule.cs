using SplineTest.GameStudioExt.StrideEditorExt;
using Stride.Assets.Presentation.AssetEditors.EntityHierarchyEditor.Game;
using Stride.Assets.Presentation.AssetEditors.SceneEditor.Game;
using Stride.Core;
using Stride.Core.Assets.Editor.Services;
using Stride.Core.Assets.Editor.ViewModel;
using Stride.Core.Diagnostics;
using Stride.Core.Extensions;
using Stride.Core.Reflection;
using Stride.Editor;
using Stride.Engine;
using System.Diagnostics;

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

            var splineEditorGizmoService = new EditorGameSplineEditorGizmoService(strideEditorService);
            sceneEditorGame.EditorServices.Add(splineEditorGizmoService);
            asyncDisposables.Add(splineEditorGizmoService);

            // HACK: forced to do a late service registration
            sceneEditorGame.Script.AddTask(async () =>
            {
                await splineEditorGizmoService.InitializeService(sceneEditorGame);
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
