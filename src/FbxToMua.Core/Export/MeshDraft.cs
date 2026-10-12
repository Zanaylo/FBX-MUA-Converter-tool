using FbxToMua.Core.Formats.Mua;

namespace FbxToMua.Core.Export;

public sealed record DraftOwner(MuaBlend Blend, uint Flags, SharedRig? Rig);

public sealed class MeshDraft
{
    public string Name { get; init; } = string.Empty;
    public DraftOwner Owner { get; init; } = new(MuaBlend.Unset, 0, null);
    public int Tier { get; init; }
    public int Order { get; init; }
    public List<MuaVertex> Vertices { get; init; } = [];
    public List<(int Material, List<int> Indices)> Parts { get; init; } = [];

    public bool Takes(MeshDraft next)
    {
        return next.Owner == Owner && next.Tier == Tier && Vertices.Count + next.Vertices.Count <= MuaVertex.MostPerMesh;
    }

    public void Absorb(MeshDraft next)
    {
        int offset = Vertices.Count;
        Vertices.AddRange(next.Vertices);

        foreach ((int material, List<int> indices) in next.Parts)
        {
            IEnumerable<int> moved = indices.Select(index => index + offset);

            if (Parts.Count > 0 && Parts[^1].Material == material)
            {
                Parts[^1].Indices.AddRange(moved);
                continue;
            }

            Parts.Add((material, moved.ToList()));
        }
    }

    public MeshDraft Copy()
    {
        return new MeshDraft
        {
            Name = Name,
            Owner = Owner,
            Tier = Tier,
            Order = Order,
            Vertices = [.. Vertices],
            Parts = Parts.Select(part => (part.Material, new List<int>(part.Indices))).ToList(),
        };
    }
}

public static class DraftMerger
{
    public static List<MeshDraft> Merge(IReadOnlyList<MeshDraft> drafts)
    {
        List<MeshDraft> merged = [];

        foreach (MeshDraft draft in drafts.OrderBy(draft => draft.Tier).ThenBy(draft => draft.Order))
        {
            if (merged.Count > 0 && merged[^1].Takes(draft))
            {
                merged[^1].Absorb(draft);
                continue;
            }

            merged.Add(draft.Copy());
        }

        return merged.OrderBy(draft => draft.Order).ToList();
    }
}
