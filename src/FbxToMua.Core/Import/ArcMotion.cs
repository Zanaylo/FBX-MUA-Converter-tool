using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Import;

internal sealed class Track(MmotMotion motion, List<List<float>> frames)
{
    public MmotMotion Motion { get; } = motion;
    public List<List<float>> Frames { get; } = frames;
}

internal static class ArcMotion
{
    public const string ModelTake = "(Native)";
    private const uint NoDefaultTake = 0x10;
    private const int DeepestChain = 256;

    public static string TakeName(string picked) => picked.Length == 0 ? ModelTake : Lowered(picked);

    public static string Lowered(string text) => string.Create(text.Length, text, (span, source) =>
    {
        for (int i = 0; i < source.Length; ++i)
            span[i] = source[i] is >= 'A' and <= 'Z' ? (char)(source[i] - 'A' + 'a') : source[i];
    });

    public static SortedDictionary<string, MmotMotion> Motions(SortedDictionary<string, byte[]> scene)
    {
        SortedDictionary<string, MmotMotion> motions = new(StringComparer.Ordinal);

        foreach ((string path, byte[] data) in scene)
        {
            string leaf = Lowered(path);

            if (!leaf.EndsWith(".mmot", StringComparison.Ordinal) || !leaf.StartsWith("mot/", StringComparison.Ordinal))
                continue;

            MmotMotion? motion = MmotMotion.Read(data);

            if (motion is not null && motion.Frames >= 2)
                motions[leaf[(leaf.LastIndexOfAny(['/', '\\']) + 1)..]] = motion;
        }

        return motions;
    }

    public static void Bind(MuaReader model, SortedDictionary<string, MmotMotion> files, int first, string picked, Dictionary<int, Dictionary<string, Track>> moving)
    {
        string name = TakeName(picked);

        if (picked.Length == 0 || (moving.TryGetValue(first, out Dictionary<string, Track>? known) && known.ContainsKey(name)))
            return;

        if (!files.TryGetValue(name, out MmotMotion? motion))
            return;

        List<List<float>> frames = Deltas(model, first, motion);

        if (frames.Count == 0)
            return;

        TakesOf(moving, first)[name] = new Track(motion, frames);
    }

    public static void Internal(MuaReader model, Dictionary<int, Dictionary<string, Track>> moving)
    {
        HashSet<int> wanted = model.Meshes.Where(mesh => mesh.Vertices > 0).Select(mesh => mesh.Bone).ToHashSet();

        foreach (MuaReadSkeleton skeleton in model.Skeletons)
        {
            int first = skeleton.FirstBone;
            int count = skeleton.Bones;

            if (!wanted.Contains(first) || (skeleton.Flags & NoDefaultTake) != 0)
                continue;

            if (first < 0 || count < 1 || first + count > model.Bones.Count || !Moves(model, first, count))
                continue;

            MmotMotion motion = Adopt(model, first, count);

            if (motion.Frames < 2)
                continue;

            TakesOf(moving, first)[ModelTake] = new Track(motion, Deltas(model, first, motion));
        }
    }

    public static List<float> MotionOf(int root, int bone, EvbRun? run, Dictionary<int, Dictionary<string, Track>> moving)
    {
        List<float> entry = [];

        if (!moving.TryGetValue(root, out Dictionary<string, Track>? takes) || bone < 0 || takes.Count == 0)
            return entry;

        int at = bone * ArcGeometry.MatrixFloats;

        if (run is null)
        {
            if (!takes.TryGetValue(ModelTake, out Track? native))
                return entry;

            foreach (List<float> step in native.Frames)
                ArcGeometry.Append(entry, step, at);

            return entry;
        }

        List<EvbStep> steps = run.Frame;

        if (steps.Count == 0)
            return entry;

        EvbStep last = steps[^1];
        takes.TryGetValue(TakeName(last.Take), out Track? held);

        if (steps.All(one => one.Take == last.Take))
        {
            if (held is null || held.Frames.Count == 0)
                return entry;

            for (int i = 0; i < held.Frames.Count; ++i)
                ArcGeometry.Append(entry, held.Frames[Wrapped(steps[0].At + (long)i, held.Frames.Count)], at);

            return entry;
        }

        int began = steps.Count - 1 - last.At;
        int length = steps.Count;

        if (run.Settled && held is not null && held.Frames.Count > 1)
            length = Math.Max(length, Math.Max(0, began + held.Frames.Count));

        for (int i = 0; i < length; ++i)
        {
            EvbStep step = i < steps.Count ? steps[i] : new EvbStep(last.Take, i - began);

            if (!takes.TryGetValue(TakeName(step.Take), out Track? take) || take.Frames.Count == 0)
            {
                ArcGeometry.Append(entry, null, 0);
                continue;
            }

            ArcGeometry.Append(entry, take.Frames[Wrapped(step.At, take.Frames.Count)], at);
        }

        return entry;
    }

    private static int Wrapped(long at, int count) => (int)(unchecked((ulong)at) % (ulong)count);

    private static Dictionary<string, Track> TakesOf(Dictionary<int, Dictionary<string, Track>> moving, int first)
    {
        if (!moving.TryGetValue(first, out Dictionary<string, Track>? takes))
            moving[first] = takes = new Dictionary<string, Track>(StringComparer.Ordinal);

        return takes;
    }

    private static Matrix Chain(MuaReader model, int first, int local, List<Matrix> matrices)
    {
        Matrix chained = Matrix.Identity;
        bool started = false;
        int index = local;
        int guard = 0;

        while (index >= 0 && index < matrices.Count && guard++ < DeepestChain)
        {
            chained = started ? MatrixMath.Multiply(chained, matrices[index]) : matrices[index];
            started = true;
            int bone = first + index;

            if (bone < 0 || bone >= model.Bones.Count)
                break;

            index = model.Bones[bone].Parent;
        }

        return chained;
    }

    private static int BonesOf(MuaReader model, int first)
    {
        foreach (MuaReadSkeleton skeleton in model.Skeletons)
        {
            if (skeleton.FirstBone == first)
                return skeleton.Bones;
        }

        return 1;
    }

    private static List<List<float>> Deltas(MuaReader model, int first, MmotMotion motion)
    {
        int bones = BonesOf(model, first);

        if (first < 0 || bones < 1 || first + bones > model.Bones.Count)
            return [];

        List<Matrix> rest = Enumerable.Range(0, bones).Select(i => model.Bones[first + i].Matrix).ToList();
        List<Matrix> settled = [];

        for (int i = 0; i < bones; ++i)
        {
            if (i < motion.Bones && motion.TryUnbind(i, out Matrix inverse))
            {
                settled.Add(inverse);
                continue;
            }

            settled.Add(MatrixMath.Invert(Chain(model, first, i, rest)));
        }

        Matrix place = Matrix.Identity;
        place[0] = (float)ArcGeometry.Scale;
        place[5] = (float)ArcGeometry.Scale;
        place[10] = (float)(ArcGeometry.Mirror ? -ArcGeometry.Scale : ArcGeometry.Scale);
        Matrix unplace = MatrixMath.Invert(place);
        List<List<float>> frames = [];

        for (int frame = 0; frame < motion.Frames; ++frame)
        {
            List<Matrix> local = [];

            for (int i = 0; i < bones; ++i)
            {
                MuaReadBone held = model.Bones[first + i];
                local.Add(i >= motion.Bones ? rest[i] : motion.Pose(i, frame, held.Translation, held.Rotation, held.Scale));
            }

            List<float> step = new(bones * Matrix.Size);

            for (int i = 0; i < bones; ++i)
            {
                Matrix delta = MatrixMath.Multiply(settled[i], Chain(model, first, i, local));
                Matrix landed = MatrixMath.Multiply(MatrixMath.Multiply(unplace, delta), place);

                for (int k = 0; k < Matrix.Size; ++k)
                    step.Add(landed[k]);
            }

            frames.Add(step);
        }

        return frames;
    }

    private static bool Moves(MuaReader model, int first, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            int index = first + i;

            if (index < 0 || index >= model.Bones.Count)
                break;

            MuaReadBone bone = model.Bones[index];

            if (bone.Frames < 2)
                continue;

            for (int k = 0; k < 4; ++k)
            {
                List<MuaKey> keys = model.Keys(bone.Track[k]);

                if (keys.Skip(1).Any(key => !SameBits(key.Value, keys[0].Value)))
                    return true;
            }
        }

        return false;
    }

    private static bool SameBits(Float4 left, Float4 right)
    {
        for (int k = 0; k < 4; ++k)
        {
            if (BitConverter.SingleToUInt32Bits(left[k]) != BitConverter.SingleToUInt32Bits(right[k]))
                return false;
        }

        return true;
    }

    private static MmotMotion Adopt(MuaReader model, int first, int count)
    {
        int frames = 0;

        for (int i = 0; i < count; ++i)
        {
            MuaReadBone bone = model.Bones[first + i];

            if (bone.Frames > 1)
                frames = Math.Max(frames, bone.Frames);
        }

        MmotMotion motion = MmotMotion.Empty(count, frames);

        for (int i = 0; i < count; ++i)
        {
            MuaReadBone bone = model.Bones[first + i];

            if (bone.Frames < 2)
                continue;

            for (int k = 0; k < 4; ++k)
            {
                foreach (MuaKey key in model.Keys(bone.Track[k]))
                    motion.Add(i, (MmotKind)k, key.Value, key.Frame);
            }
        }

        return motion;
    }
}
