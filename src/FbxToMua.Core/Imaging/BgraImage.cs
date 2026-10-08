namespace FbxToMua.Core.Imaging;

public sealed class BgraImage
{
    public const int Channels = 4;
    public const int Alpha = 3;

    public byte[] Pixels { get; set; } = [];
    public int Width { get; set; }
    public int Height { get; set; }

    public BgraImage Copy() => new() { Pixels = (byte[])Pixels.Clone(), Width = Width, Height = Height };

    public bool Inside(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public int At(int x, int y) => (y * Width + x) * Channels;
}

public readonly record struct PixelRect(int X, int Y, int Width, int Height);
