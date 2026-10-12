using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;
using FbxToMua.App.Viewer;
using FbxToMua.App.ViewModels;
using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Preview;
using StageMatrix = FbxToMua.Core.Geometry.Matrix;

namespace FbxToMua.App;

public partial class StageViewerWindow : Window
{
    private static readonly TimeSpan RelookDelay = TimeSpan.FromMilliseconds(250);

    private readonly StageViewerViewModel _viewModel;
    private readonly Action<Reframe> _keep;
    private readonly StagePreview _preview;
    private readonly DispatcherTimer _relook;
    private int _looks;
    private readonly FightView _fightView = new();
    private readonly Model3DGroup _world = new() { Transform = new ScaleTransform3D(-1.0, 1.0, 1.0) };
    private readonly Model3DGroup _stage = new();
    private readonly GeometryModel3D _fightLine = FightMarkers.FightLine();
    private readonly GeometryModel3D _fighterOne = FightMarkers.Fighter(FightMarkers.FighterOneColour);
    private readonly GeometryModel3D _fighterTwo = FightMarkers.Fighter(FightMarkers.FighterTwoColour);
    private readonly Model3DGroup _shadows = new();
    private Point? _dragFrom;
    private Reframe? _litKey;
    private List<ShadowPiece<Float3>> _lit = [];

    public StageViewerWindow(StageViewerViewModel viewModel, StagePreview preview, Model3DGroup stage, Action<Reframe> keep)
    {
        InitializeComponent();

        _viewModel = viewModel;
        _keep = keep;
        _preview = preview;
        _relook = new DispatcherTimer { Interval = RelookDelay };
        _relook.Tick += OnRelook;
        DataContext = viewModel;
        _stage.Children.Add(stage);
        _world.Children.Add(new AmbientLight(Colors.White));
        _world.Children.Add(_stage);
        _world.Children.Add(_shadows);
        _world.Children.Add(_fighterOne);
        _world.Children.Add(_fighterTwo);
        View.Children.Add(new ModelVisual3D { Content = _world });

        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Look.PropertyChanged += OnLookChanged;
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= OnViewModelChanged;
            viewModel.Look.PropertyChanged -= OnLookChanged;
            _relook.Stop();
        };
        Refresh();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StageViewerViewModel.ShadowNote))
            return;

        if (e.PropertyName == nameof(StageViewerViewModel.FreeLook))
            _fightView.ResetOrbit();

        Refresh();
    }

    private void OnLookChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LookEditor.Look))
            return;

        _relook.Stop();
        _relook.Start();
    }

    private async void OnRelook(object? sender, EventArgs e)
    {
        _relook.Stop();
        int asked = ++_looks;
        StageLook look = _viewModel.Look.Look;
        Model3DGroup stage = await Task.Run(() => StageScene.Build(_preview, look));

        if (asked != _looks)
            return;

        _stage.Children.Clear();
        _stage.Children.Add(stage);
    }

    private void Refresh()
    {
        _stage.Transform = new MatrixTransform3D(Wpf(MatrixMath.Multiply(_viewModel.Reframe.StageMatrix(), SceneTurn.Of(_viewModel.SceneTilt))));
        _fighterOne.Transform = new TranslateTransform3D(_viewModel.FighterOne, 0.0, 0.0);
        _fighterTwo.Transform = new TranslateTransform3D(_viewModel.FighterTwo, 0.0, 0.0);
        ShowFightLine(_viewModel.ShowFightLine);
        CastShadows();
        Aim();
    }

    private void CastShadows()
    {
        _shadows.Children.Clear();
        Reframe reframe = _viewModel.Reframe;
        List<ShadowPiece<Float3>> lit = LitFloors(reframe);
        _viewModel.ShadowNote = ShadowNote.Of(SceneLight.Vector(reframe), lit.Count);

        if (!_viewModel.ShowShadows)
            return;

        StageMatrix toWorld = SceneTurn.Of(_viewModel.SceneTilt);
        (float fallX, float fallZ) = SceneLight.Reach(reframe, 1.0f);

        foreach (float fighter in new[] { _viewModel.FighterOne, _viewModel.FighterTwo })
        {
            ShadowCaster caster = new(fighter, (float)FightMarkers.FighterWidth, (float)FightMarkers.FighterHeight, fallX, fallZ);
            _shadows.Children.Add(FightMarkers.Shadow(ShadowCast.Onto(lit, toWorld, caster), _viewModel.ShadowStrength));
        }
    }

    private List<ShadowPiece<Float3>> LitFloors(Reframe reframe)
    {
        Reframe key = reframe with { Tilt = 0.0f, ShadowStrength = 0.0f, Look = null };

        if (_litKey == key)
            return _lit;

        _litKey = key;
        _lit = ShadowReceiver.Lit(_preview.ShadowCandidates, reframe.StageMatrix(), SceneLight.Vector(reframe));

        return _lit;
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

        FightView.Game(Lens, _viewModel.CameraX);
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

    private void OnKeep(object sender, RoutedEventArgs e)
    {
        _keep(_viewModel.Reframe);
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
