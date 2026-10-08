using System.Globalization;
using FbxToMua.Core.Export;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Import;

public sealed record ArcLook(string Stage, float Size, float Contrast, float Glow);

public static class ArcLooks
{
    public const double Character = 213.0;
    public const double Uni2Character = 0.132;
    public const double Uni2EyeHeight = 280.0 / 360.0;

    private const string StageBlock =
        "\tIsFog = 0,\r\n\tFogStart = 0.0,\r\n\tFogEnd = 100.0,\r\n\tFogColor = [ 0.0, 0.0, 0.0, 0.0 ],\r\n\tMSAA = 4,\r\n" +
        "\tStageW = 4096,\r\n\tIsBloom = 0,\r\n\tShadowLightType = 0,\r\n\tShadowReflexColor = [ 0.0, 0.0, 0.0, 0.0 ],\r\n" +
        "\tShadowScale = 0.6,\r\n\tShadowAlpha = 0.7,\r\n\tBGBloomEnable = 1,\r\n\tBGBloomBlightness = 0.8,\r\n\tBGBloomPower = 2.0,\r\n" +
        "\tBGBloomBiassR = 1.0,\r\n\tBGBloomBiassG = 1.0,\r\n\tBGBloomBiassB = 1.0,\r\n\tBGBloomBlurRadius = 1.0,\r\n" +
        "\tBGBloomTextureSize = 256,\r\n\tBGBloomAlpha = 0.5,\r\n\tBGTinyFXAAEnable = 0,\r\n\tBGTinyFXAAThreshold = 0.2,\r\n\tBGTinyFXAALerpT = 0.5,\r\n";

    private static readonly ArcLook[] BbtagLooks =
    [
        new("bg_beach", 1.00f, 1.11f, 1.00f), new("bg_bluegate", 1.00f, 1.10f, 1.05f), new("bg_boss", 1.05f, 1.15f, 1.10f),
        new("bg_church_2", 1.10f, 1.30f, 1.21f), new("bg_crater", 1.00f, 1.20f, 1.05f), new("bg_crossfield", 1.10f, 1.25f, 1.00f),
        new("bg_entrance", 1.10f, 1.10f, 1.00f), new("bg_falling_blossoms", 1.05f, 1.20f, 1.10f), new("bg_foodcourt", 1.05f, 1.25f, 1.15f),
        new("bg_fountain_plaza", 1.00f, 1.25f, 1.00f), new("bg_garden", 1.05f, 1.15f, 1.08f), new("bg_gate_p4u2", 1.00f, 1.10f, 1.10f),
        new("bg_gyoenjogakuen", 1.00f, 1.10f, 1.05f), new("bg_intersection", 1.05f, 1.15f, 1.10f), new("bg_ishana", 1.10f, 1.25f, 1.20f),
        new("bg_lakeside", 1.15f, 1.25f, 1.10f), new("bg_monolis", 1.00f, 1.20f, 1.16f), new("bg_nevermore", 1.05f, 1.20f, 1.10f),
        new("bg_odaiba", 1.07f, 1.20f, 0.90f), new("bg_prologue", 1.10f, 1.10f, 1.10f), new("bg_ring", 1.15f, 1.15f, 1.00f),
        new("bg_snowtown", 1.10f, 1.26f, 1.20f), new("bg_stadium", 1.00f, 1.25f, 1.05f), new("bg_station", 1.10f, 1.32f, 1.14f),
        new("bg_station_2", 1.10f, 1.25f, 1.05f), new("bg_street", 1.05f, 1.25f, 1.20f), new("bg_test_2", 0.90f, 1.10f, 1.00f),
        new("bg_test_3", 1.00f, 1.15f, 4.00f), new("bg_test_4", 40.00f, 1.20f, 1.05f), new("bg_test_5", 25.00f, 1.10f, 1.00f),
        new("bg_town", 1.10f, 1.28f, 1.10f), new("bg_training", 1.10f, 1.15f, 1.10f),
    ];

    private static readonly ArcLook[] P4u2Looks =
    [
        new("bg_astro", 1.00f, 1.35f, 1.00f), new("bg_boss_1", 1.00f, 1.17f, 1.00f), new("bg_boss_1_p4u2", 1.00f, 1.17f, 1.00f),
        new("bg_boss_2", 1.00f, 1.15f, 1.00f), new("bg_boss_2_p4u2", 1.00f, 1.15f, 1.00f), new("bg_boss_3_a", 1.00f, 1.30f, 1.00f),
        new("bg_boss_3_b", 1.00f, 1.17f, 1.00f), new("bg_classroom", 1.00f, 1.26f, 1.00f), new("bg_classroom_p4u2", 1.00f, 1.26f, 1.00f),
        new("bg_corridor", 1.00f, 1.17f, 1.00f), new("bg_corridor_2", 1.00f, 1.14f, 1.00f), new("bg_corridor_p4u2", 1.00f, 1.17f, 1.00f),
        new("bg_entrance", 1.00f, 1.17f, 1.00f), new("bg_entrance_p4u2", 1.00f, 1.17f, 1.00f), new("bg_foodcourt", 1.00f, 1.18f, 1.00f),
        new("bg_gate", 1.00f, 1.13f, 1.00f), new("bg_gate_3", 1.00f, 1.20f, 1.00f), new("bg_gate_night", 1.00f, 1.08f, 1.00f),
        new("bg_gate_night_p4u2", 1.00f, 1.08f, 1.00f), new("bg_gate_p4u2", 1.00f, 1.13f, 1.00f), new("bg_gym", 1.00f, 1.15f, 1.00f),
        new("bg_gym_p4u2", 1.00f, 1.15f, 1.00f), new("bg_gym_p4u2_b", 1.00f, 1.14f, 1.00f), new("bg_musicroom", 1.00f, 1.17f, 1.00f),
        new("bg_musicroom_p4u2", 1.00f, 1.17f, 1.00f), new("bg_riverbed", 1.00f, 1.16f, 1.00f), new("bg_street", 1.00f, 1.25f, 1.00f),
        new("bg_street_2", 1.00f, 1.19f, 1.00f), new("bg_street_p4u2", 1.00f, 1.25f, 1.00f), new("bg_tartaros", 1.00f, 1.21f, 1.00f),
        new("bg_town", 1.00f, 1.16f, 1.00f),
    ];

    public static Lens LensOf(GameKind game) => game == GameKind.P4u2 ? BattleCamera.P4u2 : BattleCamera.Bbtag;

    public static ArcLook? Of(GameKind game, string stage)
    {
        ArcLook[]? looks = game switch
        {
            GameKind.P4u2 => P4u2Looks,
            GameKind.Bbtag => BbtagLooks,
            _ => null,
        };

        return looks?.FirstOrDefault(look => string.Equals(look.Stage, stage, StringComparison.OrdinalIgnoreCase));
    }

    public static string Block(string stage, GameKind game, float tilt = 0.0f)
    {
        Lens lens = LensOf(game);
        double size = Of(game, stage)?.Size ?? 1.0;
        double half = Math.Tan(lens.Fov * Math.PI / 360.0);
        double scale = size * (1.0 / half) / (lens.EyeDistance * Uni2Character / Character);
        double vanish = Uni2EyeHeight - lens.EyeHeight / (lens.EyeDistance * half);
        CultureInfo invariant = CultureInfo.InvariantCulture;

        return string.Format(invariant, "\tScale = [ {0:F4}, {0:F4}, {0:F4} ],\r\n", scale)
            + "\tPosition = [ 0.0, 0.0, 0.0 ],\r\n"
            + "\tViewGrid = 0,\r\n"
            + string.Format(invariant, "\tFOV = {0:F1},\r\n", lens.Fov)
            + string.Format(invariant, "\tViewRotationX = {0:F1},\r\n", tilt)
            + string.Format(invariant, "\tVanishingPoint = {0:F4},\r\n", vanish)
            + StageBlock;
    }
}
