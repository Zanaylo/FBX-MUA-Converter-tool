using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using FbxToMua.Core.Imaging;
using FbxToMua.Core.Preview;

namespace FbxToMua.App.Viewer;

public static class StageScene
{
    private const double Dpi = 96.0;
    private static readonly Rect WholeTexture = new(0.0, 0.0, 1.0, 1.0);
    private static readonly Color Untextured = Color.FromRgb(0x80, 0x80, 0x80);

    public static Model3DGroup Build(StagePreview preview)
    {
        Dictionary<string, Brush> brushes = new(StringComparer.OrdinalIgnoreCase);
        Model3DGroup scene = new();

        foreach (PreviewMesh mesh in preview.Meshes)
        {
            foreach (PreviewPart part in mesh.Parts)
            {
                if (part.Triangles.Length == 0)
                    continue;

                Brush brush = BrushFor(part.Texture, preview, brushes);
                scene.Children.Add(Part(mesh, part, MaterialFor(brush, mesh.Blend)));
            }
        }

        scene.Freeze();

        return scene;
    }

    private static GeometryModel3D Part(PreviewMesh mesh, PreviewPart part, Material material)
    {
        GeometryModel3D model = new(Geometry(mesh, part), material) { BackMaterial = material };
        model.Freeze();

        return model;
    }

    private static MeshGeometry3D Geometry(PreviewMesh mesh, PreviewPart part)
    {
        Dictionary<int, int> taken = [];
        Point3DCollection positions = new();
        PointCollection uvs = new();
        Int32Collection triangles = new(part.Triangles.Length);

        foreach (int v in part.Triangles)
        {
            if (!taken.TryGetValue(v, out int local))
            {
                local = positions.Count;
                taken[v] = local;
                positions.Add(new Point3D(mesh.Positions[v * 3], mesh.Positions[v * 3 + 1], mesh.Positions[v * 3 + 2]));
                uvs.Add(new Point(mesh.Uvs[v * 2], mesh.Uvs[v * 2 + 1]));
            }

            triangles.Add(local);
        }

        MeshGeometry3D geometry = new()
        {
            Positions = positions,
            TextureCoordinates = uvs,
            TriangleIndices = triangles,
        };
        geometry.Freeze();

        return geometry;
    }

    private static Material MaterialFor(Brush brush, PreviewBlend blend)
    {
        Material material = blend == PreviewBlend.Add ? new EmissiveMaterial(brush) : new DiffuseMaterial(brush);
        material.Freeze();

        return material;
    }

    private static Brush BrushFor(string texture, StagePreview preview, Dictionary<string, Brush> brushes)
    {
        if (brushes.TryGetValue(texture, out Brush? known))
            return known;

        Brush brush = preview.Images.TryGetValue(texture, out BgraImage? image) ? ImageBrushOf(image) : new SolidColorBrush(Untextured);
        brush.Freeze();
        brushes[texture] = brush;

        return brush;
    }

    private static ImageBrush ImageBrushOf(BgraImage image)
    {
        BitmapSource bitmap = BitmapSource.Create(image.Width, image.Height, Dpi, Dpi, PixelFormats.Bgra32, null, image.Pixels, image.Width * BgraImage.Channels);
        bitmap.Freeze();

        return new ImageBrush(bitmap)
        {
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = WholeTexture,
            TileMode = TileMode.Tile,
        };
    }
}
