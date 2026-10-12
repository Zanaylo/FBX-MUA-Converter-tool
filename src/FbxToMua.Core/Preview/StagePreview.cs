using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Preview;

public enum PreviewBlend
{
    Normal,
    Add,
    Subtract,
}

public sealed record PreviewPart(string Texture, int[] Triangles);

public sealed class PreviewMesh
{
    public string Name { get; init; } = string.Empty;
    public float[] Positions { get; init; } = [];
    public float[] Uvs { get; init; } = [];
    public List<PreviewPart> Parts { get; init; } = [];
    public PreviewBlend Blend { get; init; }
    public bool Overlay { get; init; }
}

public sealed class StagePreview
{
    public const int MostTextureSide = 1024;
    private const int NoTexture = -1;

    public List<PreviewMesh> Meshes { get; } = [];
    public Dictionary<string, BgraImage> Images { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, float> Depths { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int Tilt { get; init; }
    public IReadOnlyList<ShadowCandidate> ShadowCandidates { get; init; } = [];

    public static StagePreview Of(ExportResult result, int mostTextureSide = MostTextureSide)
    {
        MuaReader model = MuaReader.Read(result.Model) ?? throw new StageConversionException("the exported model could not be read back");
        StagePreview preview = new() { Tilt = result.Tilt, ShadowCandidates = result.ShadowCandidates };

        foreach ((string texture, float depth) in result.Depths)
            preview.Depths[texture] = depth;

        foreach (MuaReadMesh mesh in model.Meshes.OrderByDescending(mesh => mesh.Pivot[2]))
            preview.Meshes.Add(MeshOf(model, mesh));

        List<(string Name, BgraImage? Image)> decoded = result.Images.AsParallel().AsOrdered()
            .Select(image => (image.Name, Readable(image, mostTextureSide)))
            .ToList();

        foreach ((string name, BgraImage? image) in decoded)
        {
            if (image is not null)
                preview.Images[name] = image;
        }

        return preview;
    }

    private static BgraImage? Readable(ExportFile image, int mostSide)
    {
        return DdsCodec.Decode(image.Data) is BgraImage decoded ? Fitted(decoded, mostSide) : null;
    }

    private static BgraImage Fitted(BgraImage image, int mostSide)
    {
        BgraImage fitted = image;

        while (fitted.Width > mostSide || fitted.Height > mostSide)
            fitted = ImageOps.Halved(fitted);

        return fitted;
    }

    private static PreviewMesh MeshOf(MuaReader model, MuaReadMesh mesh)
    {
        float[] positions = new float[mesh.Vertices * 3];
        float[] uvs = new float[mesh.Vertices * 2];

        for (int v = 0; v < mesh.Vertices; ++v)
        {
            MuaReadVertex vertex = model.VertexAt(mesh.FirstVertex + v);

            for (int k = 0; k < 3; ++k)
                positions[v * 3 + k] = vertex.Position[k];

            uvs[v * 2] = vertex.U;
            uvs[v * 2 + 1] = vertex.V;
        }

        MuaReadSkeleton skeleton = model.Skeletons[mesh.Skeleton];

        return new PreviewMesh
        {
            Name = mesh.Name,
            Positions = positions,
            Uvs = uvs,
            Parts = PartsOf(model, mesh),
            Blend = BlendOf(skeleton.Blend),
            Overlay = (skeleton.Flags & MuaFlags.NoDepthTest) != 0,
        };
    }

    private static List<PreviewPart> PartsOf(MuaReader model, MuaReadMesh mesh)
    {
        List<PreviewPart> parts = [];

        for (int p = mesh.FirstPart; p < mesh.FirstPart + mesh.Parts; ++p)
        {
            MuaPart part = model.Parts[p];
            int[] triangles = model.Triangles(part).SelectMany(triangle => new[] { triangle.A, triangle.B, triangle.C }).ToArray();
            parts.Add(new PreviewPart(TextureOf(model, part.Material), triangles));
        }

        return parts;
    }

    private static string TextureOf(MuaReader model, int material)
    {
        bool known = material >= 0 && material < model.Materials.Count && model.Materials[material].Count > 0;
        int texture = known ? model.Materials[material][0] : NoTexture;

        return texture >= 0 && texture < model.Textures.Count ? model.Textures[texture] : string.Empty;
    }

    private static PreviewBlend BlendOf(int blend)
    {
        return (MuaBlend)blend switch
        {
            MuaBlend.Add => PreviewBlend.Add,
            MuaBlend.Subtract => PreviewBlend.Subtract,
            _ => PreviewBlend.Normal,
        };
    }
}
