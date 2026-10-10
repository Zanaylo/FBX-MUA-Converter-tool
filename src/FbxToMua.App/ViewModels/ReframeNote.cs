using System.Globalization;
using FbxToMua.Core.Export;

namespace FbxToMua.App.ViewModels;

public static class ReframeNote
{
    public static string Of(Reframe reframe)
    {
        if (reframe.IsNone)
            return "Framing: as converted";

        return string.Create(CultureInfo.InvariantCulture,
            $"Framing: side {reframe.Side:0.##}, height {reframe.Height:0.##}, distance {reframe.Distance:0.##}, turn {reframe.Turn:0.##}, tilt {reframe.Tilt:+0;-0;0}, scale {reframe.Scale:0.##}");
    }
}
