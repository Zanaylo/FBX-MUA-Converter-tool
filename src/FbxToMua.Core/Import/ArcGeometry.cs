using FbxToMua.Core.Binary;
using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Import;

internal readonly record struct Plate(float Span, float Tall, float Low);

internal readonly record struct Rank(float First, float Second)
{
    public static int Compare(Rank one, Rank other)
    {
        if (one.First < other.First || (one.First == other.First && one.Second < other.Second))
            return -1;

        return other.First < one.First || (other.First == one.First && other.Second < one.Second) ? 1 : 0;
    }
}

internal static class ArcGeometry
{
    public const double Scale = ArcLooks.Uni2Character / ArcLooks.Character;
    public const bool Mirror = true;
    public const bool Flip = true;
    public const int Floats = FbxExNode.VertexFloats;
    public const int MatrixFloats = Geometry.Matrix.Size;
    public const int NormalX = 3;
    public const int NormalY = 4;
    public const int U = 10;
    public const int V = 11;
    public const int Alpha = 9;
    public const int MergedVertices = 65535;
    public const float Parked = -1000.0f;
    public const float LampMark = 128.0f;

    private const float WindowCover = 0.9f;
    private const float WindowBand = 0.6f;
    private const float WindowMatch = 0.25f;
    private const int WindowBands = 8;
    private const int WindowHold = 180;
    private const int ShineSplits = 3;
    private const double SkyDriftY = 0.0005;
    private const double Nearest = 1.0;

    public static Float3 Place(Float3 point, double scale, bool mirror)
    {
        return new Float3((float)(point[0] * scale), (float)(point[1] * scale), (float)(mirror ? -point[2] * scale : point[2] * scale));
    }

    public static Float3 Normalise(Float3 vector)
    {
        double length = Math.Sqrt((double)vector[0] * vector[0] + (double)vector[1] * vector[1] + (double)vector[2] * vector[2]);

        if (length < 1e-9)
            return new Float3(0.0f, 1.0f, 0.0f);

        return new Float3((float)(vector[0] / length), (float)(vector[1] / length), (float)(vector[2] / length));
    }

    public static (float Wide, float Tall) Sides(Float3[] points)
    {
        List<float> apart = [];

        for (int i = 0; i < 4; ++i)
        {
            for (int j = i + 1; j < 4; ++j)
            {
                float total = 0.0f;

                for (int k = 0; k < 3; ++k)
                {
                    float step = points[i][k] - points[j][k];
                    total += step * step;
                }

                apart.Add(MathF.Sqrt(total));
            }
        }

        apart.Sort();

        return (apart[2], apart[0]);
    }

    public static Float3[] Corners(List<float> vertices)
    {
        return Enumerable.Range(0, 4).Select(i => new Float3(vertices[i * Floats], vertices[i * Floats + 1], vertices[i * Floats + 2])).ToArray();
    }

    public static (int Count, int Taken) Divides(List<Plate> siblings, float tall)
    {
        foreach (Plate plate in siblings)
        {
            if (plate.Span <= 0.0f || plate.Span > WindowBand || MathF.Abs(plate.Tall - tall) > WindowMatch * tall)
                continue;

            float bands = 1.0f / plate.Span;
            int whole = (int)(bands + 0.5f);

            if (whole < 2 || whole > WindowBands || MathF.Abs(bands - whole) > WindowMatch)
                continue;

            return (whole, (int)(plate.Low / plate.Span + 0.5f));
        }

        return (1, -1);
    }

    public static (int Count, int Taken) Bands(List<float> vertices, List<FbxExSubmesh> submeshes, ArtSize? size, List<Plate> siblings)
    {
        if (size is null || vertices.Count != 4 * Floats || submeshes.Sum(submesh => submesh.Indices.Count) != 6)
            return (1, -1);

        float low = vertices[V];
        float high = vertices[V];
        float leftU = vertices[U];
        float rightU = vertices[U];

        for (int i = 1; i < 4; ++i)
        {
            low = Std.Min(low, vertices[i * Floats + V]);
            high = Std.Max(high, vertices[i * Floats + V]);
            leftU = Std.Min(leftU, vertices[i * Floats + U]);
            rightU = Std.Max(rightU, vertices[i * Floats + U]);
        }

        if (high - low < WindowCover)
            return (1, -1);

        (float wide, float tall) = Sides(Corners(vertices));
        float across = (rightU - leftU) * size.Value.Width;
        float down = (high - low) * size.Value.Height;

        if (tall <= 0.0f || down <= 0.0f || across <= 0.0f)
            return (1, -1);

        (int whole, int already) = Divides(siblings, tall);

        if (whole < 2)
            return (1, -1);

        float stretch = wide / tall / (across / down);

        return MathF.Abs(stretch - whole) > WindowMatch * whole ? (1, -1) : (whole, already);
    }

    public static List<float> Banded(List<float> vertices, int index, int count)
    {
        float low = vertices[V];
        float high = vertices[V];
        int rows = vertices.Count / Floats;

        for (int i = 1; i < rows; ++i)
        {
            low = Std.Min(low, vertices[i * Floats + V]);
            high = Std.Max(high, vertices[i * Floats + V]);
        }

        float step = (high - low) / count;
        List<float> banded = [.. vertices];

        for (int i = 0; i < rows; ++i)
            banded[i * Floats + V] = low + index * step + (vertices[i * Floats + V] - low) / count;

        return banded;
    }

    public static List<float> Framed(List<float> vertices, EvbRect rect, ArtSize size, bool flip)
    {
        int rows = vertices.Count / Floats;
        List<float> framed = [.. vertices];

        for (int i = 0; i < rows; ++i)
        {
            double u = vertices[i * Floats + U];
            double v = flip ? 1.0 - vertices[i * Floats + V] : vertices[i * Floats + V];
            double across = (u * rect.W + rect.X) / size.Width;
            double down = (v * rect.H + rect.Y) / size.Height;

            framed[i * Floats + U] = (float)across;
            framed[i * Floats + V] = (float)(flip ? 1.0 - down : down);
        }

        return framed;
    }

    public static List<float> Shown(int index, int count, int phase)
    {
        int frames = count * WindowHold;
        List<float> shown = new(frames * MatrixFloats);

        for (int frame = 0; frame < frames; ++frame)
        {
            bool up = (frame / WindowHold + phase) % count == index;
            shown.AddRange(Rest());

            if (!up)
                shown[^3] = -1000.0f;
        }

        return shown;
    }

    public static List<float> Rest() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    public static void Shift(List<float> vertices, int column, float by)
    {
        int rows = vertices.Count / Floats;

        for (int i = 0; i < rows; ++i)
            vertices[i * Floats + column] += by;
    }

    public static void MarkFlow(List<float> vertices, float mark)
    {
        if (mark != 0.0f)
            Shift(vertices, U, mark);
    }

    public static List<float> Squeezed(List<float> vertices)
    {
        List<float> squeezed = [.. vertices];
        int rows = squeezed.Count / Floats;

        for (int i = 0; i < rows; ++i)
        {
            squeezed[i * Floats + U] = ImMarks.FlipInset + ImMarks.FlipSpan * squeezed[i * Floats + U];
            squeezed[i * Floats + V] = ImMarks.FlipInset + ImMarks.FlipSpan * squeezed[i * Floats + V];
        }

        return squeezed;
    }

    public static void Mark(List<float> vertices, int lamp)
    {
        if (lamp >= 0)
            Shift(vertices, V, -LampMark * (lamp + 1));
    }

    public static void Append(List<float> entry, List<float>? frame, int at)
    {
        if (frame is null || at + MatrixFloats > frame.Count)
        {
            entry.AddRange(Rest());
            return;
        }

        entry.AddRange(frame.GetRange(at, MatrixFloats));
    }

    public static List<float> ParkedMotion(List<int> frames, int rect, List<float> motion)
    {
        List<float> source = motion.Count < MatrixFloats ? Rest() : motion;
        int steps = source.Count / MatrixFloats;
        List<float> parked = new(frames.Count * MatrixFloats);

        for (int i = 0; i < frames.Count; ++i)
        {
            parked.AddRange(source.GetRange(i % steps * MatrixFloats, MatrixFloats));

            if (frames[i] != rect)
                parked[^3] += Parked;
        }

        return parked;
    }

    public static byte[] RawDds(int side, byte[] rgba)
    {
        byte[] dds = new byte[128 + rgba.Length];
        LittleEndian.Latin1("DDS ").CopyTo(dds, 0);
        uint[] fields = [124, 0x1 | 0x2 | 0x4 | 0x8 | 0x1000, (uint)side, (uint)side, (uint)side * 4];
        uint[] format = [32, 0x41, 0, 32];
        uint[] masks = [0x00ff0000, 0x0000ff00, 0x000000ff, 0xff000000];

        for (int i = 0; i < fields.Length; ++i)
            LittleEndian.Put(dds, 4 + i * 4, fields[i]);

        for (int i = 0; i < format.Length; ++i)
            LittleEndian.Put(dds, 76 + i * 4, format[i]);

        for (int i = 0; i < masks.Length; ++i)
            LittleEndian.Put(dds, 92 + i * 4, masks[i]);

        LittleEndian.Put(dds, 108, 0x1000);

        for (int at = 0; at + 3 < rgba.Length; at += 4)
        {
            dds[128 + at] = rgba[at + 2];
            dds[128 + at + 1] = rgba[at + 1];
            dds[128 + at + 2] = rgba[at];
            dds[128 + at + 3] = rgba[at + 3];
        }

        return dds;
    }

    public static List<float> PoseMatrix(CardPose pose)
    {
        List<float> matrix = Rest();

        if (!pose.Shown)
        {
            matrix[0] = 0.0f;
            matrix[5] = 0.0f;
            matrix[13] = Parked;
            return matrix;
        }

        Float3 place = Place(new Float3((float)pose.Position[0], (float)pose.Position[1], (float)pose.Position[2]), Scale, Mirror);
        double c = Math.Cos(-pose.Turn);
        double s = Math.Sin(-pose.Turn);
        double wide = pose.Size[0] * Scale;
        double tall = pose.Size[1] * Scale;

        matrix[0] = (float)(wide * c);
        matrix[1] = (float)(wide * s);
        matrix[4] = (float)(-tall * s);
        matrix[5] = (float)(tall * c);
        matrix[12] = place[0];
        matrix[13] = place[1];
        matrix[14] = place[2];

        return matrix;
    }

    public static FbxExNode CardQuad(double[] cell, double[] tint, int material)
    {
        float[,] corners = { { -0.5f, -0.5f }, { 0.5f, -0.5f }, { 0.5f, 0.5f }, { -0.5f, 0.5f } };
        FbxExNode node = FbxExNode.Leaf();

        for (int k = 0; k < 4; ++k)
        {
            double u = corners[k, 0] < 0.0f ? cell[0] : cell[2];
            double v = corners[k, 1] > 0.0f ? cell[1] : cell[3];
            node.Vertices.AddRange([corners[k, 0], corners[k, 1], 0.0f, 0.0f, 0.0f, Mirror ? -1.0f : 1.0f,
                (float)tint[0], (float)tint[1], (float)tint[2], 1.0f, (float)u, (float)(Flip ? 1.0 - v : v)]);
        }

        node.Submeshes.Add(new FbxExSubmesh { Material = material, Indices = [0, 1, 2, 0, 2, 3, 0, 2, 1, 0, 3, 2] });
        node.BlendMode = 1;

        return node;
    }

    public static (List<float> Vertices, List<int> Rigged) Vertices(MuaReader model, MuaReadMesh mesh)
    {
        List<float> vertices = new(mesh.Vertices * Floats);
        List<int> rigged = new(mesh.Vertices);

        for (int v = 0; v < mesh.Vertices; ++v)
        {
            MuaReadVertex raw = model.VertexAt(mesh.FirstVertex + v);
            rigged.Add(raw.Bone);
            Float3 position = Place(raw.Position, Scale, Mirror);
            Float3 normal = Normalise(new Float3(raw.Normal[0], raw.Normal[1], Mirror ? -raw.Normal[2] : raw.Normal[2]));

            vertices.AddRange([position[0], position[1], position[2], normal[0], normal[1], normal[2],
                (float)(raw.Colour.R / 255.0), (float)(raw.Colour.G / 255.0), (float)(raw.Colour.B / 255.0), (float)(raw.Colour.A / 255.0),
                raw.U, Flip ? (float)(1.0 - raw.V) : raw.V]);
        }

        return (vertices, rigged);
    }

    public static List<FbxExSubmesh> Submeshes(MuaReader model, MuaReadMesh mesh, int materials, bool twoSided)
    {
        List<FbxExSubmesh> submeshes = [];
        bool flipped = Mirror != mesh.Reversed;

        for (int p = 0; p < mesh.Parts; ++p)
        {
            int index = mesh.FirstPart + p;

            if (index < 0 || index >= model.Parts.Count)
                continue;

            MuaPart part = model.Parts[index];
            FbxExSubmesh submesh = new() { Material = part.Material >= 0 && part.Material < materials ? part.Material : 0 };

            foreach (MuaTriangle triangle in model.Triangles(part))
            {
                if (triangle.A >= mesh.Vertices || triangle.B >= mesh.Vertices || triangle.C >= mesh.Vertices)
                    continue;

                Wind(triangle, flipped, twoSided, submesh.Indices);
            }

            if (submesh.Indices.Count > 0)
                submeshes.Add(submesh);
        }

        return submeshes;
    }

    public static void Subdivided(List<float> vertices, List<int> rigged, List<FbxExSubmesh> submeshes)
    {
        for (int level = 0; level < ShineSplits; ++level)
        {
            Dictionary<(int, int), int> made = [];

            foreach (FbxExSubmesh submesh in submeshes)
            {
                List<int> finer = [];

                for (int i = 0; i + 2 < submesh.Indices.Count; i += 3)
                {
                    int a = submesh.Indices[i];
                    int b = submesh.Indices[i + 1];
                    int c = submesh.Indices[i + 2];
                    int ab = Midpoint(vertices, rigged, made, a, b);
                    int bc = Midpoint(vertices, rigged, made, b, c);
                    int ca = Midpoint(vertices, rigged, made, c, a);
                    finer.AddRange([a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca]);
                }

                submesh.Indices = finer;
            }
        }
    }

    public static void ScreenMapped(List<float> vertices, Lens lens)
    {
        double half = Math.Tan(lens.Fov * Math.PI / 360.0);
        double drift = lens.EyeHeight * SkyDriftY;
        int rows = vertices.Count / Floats;

        for (int v = 0; v < rows; ++v)
        {
            int row = v * Floats;
            double x = vertices[row] / Scale;
            double y = vertices[row + 1] / Scale;
            double z = (Mirror ? -vertices[row + 2] : vertices[row + 2]) / Scale;
            double depth = Std.Max(Nearest, z + lens.EyeDistance);
            double across = x / (depth * half * BattleCamera.Aspect);
            double down = (y - lens.EyeHeight) / (depth * half) + drift;

            vertices[row + U] = (float)across;
            vertices[row + V] = (float)(Flip ? 1.0 - down : down);
        }
    }

    public static void Reflect(List<float> vertices)
    {
        int rows = vertices.Count / Floats;

        for (int i = 0; i < rows; ++i)
        {
            int row = i * Floats;
            vertices[row + U] = 0.5f + 0.5f * vertices[row + NormalX];
            vertices[row + V] = 0.5f + 0.5f * vertices[row + NormalY];
        }
    }

    public static List<(int Bone, FbxExNode Node)> Split(List<float> vertices, List<int> rigged, List<FbxExSubmesh> submeshes, int unrigged)
    {
        SortedDictionary<int, SortedDictionary<int, List<int>>> groups = [];

        foreach (FbxExSubmesh submesh in submeshes)
        {
            for (int i = 0; i + 2 < submesh.Indices.Count; i += 3)
            {
                int lead = submesh.Indices[i];
                int held = lead >= 0 && lead < rigged.Count ? rigged[lead] : -1;
                int bone = held < 0 ? unrigged : held;

                if (!groups.TryGetValue(bone, out SortedDictionary<int, List<int>>? byMaterial))
                    groups[bone] = byMaterial = [];

                if (!byMaterial.TryGetValue(submesh.Material, out List<int>? flat))
                    byMaterial[submesh.Material] = flat = [];

                flat.AddRange([submesh.Indices[i], submesh.Indices[i + 1], submesh.Indices[i + 2]]);
            }
        }

        List<(int, FbxExNode)> split = [];

        foreach ((int bone, SortedDictionary<int, List<int>> parts) in groups)
        {
            Dictionary<int, int> remap = [];
            FbxExNode node = FbxExNode.Leaf();

            foreach ((int material, List<int> indices) in parts)
            {
                FbxExSubmesh submesh = new() { Material = material };

                foreach (int index in indices)
                {
                    if (!remap.TryGetValue(index, out int mapped))
                    {
                        mapped = node.VertexCount;
                        remap[index] = mapped;
                        node.Vertices.AddRange(vertices.GetRange(index * Floats, Floats));
                    }

                    submesh.Indices.Add(mapped);
                }

                node.Submeshes.Add(submesh);
            }

            split.Add((bone, node));
        }

        return split;
    }

    public static bool Mergeable(FbxExNode heldNode, List<float> heldAnime, bool heldClear, FbxExNode node, List<float> anime, bool clear)
    {
        if (heldAnime.Count != MatrixFloats || !heldAnime.SequenceEqual(anime) || heldClear != clear)
            return false;

        if (heldNode.BlendMode != node.BlendMode || !heldNode.Matrix.Equals(node.Matrix))
            return false;

        return heldNode.Vertices.Count + node.Vertices.Count <= MergedVertices * Floats;
    }

    public static void Merge(FbxExNode into, FbxExNode node)
    {
        int offset = into.VertexCount;
        into.Vertices.AddRange(node.Vertices);

        foreach (FbxExSubmesh submesh in node.Submeshes)
        {
            if (into.Submeshes.Count == 0 || into.Submeshes[^1].Material != submesh.Material)
                into.Submeshes.Add(new FbxExSubmesh { Material = submesh.Material });

            into.Submeshes[^1].Indices.AddRange(submesh.Indices.Select(index => index + offset));
        }
    }

    private static void Wind(MuaTriangle triangle, bool flipped, bool twoSided, List<int> indices)
    {
        indices.Add(triangle.A);
        indices.Add(flipped ? triangle.C : triangle.B);
        indices.Add(flipped ? triangle.B : triangle.C);

        if (!twoSided)
            return;

        indices.Add(triangle.A);
        indices.Add(flipped ? triangle.B : triangle.C);
        indices.Add(flipped ? triangle.C : triangle.B);
    }

    private static int Midpoint(List<float> vertices, List<int> rigged, Dictionary<(int, int), int> made, int a, int b)
    {
        (int, int) key = (Math.Min(a, b), Math.Max(a, b));

        if (made.TryGetValue(key, out int known))
            return known;

        int index = vertices.Count / Floats;

        for (int k = 0; k < Floats; ++k)
            vertices.Add(0.5f * (vertices[a * Floats + k] + vertices[b * Floats + k]));

        rigged.Add(rigged[a]);
        made[key] = index;

        return index;
    }
}
