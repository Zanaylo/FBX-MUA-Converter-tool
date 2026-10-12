using System.Windows;
using System.Windows.Media.Media3D;
using FbxToMua.App.Viewer;
using FbxToMua.App.ViewModels;
using FbxToMua.Core.Export;
using FbxToMua.Core.Preview;

namespace FbxToMua.App.Services;

public sealed record ViewerRequest(string Key, string Title, StagePreview Preview, Reframe Current, StageLook OwnLook, Action<Reframe> Keep);

public interface IStageViewer
{
    bool Bring(string key);

    Task Open(ViewerRequest request);
}

public sealed class StageViewer : IStageViewer
{
    private readonly Dictionary<string, StageViewerWindow> _open = new(StringComparer.OrdinalIgnoreCase);

    public bool Bring(string key)
    {
        if (!_open.TryGetValue(key, out StageViewerWindow? window))
            return false;

        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Activate();

        return true;
    }

    public async Task Open(ViewerRequest request)
    {
        StageLook look = request.Current.Look ?? request.OwnLook;
        Model3DGroup stage = await Task.Run(() => StageScene.Build(request.Preview, look));

        if (Bring(request.Key))
            return;

        StageViewerViewModel viewModel = new(request.Title, request.Preview.Tilt, request.Current, request.OwnLook);
        StageViewerWindow window = new(viewModel, request.Preview, stage, request.Keep);
        _open[request.Key] = window;
        window.Closed += (_, _) => _open.Remove(request.Key);
        window.Show();
    }
}
