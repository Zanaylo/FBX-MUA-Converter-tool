using System.Windows;
using System.Windows.Media.Media3D;
using FbxToMua.App.Viewer;
using FbxToMua.App.ViewModels;
using FbxToMua.Core.Export;
using FbxToMua.Core.Preview;

namespace FbxToMua.App.Services;

public interface IStageViewer
{
    Task<Reframe?> Adjust(string title, StagePreview preview, Reframe current);
}

public sealed class StageViewer : IStageViewer
{
    public async Task<Reframe?> Adjust(string title, StagePreview preview, Reframe current)
    {
        Model3DGroup stage = await Task.Run(() => StageScene.Build(preview));
        StageViewerViewModel viewModel = new(title, preview.Tilt, current);
        StageViewerWindow window = new(viewModel, stage) { Owner = Application.Current.MainWindow };

        return window.ShowDialog() == true ? viewModel.Reframe : null;
    }
}
