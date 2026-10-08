namespace FbxToMua.Core.Formats.FbxText;

internal sealed class FbxTextModel(FbxTextTree tree)
{
    public const double FbxSecond = 46186158000.0;
    private const double DefaultFrameRate = 30.0;
    public const int MaterialValues = 17;

    private static readonly Dictionary<int, double> TimeModes = new()
    {
        [1] = 120.0, [2] = 100.0, [3] = 60.0, [4] = 50.0, [5] = 48.0, [8] = 29.97, [9] = 29.97,
        [10] = 25.0, [11] = 24.0, [12] = 1000.0, [13] = 23.976, [14] = 47.952, [15] = 59.94,
    };

    public FbxTextTree Tree => tree;

    public List<double>? PropertyNumbers(int node, string name)
    {
        int props = tree.Find(node, "Properties60");

        if (props < 0)
            return null;

        foreach (int entry in tree.All(props, "Property"))
        {
            if (tree.Prop(entry, 0) != name)
                continue;

            return tree.At(entry).Numbers.Count == 0 ? null : tree.At(entry).Numbers;
        }

        return null;
    }

    public bool HasProperty(int node, string name)
    {
        int props = tree.Find(node, "Properties60");

        return props >= 0 && tree.All(props, "Property").Any(entry => tree.Prop(entry, 0) == name);
    }

    public List<double>? PropertyVector(int node, string name)
    {
        List<double>? values = PropertyNumbers(node, name);

        return values is not null && values.Count >= 3 ? values : null;
    }

    public double PropertyScalar(int node, string name, double fallback)
    {
        List<double>? values = PropertyNumbers(node, name);

        return values is not null ? values[0] : fallback;
    }

    public double[] Material(int node)
    {
        double[] value = new double[MaterialValues];
        Colour(node, "DiffuseColor", value, 0);
        Colour(node, "AmbientColor", value, 4);
        Colour(node, "SpecularColor", value, 8);
        Colour(node, "EmissiveColor", value, 12);
        value[16] = PropertyScalar(node, "Shininess", PropertyScalar(node, "ShininessExponent", 0.0));

        return value;
    }

    public DoubleMatrix LocalMatrix(int model, double[]? animT, double[]? animR, double[]? animS)
    {
        DoubleMatrix t = animT is not null ? DoubleMatrix.Translation(animT[0], animT[1], animT[2]) : TranslationProp(model, "Lcl Translation", 1.0);
        DoubleMatrix roff = TranslationProp(model, "RotationOffset", 1.0);
        DoubleMatrix rp = TranslationProp(model, "RotationPivot", 1.0);
        DoubleMatrix rpInv = TranslationProp(model, "RotationPivot", -1.0);
        DoubleMatrix soff = TranslationProp(model, "ScalingOffset", 1.0);
        DoubleMatrix sp = TranslationProp(model, "ScalingPivot", 1.0);
        DoubleMatrix spInv = TranslationProp(model, "ScalingPivot", -1.0);
        DoubleMatrix rpre = RotationProp(model, "PreRotation", 0);
        int order = RotationOrderOf(model);
        DoubleMatrix r = animR is not null ? DoubleMatrix.Rotation(animR[0], animR[1], animR[2], order) : RotationProp(model, "Lcl Rotation", order);
        DoubleMatrix rpostInv = DoubleMatrix.TransposeRotation(RotationProp(model, "PostRotation", 0));
        DoubleMatrix s = animS is not null ? DoubleMatrix.Scaling(animS[0], animS[1], animS[2]) : ScalingProp(model, "Lcl Scaling");

        DoubleMatrix local = spInv;

        foreach (DoubleMatrix next in new[] { s, sp, soff, rpInv, rpostInv, r, rpre, rp, roff, t })
            local = DoubleMatrix.Multiply(local, next);

        return local;
    }

    public DoubleMatrix GeometricMatrix(int model)
    {
        DoubleMatrix s = ScalingProp(model, "GeometricScaling");
        DoubleMatrix r = RotationProp(model, "GeometricRotation", 0);
        DoubleMatrix t = TranslationProp(model, "GeometricTranslation", 1.0);

        return DoubleMatrix.Multiply(DoubleMatrix.Multiply(s, r), t);
    }

    public DoubleMatrix PosedLocal(int model, NodeAnimation animation, double seconds)
    {
        List<double>? staticT = PropertyVector(model, "Lcl Translation");
        List<double>? staticR = PropertyVector(model, "Lcl Rotation");
        List<double>? staticS = PropertyVector(model, "Lcl Scaling");
        double[] t = new double[3];
        double[] r = new double[3];
        double[] s = new double[3];

        for (int i = 0; i < 3; ++i)
        {
            t[i] = animation.Translation[i].Present ? animation.Translation[i].Sample(seconds) : staticT?[i] ?? 0.0;
            r[i] = animation.Rotation[i].Present ? animation.Rotation[i].Sample(seconds) : staticR?[i] ?? 0.0;
            s[i] = animation.Scaling[i].Present ? animation.Scaling[i].Sample(seconds) : staticS?[i] ?? 1.0;
        }

        return LocalMatrix(model, t, r, s);
    }

    public double FrameRate()
    {
        int settings = FindDeep(tree.Root, "GlobalSettings");
        List<double>? mode = settings < 0 ? null : PropertyNumbers(settings, "TimeMode");
        double rate = mode is not null && mode.Count > 0 ? TimeModes.GetValueOrDefault((int)mode[0], DefaultFrameRate) : DefaultFrameRate;

        return rate < 1.0 ? 1.0 : rate;
    }

    public Dictionary<string, NodeAnimation> Takes(out double start, out double end)
    {
        start = 0.0;
        end = 0.0;
        Dictionary<string, NodeAnimation> animations = new(StringComparer.Ordinal);
        int takes = tree.Find(tree.Root, "Takes");
        List<int> list = tree.All(takes, "Take");

        if (takes < 0 || list.Count == 0)
            return animations;

        int take = list[0];
        int span = tree.Find(take, "LocalTime");

        if (span >= 0 && tree.At(span).Numbers.Count >= 2)
        {
            start = tree.At(span).Numbers[0] / FbxSecond;
            end = tree.At(span).Numbers[1] / FbxSecond;
        }

        foreach (int model in tree.All(take, "Model"))
        {
            string name = tree.Prop(model, 0);
            int transform = FindChannel(model, "Transform");

            if (name.Length == 0 || transform < 0)
                continue;

            NodeAnimation animation = new();
            ReadTriple(FindChannel(transform, "T"), animation.Translation);
            ReadTriple(FindChannel(transform, "R"), animation.Rotation);
            ReadTriple(FindChannel(transform, "S"), animation.Scaling);

            if (animation.Keyed)
                animations[name] = animation;
        }

        return animations;
    }

    private void Colour(int node, string name, double[] value, int at)
    {
        List<double>? values = PropertyVector(node, name);

        if (values is null)
            return;

        for (int i = 0; i < 3; ++i)
            value[at + i] = values[i];

        value[at + 3] = 1.0;
    }

    private DoubleMatrix TranslationProp(int model, string name, double sign)
    {
        List<double>? v = PropertyVector(model, name);

        return v is null ? DoubleMatrix.Identity() : DoubleMatrix.Translation(sign * v[0], sign * v[1], sign * v[2]);
    }

    private DoubleMatrix RotationProp(int model, string name, int order)
    {
        List<double>? v = PropertyVector(model, name);

        return v is null ? DoubleMatrix.Identity() : DoubleMatrix.Rotation(v[0], v[1], v[2], order);
    }

    private DoubleMatrix ScalingProp(int model, string name)
    {
        List<double>? v = PropertyVector(model, name);

        return v is null ? DoubleMatrix.Identity() : DoubleMatrix.Scaling(v[0], v[1], v[2]);
    }

    private int RotationOrderOf(int model)
    {
        int props = tree.Find(model, "Properties60");

        if (props < 0)
            return 0;

        foreach (int entry in tree.All(props, "Property"))
        {
            if (tree.Prop(entry, 0) != "RotationOrder")
                continue;

            if (tree.At(entry).Numbers.Count == 0)
                return 0;

            int order = (int)tree.At(entry).Numbers[0];
            return order is >= 0 and <= 5 ? order : 0;
        }

        return 0;
    }

    private int FindDeep(int parent, string name)
    {
        int direct = tree.Find(parent, name);

        if (direct >= 0)
            return direct;

        foreach (int child in tree.At(parent).Children)
        {
            int found = FindDeep(child, name);

            if (found >= 0)
                return found;
        }

        return -1;
    }

    private int FindChannel(int parent, string label)
    {
        if (parent < 0)
            return -1;

        return tree.All(parent, "Channel").FirstOrDefault(child => tree.Prop(child, 0) == label, -1);
    }

    private void ReadTriple(int group, Curve[] curves)
    {
        string[] axes = ["X", "Y", "Z"];

        for (int i = 0; i < 3; ++i)
        {
            int axis = FindChannel(group, axes[i]);

            if (axis >= 0)
                ReadCurve(axis, curves[i]);
        }
    }

    private void ReadCurve(int axis, Curve curve)
    {
        int fallback = tree.Find(axis, "Default");

        if (fallback >= 0 && tree.At(fallback).Numbers.Count > 0)
        {
            curve.Fallback = tree.At(fallback).Numbers[0];
            curve.Present = true;
        }

        int count = tree.Find(axis, "KeyCount");
        int key = tree.Find(axis, "Key");

        if (count < 0 || key < 0 || tree.At(count).Numbers.Count == 0)
            return;

        int keys = (int)tree.At(count).Numbers[0];
        List<double> raw = tree.At(key).Numbers;

        if (keys <= 0 || raw.Count < keys * 2L)
            return;

        curve.Keys = WalkKeys(tree.At(key), keys) ?? Strided(raw, keys);
        curve.Keyed = curve.Keys.Count > 0;
        curve.Present = curve.Present || curve.Keyed;
    }

    private static List<CurveKey> Strided(List<double> raw, int keys)
    {
        int stride = raw.Count / keys;

        if (stride < 2)
            return [];

        return Enumerable.Range(0, keys)
            .Select(i => new CurveKey { Time = raw[i * stride] / FbxSecond, Value = raw[i * stride + 1] })
            .ToList();
    }

    private static List<CurveKey>? WalkKeys(FbxTextNode node, int keys)
    {
        List<CurveKey> walked = [];
        int number = 0;
        int letter = 0;

        for (int i = 0; i < keys; ++i)
        {
            if (number + 2 > node.Numbers.Count)
                return null;

            CurveKey key = new() { Time = node.Numbers[number] / FbxSecond, Value = node.Numbers[number + 1] };
            number += 2;

            if (letter >= node.Props.Count || node.PropAt[letter] != number)
                return null;

            string interpolation = node.Props[letter++];

            if (interpolation is "C" or "L")
            {
                key.Interpolation = interpolation == "C" ? Interpolation.Constant : Interpolation.Linear;
                walked.Add(key);
                continue;
            }

            if (interpolation != "U" || letter >= node.Props.Count || node.PropAt[letter] != number)
                return null;

            ++letter;

            if (number + 2 > node.Numbers.Count)
                return null;

            key.Interpolation = Interpolation.Cubic;
            key.Right = node.Numbers[number];
            key.Left = node.Numbers[number + 1];
            number += 2;

            if (letter >= node.Props.Count || node.PropAt[letter] != number)
                return null;

            if (node.Props[letter++] == "a")
                number += 2;

            walked.Add(key);
        }

        return number == node.Numbers.Count && letter == node.Props.Count ? walked : null;
    }
}
