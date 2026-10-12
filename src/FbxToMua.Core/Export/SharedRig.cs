using FbxToMua.Core.Formats.Mua;

namespace FbxToMua.Core.Export;

public readonly record struct RigKey(int Span, MuaBlend Blend, uint Flags);

public sealed class SharedRig
{
    public const int Root = 0;
    private const int Unplaced = -1;

    private readonly Dictionary<(int Parent, LimbOrigin Origin), int> _bones = [];

    public SharedRig(string name, RigKey key)
    {
        Name = name;
        Key = key;
    }

    public string Name { get; }
    public RigKey Key { get; }
    public List<Limb> Limbs { get; } = [];
    public int Bones => Limbs.Count + 1;

    public int Missing(IReadOnlyList<Limb> chain)
    {
        int[] bones = Resolve(chain);

        return bones.Skip(1).Count(bone => bone == Unplaced);
    }

    public int Attach(IReadOnlyList<Limb> chain)
    {
        int[] bones = new int[chain.Count + 1];
        bones[0] = Root;

        for (int i = 0; i < chain.Count; ++i)
        {
            int parent = bones[chain[i].Parent];
            (int, LimbOrigin) key = (parent, chain[i].Origin);

            if (!_bones.TryGetValue(key, out int bone))
            {
                Limbs.Add(chain[i] with { Parent = parent });
                bone = Limbs.Count;
                _bones[key] = bone;
            }

            bones[i + 1] = bone;
        }

        return bones[^1];
    }

    private int[] Resolve(IReadOnlyList<Limb> chain)
    {
        int[] bones = new int[chain.Count + 1];
        bones[0] = Root;

        for (int i = 0; i < chain.Count; ++i)
        {
            int parent = bones[chain[i].Parent];
            bones[i + 1] = parent == Unplaced ? Unplaced : _bones.GetValueOrDefault((parent, chain[i].Origin), Unplaced);
        }

        return bones;
    }
}

public readonly record struct RigPlace(SharedRig Rig, int Bone);

public sealed class RigBook
{
    public const int MostBones = 32;
    private const string RigPrefix = "rig_";

    private readonly List<SharedRig> _rigs = [];

    public IReadOnlyList<SharedRig> Rigs => _rigs;

    public RigPlace Place(RigKey key, IReadOnlyList<Limb> chain)
    {
        SharedRig rig = _rigs.FirstOrDefault(open => open.Key == key && open.Bones + open.Missing(chain) <= MostBones) ?? Open(key);

        return new RigPlace(rig, rig.Attach(chain));
    }

    private SharedRig Open(RigKey key)
    {
        SharedRig rig = new($"{RigPrefix}{_rigs.Count:D3}", key);
        _rigs.Add(rig);

        return rig;
    }
}
