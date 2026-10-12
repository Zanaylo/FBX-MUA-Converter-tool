using System.Runtime.CompilerServices;
using System.Windows.Input;
using FbxToMua.Core.Export;

namespace FbxToMua.App.ViewModels;

public sealed class StageViewerViewModel : ObservableObject
{
    public const float FighterStart = 90.0f;

    private float _side;
    private float _height;
    private float _distance;
    private float _turn;
    private float _tilt;
    private float _scale;
    private float _shadowTurn;
    private float _shadowLength = Reframe.ShippedShadowLength;
    private float _shadowStrength = Reframe.ShippedShadowStrength;
    private float _fighterOne = -FighterStart;
    private float _fighterTwo = FighterStart;
    private bool _freeLook;
    private bool _showFightLine = true;
    private bool _showShadows = true;
    private string _shadowNote = string.Empty;

    public StageViewerViewModel(string title, int stageTilt, Reframe current, StageLook ownLook)
    {
        Title = title;
        StageTilt = stageTilt;
        Look = new LookEditor(ownLook, true);
        Apply(current);

        ResetFraming = new RelayCommand(() => ApplyCamera(Reframe.None));
        ResetFighters = new RelayCommand(() => (FighterOne, FighterTwo) = (-FighterStart, FighterStart));
        ResetShadow = new RelayCommand(() => (ShadowTurn, ShadowLength, ShadowStrength) = (Reframe.None.ShadowTurn, Reframe.None.ShadowLength, Reframe.None.ShadowStrength));
    }

    public string Title { get; }

    public LookEditor Look { get; }

    public int StageTilt { get; }

    public ICommand ResetFraming { get; }

    public ICommand ResetFighters { get; }

    public ICommand ResetShadow { get; }

    public Reframe Reframe => new(Side, Height, Distance, Turn, Scale, Tilt, ShadowTurn, ShadowLength, ShadowStrength, Look.Kept);

    public int SceneTilt => Reframe.SceneTilt(StageTilt);

    public float CameraX => (FighterOne + FighterTwo) * 0.5f;

    public float Side
    {
        get => _side;
        set => SetFraming(ref _side, value);
    }

    public float Height
    {
        get => _height;
        set => SetFraming(ref _height, value);
    }

    public float Distance
    {
        get => _distance;
        set => SetFraming(ref _distance, value);
    }

    public float Turn
    {
        get => _turn;
        set => SetFraming(ref _turn, value);
    }

    public float Tilt
    {
        get => _tilt;
        set
        {
            if (SetFraming(ref _tilt, value))
                Raise(nameof(SceneTilt));
        }
    }

    public float Scale
    {
        get => _scale;
        set => SetFraming(ref _scale, value);
    }

    public float ShadowTurn
    {
        get => _shadowTurn;
        set => SetFraming(ref _shadowTurn, value);
    }

    public float ShadowLength
    {
        get => _shadowLength;
        set => SetFraming(ref _shadowLength, value);
    }

    public float ShadowStrength
    {
        get => _shadowStrength;
        set => SetFraming(ref _shadowStrength, value);
    }

    public float FighterOne
    {
        get => _fighterOne;
        set => SetFighter(ref _fighterOne, value);
    }

    public float FighterTwo
    {
        get => _fighterTwo;
        set => SetFighter(ref _fighterTwo, value);
    }

    public bool FreeLook
    {
        get => _freeLook;
        set => SetProperty(ref _freeLook, value);
    }

    public bool ShowFightLine
    {
        get => _showFightLine;
        set => SetProperty(ref _showFightLine, value);
    }

    public bool ShowShadows
    {
        get => _showShadows;
        set => SetProperty(ref _showShadows, value);
    }

    public string ShadowNote
    {
        get => _shadowNote;
        set => SetProperty(ref _shadowNote, value);
    }

    private void Apply(Reframe reframe)
    {
        ApplyCamera(reframe);
        ShadowTurn = reframe.ShadowTurn;
        ShadowLength = reframe.ShadowLength;
        ShadowStrength = reframe.ShadowStrength;
        Look.Apply(reframe.Look ?? Look.Own);
    }

    private void ApplyCamera(Reframe reframe)
    {
        Side = reframe.Side;
        Height = reframe.Height;
        Distance = reframe.Distance;
        Turn = reframe.Turn;
        Tilt = reframe.Tilt;
        Scale = reframe.Scale;
    }

    private bool SetFraming(ref float field, float value, [CallerMemberName] string? name = null)
    {
        if (!SetProperty(ref field, value, name))
            return false;

        Raise(nameof(Reframe));

        return true;
    }

    private void SetFighter(ref float field, float value, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value, name))
            Raise(nameof(CameraX));
    }
}
