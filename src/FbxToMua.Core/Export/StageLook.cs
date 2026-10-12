using System.Text.Json.Serialization;
using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Export;

public readonly record struct FogLook(bool On, float Start, float End, float Red, float Green, float Blue, bool ByDistance = false)
{
    private const float SameRangeGap = 0.01f;
    private const float VisibleChange = 6.0f / 255.0f;
    private const float Opaque = 255.0f;

    public static FogLook Off => new(false, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f);

    public float FactorAt(float depth)
    {
        float end = End == Start ? Start + SameRangeGap : End;
        float distance = ByDistance ? depth : StageDepth.Near;

        return (end - distance) / (end - Start);
    }

    public bool VisibleAt(float depth) => On && Math.Abs(1.0f - FactorAt(depth)) * Reach >= VisibleChange;

    private float Reach => new[] { Red, Green, Blue }.Max(colour => Math.Max(Math.Abs(colour), Math.Abs(1.0f - colour)));

    public BgraImage Applied(BgraImage image, float depth)
    {
        if (!VisibleAt(depth))
            return image;

        float factor = FactorAt(depth);
        float[] fog = [Blue, Green, Red];
        BgraImage fogged = image.Copy();

        for (int at = 0; at < fogged.Pixels.Length; at += BgraImage.Channels)
        {
            for (int c = 0; c < BgraImage.Alpha; ++c)
            {
                float mixed = fog[c] + (fogged.Pixels[at + c] / Opaque - fog[c]) * factor;
                fogged.Pixels[at + c] = (byte)MathF.Round(Math.Clamp(mixed, 0.0f, 1.0f) * Opaque, MidpointRounding.AwayFromZero);
            }
        }

        return fogged;
    }
}

public readonly record struct BloomLook(bool On, float Brightness, float Power, float Red, float Green, float Blue, float Radius, float Strength)
{
    public static BloomLook Off => new(false, 0.8f, 2.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f);

    [JsonIgnore]
    public bool Visible => On && Strength > 0.0f;

    public BgraImage Applied(BgraImage image) => Visible ? StageBloom.Glowed(image, this) : image;
}

public readonly record struct StageLook(FogLook Fog, BloomLook Bloom, bool ClearOfGameBloom = false)
{
    public static StageLook Plain => new(FogLook.Off, BloomLook.Off);

    public static StageLook Imported => new(FogLook.Off, BloomLook.Off with { On = true });

    public bool ChangesAt(float depth) => Fog.VisibleAt(depth) || Bloom.Visible;

    public BgraImage Applied(BgraImage image, float depth) => Bloom.Applied(Fog.Applied(image, depth));

    public byte[] Baked(byte[] dds, float depth)
    {
        if (!ChangesAt(depth))
            return dds;

        BgraImage? decoded = DdsCodec.Decode(dds);

        return decoded is null ? dds : DdsCodec.EncodeDxt(Applied(decoded, depth));
    }
}
