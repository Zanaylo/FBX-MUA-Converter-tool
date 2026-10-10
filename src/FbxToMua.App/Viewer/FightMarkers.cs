using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace FbxToMua.App.Viewer;

public static class FightMarkers
{
    public const double FighterHeight = 150.0;
    public const double FighterWidth = 64.0;
    private const double HeadRadius = 17.0;
    private const double BodyTop = 38.0;
    private const double BodyInset = 8.0;
    private const double BodyRound = 14.0;
    private const double LineReach = 4000.0;
    private const double LineDepth = 8.0;
    private const double LineLift = 0.5;
    private const byte MarkerAlpha = 0xb0;

    public static readonly Color FighterOneColour = Color.FromArgb(MarkerAlpha, 0xe0, 0x40, 0x40);
    public static readonly Color FighterTwoColour = Color.FromArgb(MarkerAlpha, 0x40, 0x80, 0xe0);
    private static readonly Color LineColour = Color.FromArgb(MarkerAlpha, 0xf0, 0xd0, 0x30);

    public static GeometryModel3D Fighter(Color colour)
    {
        double half = FighterWidth * 0.5;
        MeshGeometry3D quad = Quad(new Point3D(-half, 0.0, 0.0), new Point3D(half, 0.0, 0.0), new Point3D(half, FighterHeight, 0.0), new Point3D(-half, FighterHeight, 0.0));
        Material material = Frozen(new DiffuseMaterial(Silhouette(colour)));

        return new GeometryModel3D(quad, material) { BackMaterial = material };
    }

    public static GeometryModel3D FightLine()
    {
        MeshGeometry3D quad = Quad(new Point3D(-LineReach, LineLift, -LineDepth), new Point3D(LineReach, LineLift, -LineDepth),
            new Point3D(LineReach, LineLift, LineDepth), new Point3D(-LineReach, LineLift, LineDepth));
        Material material = Frozen(new DiffuseMaterial(new SolidColorBrush(LineColour)));
        GeometryModel3D line = new(quad, material) { BackMaterial = material };
        line.Freeze();

        return line;
    }

    private static MeshGeometry3D Quad(Point3D lowLeft, Point3D lowRight, Point3D highRight, Point3D highLeft)
    {
        MeshGeometry3D quad = new()
        {
            Positions = [lowLeft, lowRight, highRight, highLeft],
            TextureCoordinates = [new Point(0.0, 1.0), new Point(1.0, 1.0), new Point(1.0, 0.0), new Point(0.0, 0.0)],
            TriangleIndices = [0, 1, 2, 0, 2, 3],
        };
        quad.Freeze();

        return quad;
    }

    private static DrawingBrush Silhouette(Color colour)
    {
        GeometryGroup shape = new();
        shape.Children.Add(new EllipseGeometry(new Point(FighterWidth * 0.5, HeadRadius), HeadRadius, HeadRadius));
        shape.Children.Add(new RectangleGeometry(new Rect(BodyInset, BodyTop, FighterWidth - 2.0 * BodyInset, FighterHeight - BodyTop), BodyRound, BodyRound));

        DrawingBrush brush = new(new GeometryDrawing(new SolidColorBrush(colour), null, shape))
        {
            Viewbox = new Rect(0.0, 0.0, FighterWidth, FighterHeight),
            ViewboxUnits = BrushMappingMode.Absolute,
        };
        brush.Freeze();

        return brush;
    }

    private static Material Frozen(Material material)
    {
        material.Freeze();

        return material;
    }
}
