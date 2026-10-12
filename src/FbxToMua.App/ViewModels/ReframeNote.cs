using System.Globalization;
using FbxToMua.Core.Export;

namespace FbxToMua.App.ViewModels;

public static class ReframeNote
{
    public static string Of(Reframe reframe)
    {
        if (reframe.IsNone)
            return "Framing: as converted";

        string framing = string.Create(CultureInfo.InvariantCulture,
            $"Framing: side {reframe.Side:0.##}, height {reframe.Height:0.##}, distance {reframe.Distance:0.##}, turn {reframe.Turn:0.##}, tilt {reframe.Tilt:+0;-0;0}, scale {reframe.Scale:0.##}");

        return SameShadow(reframe) ? framing : framing + ShadowOf(reframe);
    }

    private static bool SameShadow(Reframe reframe)
    {
        Reframe shipped = Reframe.None;

        return reframe.ShadowTurn == shipped.ShadowTurn && reframe.ShadowLength == shipped.ShadowLength && reframe.ShadowStrength == shipped.ShadowStrength;
    }

    private static string ShadowOf(Reframe reframe)
    {
        return string.Create(CultureInfo.InvariantCulture,
            $"; shadow turn {reframe.ShadowTurn:0.##}, length {reframe.ShadowLength:0.##}, strength {reframe.ShadowStrength:0.##}");
    }
}
