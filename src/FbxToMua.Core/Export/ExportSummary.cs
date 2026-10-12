namespace FbxToMua.Core.Export;

public static class ExportSummary
{
    public static string Of(ExportResult result)
    {
        string summary = $"Exported {result.Stage}: {result.Meshes} mesh(es), {result.Animated} animated, {result.Images.Count} texture(s).";
        summary += Counted(result.Sprites, "layer sprite(s).");
        summary += Counted(result.Front, "sprite(s) in front of the fighters in UNI2 draw behind them here.");
        summary += Counted(result.Pulled, "far mesh(es) brought inside the game's far plane.");
        summary += Counted(result.ClearOfGameBloom, "solid node(s) drawn opaque, out of the game's bloom.");
        summary += Shadow(result);
        summary += Counted(result.Absent.Count, "layer pattern(s) not found.");
        summary += Counted(result.Missing.Count, "texture(s) not found.");
        summary += Counted(result.Foreign.Count, "texture(s) are not DDS.");

        return result.Turned ? summary + " View rotation Y is not carried over." : summary;
    }

    private static string Shadow(ExportResult result)
    {
        if (result.ShadowFloors == 0)
            return " No floor takes the fighters' shadow, so the game draws none.";

        return Counted(result.ShadowFloors, "floor mesh(es) take the fighters' shadow.") + Counted(result.ShadowCuts, "of them cut to the strip BBTAG casts this leaning shadow on.");
    }

    private static string Counted(int count, string what) => count > 0 ? $" {count} {what}" : string.Empty;
}
