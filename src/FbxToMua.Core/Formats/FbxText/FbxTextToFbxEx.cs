using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.FbxEx;

namespace FbxToMua.Core.Formats.FbxText;

public static class FbxTextToFbxEx
{
    private const int MaxFrames = 3600;
    private const string ScenePrefix = "Model::Scene";

    private sealed class NodeOut
    {
        public int Type { get; set; }
        public int Child { get; set; } = -1;
        public int Sibling { get; set; } = -1;
        public int BlendMode { get; set; }
        public DoubleMatrix Local { get; set; } = DoubleMatrix.Identity();
        public DoubleMatrix World { get; set; } = DoubleMatrix.Identity();
        public List<float> Vertices { get; } = [];
        public List<FbxExSubmesh> Submeshes { get; } = [];
    }

    private sealed record LayerData(List<double>? Values, string Mapping, string Reference, List<double>? Index);

    private sealed class Graph
    {
        public Dictionary<string, int> ByName { get; } = new(StringComparer.Ordinal);
        public List<string> Order { get; } = [];
        public Dictionary<string, string> Parent { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> MeshTextures { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> MeshMaterials { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, List<string>> Kids { get; } = new(StringComparer.Ordinal);
        public List<string> WalkOrder { get; } = [];
        public Dictionary<string, int> Slot { get; } = new(StringComparer.Ordinal);

        public List<string> KidsOf(string name) => Kids.TryGetValue(name, out List<string>? kids) ? kids : [];
    }

    private sealed class Output
    {
        public List<FbxExMaterial> Materials { get; } = [];
        public List<string> TextureNames { get; } = [];
        public Dictionary<string, int> TextureSlot { get; } = new(StringComparer.Ordinal);
    }

    public static byte[] Convert(byte[] fbx)
    {
        FbxTextTree tree = FbxTextTree.Parse(fbx) ?? throw new StageConversionException("that bg.fbx could not be read as FBX text");
        FbxTextModel scene = new(tree);
        int objects = tree.Find(tree.Root, "Objects");

        if (objects < 0)
            throw new StageConversionException("that bg.fbx has no Objects section");

        Graph graph = BuildGraph(tree, objects);

        if (graph.ByName.Count == 0)
            throw new StageConversionException("that bg.fbx holds no models");

        Dictionary<string, double[]> materialProps = new(StringComparer.Ordinal);

        foreach (int surface in tree.All(objects, "Material"))
            materialProps[tree.Prop(surface, 0)] = scene.Material(surface);

        Dictionary<string, NodeAnimation> animations = scene.Takes(out double takeStart, out double takeEnd);
        double rate = scene.FrameRate();
        int frames = 1;

        if (animations.Count > 0 && takeEnd > takeStart)
        {
            frames = (int)((takeEnd - takeStart) * rate + 0.5);
            frames = frames < 2 ? 1 : Math.Min(frames, MaxFrames);
        }

        Output output = new();
        NodeOut[] nodes = BuildNodes(scene, graph, animations, frames, takeStart, materialProps, output);
        LinkSiblings(graph, nodes);

        return FbxExWriter.Build(Model(scene, graph, nodes, output, animations, frames, rate));
    }

    private static Graph BuildGraph(FbxTextTree tree, int objects)
    {
        Graph graph = new();

        foreach (int model in tree.All(objects, "Model"))
        {
            string name = tree.Prop(model, 0);

            if (name.Length == 0 || graph.ByName.ContainsKey(name))
                continue;

            graph.ByName[name] = model;
            graph.Order.Add(name);
        }

        Dictionary<string, string> textureFile = new(StringComparer.Ordinal);

        foreach (int texture in tree.All(objects, "Texture"))
        {
            int file = tree.Find(texture, "RelativeFilename");
            file = file >= 0 ? file : tree.Find(texture, "FileName");

            if (file >= 0)
                textureFile[tree.Prop(texture, 0)] = BaseName(tree.Prop(file, 0));
        }

        ReadConnections(tree, graph, textureFile);

        foreach (string name in graph.Order)
        {
            string owner = graph.Parent.GetValueOrDefault(name, ScenePrefix);
            Add(graph.Kids, owner, name);
        }

        Walk(graph);

        for (int i = 0; i < graph.WalkOrder.Count; ++i)
            graph.Slot[graph.WalkOrder[i]] = i;

        return graph;
    }

    private static void ReadConnections(FbxTextTree tree, Graph graph, Dictionary<string, string> textureFile)
    {
        int connections = tree.Find(tree.Root, "Connections");

        foreach (int link in tree.All(connections, "Connect"))
        {
            if (tree.Prop(link, 0) != "OO")
                continue;

            string source = tree.Prop(link, 1);
            string target = tree.Prop(link, 2);

            if (!target.StartsWith("Model::", StringComparison.Ordinal))
                continue;

            if (source.StartsWith("Model::", StringComparison.Ordinal))
                graph.Parent[source] = target;
            else if (source.StartsWith("Material::", StringComparison.Ordinal))
                Add(graph.MeshMaterials, target, source);
            else if (source.StartsWith("Texture::", StringComparison.Ordinal))
                Add(graph.MeshTextures, target, textureFile.GetValueOrDefault(source, string.Empty));
        }
    }

    private static void Add(Dictionary<string, List<string>> lists, string key, string value)
    {
        if (!lists.TryGetValue(key, out List<string>? list))
            lists[key] = list = [];

        list.Add(value);
    }

    private static void Walk(Graph graph)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        Stack<string> stack = new();

        foreach (string name in graph.Order)
        {
            if (graph.Parent.TryGetValue(name, out string? up) && graph.ByName.ContainsKey(up))
                continue;

            stack.Push(name);

            while (stack.Count > 0)
            {
                string current = stack.Pop();

                if (!seen.Add(current))
                    continue;

                graph.WalkOrder.Add(current);
                List<string> kids = graph.KidsOf(current);

                for (int i = kids.Count; i > 0; --i)
                    stack.Push(kids[i - 1]);
            }
        }

        foreach (string name in graph.Order)
        {
            if (seen.Add(name))
                graph.WalkOrder.Add(name);
        }
    }

    private static string BaseName(string path)
    {
        int cut = path.LastIndexOfAny(['\\', '/']);

        return cut < 0 ? path : path[(cut + 1)..];
    }

    private static LayerData Layer(FbxTextTree tree, int model, string layer, string key, string indexKey)
    {
        int element = tree.Find(model, layer);

        if (element < 0)
            return new LayerData(null, string.Empty, string.Empty, null);

        int data = tree.Find(element, key);
        int index = tree.Find(element, indexKey);

        return new LayerData(data < 0 ? null : tree.At(data).Numbers,
            tree.Prop(tree.Find(element, "MappingInformationType"), 0),
            tree.Prop(tree.Find(element, "ReferenceInformationType"), 0),
            index < 0 ? null : tree.At(index).Numbers);
    }

    private static NodeOut[] BuildNodes(FbxTextModel scene, Graph graph, Dictionary<string, NodeAnimation> animations, int frames,
        double takeStart, Dictionary<string, double[]> materialProps, Output output)
    {
        FbxTextTree tree = scene.Tree;
        NodeOut[] nodes = new NodeOut[graph.WalkOrder.Count];
        Dictionary<string, DoubleMatrix> world = new(StringComparer.Ordinal);

        for (int index = 0; index < graph.WalkOrder.Count; ++index)
        {
            string name = graph.WalkOrder[index];
            int model = graph.ByName[name];
            NodeOut node = new() { BlendMode = scene.HasProperty(model, "adding") ? 1 : 0 };
            nodes[index] = node;

            int first = -1;

            foreach (string kid in graph.KidsOf(name))
            {
                if (graph.Slot.TryGetValue(kid, out int at) && (first < 0 || at < first))
                    first = at;
            }

            node.Child = first;

            DoubleMatrix local = animations.TryGetValue(name, out NodeAnimation? posed) && frames > 1
                ? scene.PosedLocal(model, posed, takeStart)
                : scene.LocalMatrix(model, null, null, null);
            DoubleMatrix accumulated = graph.Parent.TryGetValue(name, out string? up) && world.TryGetValue(up, out DoubleMatrix? above)
                ? DoubleMatrix.Multiply(local, above)
                : local;

            world[name] = accumulated;
            node.Local = local;
            node.World = accumulated;

            BuildMesh(scene, graph, model, name, node, materialProps, output);
        }

        return nodes;
    }

    private static void BuildMesh(FbxTextModel scene, Graph graph, int model, string name, NodeOut node,
        Dictionary<string, double[]> materialProps, Output output)
    {
        FbxTextTree tree = scene.Tree;
        int verticesNode = tree.Find(model, "Vertices");
        int polygonNode = tree.Find(model, "PolygonVertexIndex");

        if (verticesNode < 0 || tree.At(verticesNode).Numbers.Count == 0 || polygonNode < 0)
            return;

        List<double> positions = tree.At(verticesNode).Numbers;
        List<double> polygons = tree.At(polygonNode).Numbers;
        LayerData normals = Layer(tree, model, "LayerElementNormal", "Normals", string.Empty);
        LayerData uvs = Layer(tree, model, "LayerElementUV", "UV", "UVIndex");
        LayerData colours = Layer(tree, model, "LayerElementColor", "Colors", "ColorIndex");
        LayerData materialLayer = Layer(tree, model, "LayerElementMaterial", "Materials", string.Empty);
        LayerData textureLayer = Layer(tree, model, "LayerElementTexture", "TextureId", string.Empty);
        DoubleMatrix geometric = scene.GeometricMatrix(model);

        SortedDictionary<int, List<int>> byMaterial = [];
        Dictionary<int, int> groupSurface = [];
        List<(int Vertex, int Corner)> face = [];
        int polygon = 0;

        for (int k = 0; k < polygons.Count; ++k)
        {
            int raw = (int)polygons[k];
            face.Add((raw >= 0 ? raw : -raw - 1, k));

            if (raw >= 0)
                continue;

            int surface = Choose(materialLayer, polygon, 0);
            int material = Choose(textureLayer, polygon, surface);
            groupSurface.TryAdd(material, surface);

            int baseVertex = node.Vertices.Count / FbxExNode.VertexFloats;

            foreach ((int vertex, int corner) in face)
                AddVertex(node, geometric, positions, normals, uvs, colours, vertex, corner);

            if (!byMaterial.TryGetValue(material, out List<int>? list))
                byMaterial[material] = list = [];

            for (int j = 1; j + 1 < face.Count; ++j)
                list.AddRange([baseVertex, baseVertex + j, baseVertex + j + 1]);

            face.Clear();
            ++polygon;
        }

        List<string> names = graph.MeshTextures.GetValueOrDefault(name, []);
        List<string> surfaceNames = graph.MeshMaterials.GetValueOrDefault(name, []);

        foreach ((int key, List<int> indices) in byMaterial)
        {
            string fileName = key >= 0 && key < names.Count ? names[key] : names.Count > 0 ? names[0] : string.Empty;

            if (!output.TextureSlot.ContainsKey(fileName))
            {
                output.TextureSlot[fileName] = output.TextureNames.Count;
                output.TextureNames.Add(fileName);
            }

            int surface = groupSurface.GetValueOrDefault(key, 0);
            double[] props = surface >= 0 && surface < surfaceNames.Count && materialProps.TryGetValue(surfaceNames[surface], out double[]? described)
                ? described
                : new double[FbxTextModel.MaterialValues];

            output.Materials.Add(new FbxExMaterial
            {
                FileName = fileName,
                TextureIndex = output.TextureSlot[fileName],
                Value = props.Select(value => (float)value).ToArray(),
            });

            node.Submeshes.Add(new FbxExSubmesh { Material = output.Materials.Count - 1, Indices = indices });
        }

        node.Type = FbxExNode.MeshType;
    }

    private static int Choose(LayerData layer, int polygon, int fallback)
    {
        if (layer.Values is null || layer.Values.Count == 0)
            return fallback;

        if (layer.Mapping == "AllSame")
            return (int)layer.Values[0];

        return polygon < layer.Values.Count ? (int)layer.Values[polygon] : fallback;
    }

    private static long Direct(LayerData layer, int vertex, int corner)
    {
        long index = layer.Mapping == "ByVertice" ? vertex : corner;

        if (layer.Reference == "IndexToDirect" && layer.Index is not null && corner < layer.Index.Count)
            index = (long)layer.Index[corner];

        return index;
    }

    private static void AddVertex(NodeOut node, DoubleMatrix geometric, List<double> positions, LayerData normals, LayerData uvs,
        LayerData colours, int vertex, int corner)
    {
        long at = (long)vertex * 3;
        double[] position = at + 2 < positions.Count ? geometric.Apply(positions[(int)at], positions[(int)at + 1], positions[(int)at + 2], 1.0) : [0.0, 0.0, 0.0];
        double[] normal = [0.0, 1.0, 0.0];

        if (normals.Values is not null && normals.Values.Count > 0)
        {
            long ni = (long)(normals.Mapping == "ByVertice" ? vertex : corner) * 3;

            if (ni + 2 < normals.Values.Count)
                normal = geometric.Apply(normals.Values[(int)ni], normals.Values[(int)ni + 1], normals.Values[(int)ni + 2], 0.0);
        }

        Normalize(normal);

        double u = 0.0;
        double v = 0.0;

        if (uvs.Values is not null && uvs.Values.Count > 0)
        {
            long ui = Direct(uvs, vertex, corner);

            if (ui >= 0 && ui * 2 + 1 < uvs.Values.Count)
            {
                u = uvs.Values[(int)(ui * 2)];
                v = uvs.Values[(int)(ui * 2 + 1)];
            }
        }

        double[] tint = [1.0, 1.0, 1.0, 1.0];

        if (colours.Values is not null && colours.Values.Count > 0)
        {
            long ci = Direct(colours, vertex, corner);

            if (ci >= 0 && ci * 4 + 3 < colours.Values.Count)
            {
                for (int i = 0; i < 4; ++i)
                    tint[i] = colours.Values[(int)(ci * 4 + i)];
            }
        }

        double[] values = [position[0], position[1], position[2], normal[0], normal[1], normal[2], tint[0], tint[1], tint[2], tint[3], u, v];

        foreach (double value in values)
            node.Vertices.Add((float)value);
    }

    private static void Normalize(double[] v)
    {
        double length = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);

        if (length <= 1e-8)
        {
            v[0] = 0.0;
            v[1] = 1.0;
            v[2] = 0.0;
            return;
        }

        v[0] /= length;
        v[1] /= length;
        v[2] /= length;
    }

    private static void LinkSiblings(Graph graph, NodeOut[] nodes)
    {
        foreach (string name in graph.WalkOrder)
        {
            List<int> sorted = graph.KidsOf(name).Where(graph.Slot.ContainsKey).Select(kid => graph.Slot[kid]).Order().ToList();

            for (int i = 0; i + 1 < sorted.Count; ++i)
                nodes[sorted[i]].Sibling = sorted[i + 1];
        }

        List<int> roots = graph.WalkOrder
            .Where(name => !graph.Parent.TryGetValue(name, out string? up) || !graph.ByName.ContainsKey(up))
            .Select(name => graph.Slot[name]).Order().ToList();

        for (int i = 0; i + 1 < roots.Count; ++i)
            nodes[roots[i]].Sibling = roots[i + 1];
    }

    private static FbxExModel Model(FbxTextModel scene, Graph graph, NodeOut[] nodes, Output output,
        Dictionary<string, NodeAnimation> animations, int frames, double rate)
    {
        FbxExModel built = new()
        {
            Textures = output.TextureNames.Count == 0 ? [string.Empty] : output.TextureNames,
            Materials = output.Materials,
        };

        FbxExNode root = FbxExNode.Branch();
        root.Child = nodes.Length == 0 ? -1 : 1;
        built.Nodes.Add(root);
        built.Animes.Add(Track(DoubleMatrix.Identity()));

        foreach (NodeOut node in nodes)
        {
            FbxExNode written = new()
            {
                Type = node.Type,
                Child = node.Child >= 0 ? node.Child + 1 : -1,
                Sibling = node.Sibling >= 0 ? node.Sibling + 1 : -1,
                BlendMode = node.BlendMode,
                Vertices = node.Vertices,
                Submeshes = node.Submeshes,
            };

            for (int i = 0; i < Geometry.Matrix.Size; ++i)
                written.Matrix[i] = (float)node.World.M[i];

            built.Nodes.Add(written);
        }

        for (int slot = 0; slot < graph.WalkOrder.Count; ++slot)
            built.Animes.Add(AnimeOf(scene, graph, nodes[slot], graph.WalkOrder[slot], animations, frames, rate));

        return built;
    }

    private static List<float> AnimeOf(FbxTextModel scene, Graph graph, NodeOut node, string name,
        Dictionary<string, NodeAnimation> animations, int frames, double rate)
    {
        int nodeFrames = 0;
        double first = 0.0;

        if (animations.TryGetValue(name, out NodeAnimation? animation) && frames > 1 && animation.TryKeySpan(out first, out double last))
            nodeFrames = Math.Min((int)((last - first) * rate + 0.5), MaxFrames);

        if (nodeFrames < 2 || animation is null)
            return Track(node.Local);

        List<float> track = [];
        int model = graph.ByName[name];

        for (int frame = 0; frame < nodeFrames; ++frame)
            track.AddRange(Track(scene.PosedLocal(model, animation, first + (double)frame / rate)));

        return track;
    }

    private static List<float> Track(DoubleMatrix matrix) => matrix.M.Select(value => (float)value).ToList();
}
