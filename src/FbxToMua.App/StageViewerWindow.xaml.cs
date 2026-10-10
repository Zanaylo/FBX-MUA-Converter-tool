using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using FbxToMua.App.Viewer;
using FbxToMua.App.ViewModels;
using StageMatrix = FbxToMua.Core.Geometry.Matrix;

namespace FbxToMua.App;

public partial class StageViewerWindow : Window
{
    private readonly StageViewerViewModel _viewModel;
    private readonly FightView _fightView = new();
    private readonly Model3DGroup _world = new() { Transform = new ScaleTransform3D(-1.0, 1.0, 1.0) };
    private readonly Model3DGroup _stage = new();
    private readonly GeometryModel3D _fightLine = FightMarkers.FightLine();
    private readonly GeometryModel3D _fighterOne = FightMarkers.Fighter(FightMarkers.FighterOneColour);
    private readonly GeometryModel3D _fighterTwo = FightMarkers.Fighter(FightMarkers.FighterTwoColour);
    private Point? _dragFrom;

    public StageViewerWindow(StageViewerViewModel viewModel, Model3DGroup stage)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
        _stage.Children.Add(stage);
        _world.Children.Add(new AmbientLight(Colors.White));
        _world.Children.Add(_stage);
        _world.Children.Add(_fighterOne);
        _world.Children.Add(_fighterTwo);
        View.Children.Add(new ModelVisual3D { Content = _world });

        viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelChanged;
        Refresh();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StageViewerViewModel.FreeLook))
            _fightView.ResetOrbit();

        Refresh();
    }

    private void Refresh()
    {
        _stage.Transform = new MatrixTransform3D(Wpf(_viewModel.Reframe.StageMatrix()));
        _fighterOne.Transform = new TranslateTransform3D(_viewModel.FighterOne, 0.0, 0.0);
        _fighterTwo.Transform = new TranslateTransform3D(_viewModel.FighterTwo, 0.0, 0.0);
        ShowFightLine(_viewModel.ShowFightLine);
        Aim();
    }

    private void ShowFightLine(bool shown)
    {
        bool showing = _world.Children.Contains(_fightLine);

        if (shown == showing)
            return;

        if (shown)
            _world.Children.Insert(_world.Children.IndexOf(_fighterOne), _fightLine);
        else
            _world.Children.Remove(_fightLine);
    }

    private void Aim()
    {
        if (_viewModel.FreeLook)
        {
            _fightView.Orbit(Lens, _viewModel.CameraX);
            return;
        }

        FightView.Game(Lens, _viewModel.CameraX, _viewModel.SceneTilt);
    }

    private static Matrix3D Wpf(StageMatrix matrix)
    {
        return new Matrix3D(
            matrix[0], matrix[1], matrix[2], matrix[3],
            matrix[4], matrix[5], matrix[6], matrix[7],
            matrix[8], matrix[9], matrix[10], matrix[11],
            matrix[12], matrix[13], matrix[14], matrix[15]);
    }

    private void OnScreenPressed(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.FreeLook)
            return;

        _dragFrom = e.GetPosition(Screen);
        Screen.CaptureMouse();
    }

    private void OnScreenReleased(object sender, MouseButtonEventArgs e)
    {
        _dragFrom = null;
        Screen.ReleaseMouseCapture();
    }

    private void OnScreenMoved(object sender, MouseEventArgs e)
    {
        if (_dragFrom is not Point from)
            return;

        Point now = e.GetPosition(Screen);
        _fightView.Drag(now - from);
        _dragFrom = now;
        Aim();
    }

    private void OnScreenWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_viewModel.FreeLook)
            return;

        _fightView.Zoom(e.Delta);
        Aim();
    }

    private void OnKeep(object sender, RoutedEventArgs e) => DialogResult = true;
}
