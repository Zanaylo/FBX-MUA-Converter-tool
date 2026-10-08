namespace FbxToMua.Core.Export;

public readonly record struct Lens(double EyeDistance, double EyeHeight, double Fov);

public static class BattleCamera
{
    public const double EyeDistance = 320.0;
    public const double EyeHeight = 100.0;
    public const double Fov = 45.0;
    public const double Aspect = 16.0 / 9.0;
    public const double LayerFocal = 1735.0;
    public const double LayerHeight = 720.0;
    public const double FarPlane = 100000.0;

    public static readonly Lens Bbtag = new(EyeDistance, EyeHeight, Fov);
    public static readonly Lens P4u2 = new(334.0, 99.0, 41.7);

    public static double LayerUnits() => LayerHeight / (2.0 * EyeDistance * Math.Tan(Fov * Math.PI / 360.0));

    public static double FightPlane() => EyeDistance * Math.Tan(Fov * Math.PI / 360.0);
}
