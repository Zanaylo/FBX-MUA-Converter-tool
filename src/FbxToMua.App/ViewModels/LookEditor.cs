using System.Runtime.CompilerServices;
using System.Windows.Input;
using FbxToMua.Core.Export;

namespace FbxToMua.App.ViewModels;

public sealed class LookEditor : ObservableObject
{
    private bool _fogOn;
    private float _fogStart;
    private float _fogEnd;
    private float _fogRed;
    private float _fogGreen;
    private float _fogBlue;
    private bool _fogByDistance;
    private bool _bloomOn;
    private float _bloomBrightness;
    private float _bloomPower;
    private float _bloomRed;
    private float _bloomGreen;
    private float _bloomBlue;
    private float _bloomRadius;
    private float _bloomStrength;
    private bool _clearOfGameBloom;

    public LookEditor(StageLook own, bool exporting)
    {
        Own = own;
        Exporting = exporting;
        Apply(own);

        ResetFog = new RelayCommand(() => Apply(Look with { Fog = Own.Fog }));
        ResetBloom = new RelayCommand(() => Apply(Look with { Bloom = Own.Bloom, ClearOfGameBloom = Own.ClearOfGameBloom }));
    }

    public StageLook Own { get; }

    public bool Exporting { get; }

    public ICommand ResetFog { get; }

    public ICommand ResetBloom { get; }

    public StageLook Look => new(
        new FogLook(FogOn, FogStart, FogEnd, FogRed, FogGreen, FogBlue, FogByDistance),
        new BloomLook(BloomOn, BloomBrightness, BloomPower, BloomRed, BloomGreen, BloomBlue, BloomRadius, BloomStrength),
        ClearOfGameBloom);

    public StageLook? Kept => Look == Own ? null : Look;

    public bool FogOn { get => _fogOn; set => SetLook(ref _fogOn, value); }

    public float FogStart { get => _fogStart; set => SetLook(ref _fogStart, value); }

    public float FogEnd { get => _fogEnd; set => SetLook(ref _fogEnd, value); }

    public float FogRed { get => _fogRed; set => SetLook(ref _fogRed, value); }

    public float FogGreen { get => _fogGreen; set => SetLook(ref _fogGreen, value); }

    public float FogBlue { get => _fogBlue; set => SetLook(ref _fogBlue, value); }

    public bool FogByDistance { get => _fogByDistance; set => SetLook(ref _fogByDistance, value); }

    public bool BloomOn { get => _bloomOn; set => SetLook(ref _bloomOn, value); }

    public float BloomBrightness { get => _bloomBrightness; set => SetLook(ref _bloomBrightness, value); }

    public float BloomPower { get => _bloomPower; set => SetLook(ref _bloomPower, value); }

    public float BloomRed { get => _bloomRed; set => SetLook(ref _bloomRed, value); }

    public float BloomGreen { get => _bloomGreen; set => SetLook(ref _bloomGreen, value); }

    public float BloomBlue { get => _bloomBlue; set => SetLook(ref _bloomBlue, value); }

    public float BloomRadius { get => _bloomRadius; set => SetLook(ref _bloomRadius, value); }

    public float BloomStrength { get => _bloomStrength; set => SetLook(ref _bloomStrength, value); }

    public bool ClearOfGameBloom { get => _clearOfGameBloom; set => SetLook(ref _clearOfGameBloom, value); }

    public void Apply(StageLook look)
    {
        (FogOn, FogStart, FogEnd, FogRed, FogGreen, FogBlue) = (look.Fog.On, look.Fog.Start, look.Fog.End, look.Fog.Red, look.Fog.Green, look.Fog.Blue);
        FogByDistance = look.Fog.ByDistance;
        (BloomOn, BloomBrightness, BloomPower) = (look.Bloom.On, look.Bloom.Brightness, look.Bloom.Power);
        (BloomRed, BloomGreen, BloomBlue) = (look.Bloom.Red, look.Bloom.Green, look.Bloom.Blue);
        (BloomRadius, BloomStrength) = (look.Bloom.Radius, look.Bloom.Strength);
        ClearOfGameBloom = look.ClearOfGameBloom;
    }

    private void SetLook<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (SetProperty(ref field, value, name))
            Raise(nameof(Look));
    }
}
