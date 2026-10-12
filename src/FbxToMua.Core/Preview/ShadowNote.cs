using System.Globalization;
using FbxToMua.Core.Export;

namespace FbxToMua.Core.Preview;

public static class ShadowNote
{
    public static string Of(int[] light, int litPieces)
    {
        if (litPieces == 0)
            return "No floor of this stage takes the shadow with this light, so BBTAG draws none.";

        ShadowBand band = ShadowArea.Band(light);

        if (band.IsOpen)
            return "BBTAG shades the whole floor with this light.";

        return string.Create(CultureInfo.InvariantCulture,
            $"BBTAG only shades the floor between z {band.Low:0} and {band.High:0} with this light, so the export cuts the floor there and the shadow stops at that line. Leaning it the other way (a negative turn) widens the strip.");
    }
}
