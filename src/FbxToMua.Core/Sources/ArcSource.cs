using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Import;
using FbxToMua.Core.Sources.Tables;

namespace FbxToMua.Core.Sources;

public sealed class ArcSource
{
    private const string GeometrySuffix = "_vtx.pac";
    private const string SceneSuffix = ".pac";
    private const string ArtSuffix = "_img.pac";
    private const string Astral = "bg_exastral";
    private const string Particles = "data/particle/particle_dat_bg.pac";
    private const string ParticleArt = "data/particle/particle_img_bg.pac";
    private const int PacHeader = 16;
    private const int PacCount = 12;
    private const int MostHashed = 200000;
    private static readonly string[] HashedRoots = ["asset", "data"];

    private sealed record Held(string Relative, string Geometry, long Bytes, bool Backdrop);

    private readonly string _root;
    private readonly ArcKey _key;
    private readonly SortedDictionary<string, Held> _stages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _hashed = new(StringComparer.Ordinal);
    private Dictionary<string, string>? _names;

    public GameKind Game { get; }

    private ArcSource(string root, GameKind game)
    {
        _root = root;
        Game = game;
        _key = game == GameKind.P4u2 ? ArcKey.P4u2 : ArcKey.Bbtag;
    }

    public static ArcSource? Open(string folder)
    {
        GameKind game = GameFolder.Detect(folder);

        if (!GameFolder.IsArcSys(game))
            return null;

        ArcSource source = new(folder, game);
        source.Sweep();

        if (source._stages.Count == 0)
            source.Listed();

        return source._stages.Count > 0 ? source : null;
    }

    public IReadOnlyList<SourceStage> Stages() => _stages.Select(held => new SourceStage(held.Key, NameOf(held.Key), held.Value.Bytes, held.Value.Backdrop)).ToList();

    public string NameOf(string stage)
    {
        string folder = stage.ToLowerInvariant();
        _names ??= ArcIdList.StageNames(ArcIdList.Text(Whole(ArcIdList.English)), ArcIdList.Text(Whole(ArcIdList.Japanese)));

        return _names.GetValueOrDefault(folder) ?? EnglishStageNames.Of(Game, folder) ?? Readable(folder);
    }

    public ArcStageResult? Convert(string stage)
    {
        if (!_stages.TryGetValue(stage.ToLowerInvariant(), out Held? held))
            return null;

        byte[]? geometry = Whole(held.Geometry);

        if (geometry is null)
            return null;

        return ArcStageImporter.Convert(new ArcStageInput
        {
            Geometry = geometry,
            Scene = Whole(held.Relative + SceneSuffix) ?? [],
            Art = Whole(held.Relative + ArtSuffix) ?? [],
            Particles = Whole(Particles) ?? [],
            ParticleArt = Whole(ParticleArt) ?? [],
            Stage = stage,
            Game = Game,
        });
    }

    public static string Readable(string stage)
    {
        if (stage.Length > Astral.Length && stage.StartsWith(Astral, StringComparison.Ordinal))
            return "Astral " + stage[Astral.Length..].ToUpperInvariant();

        string body = stage.StartsWith("bg_", StringComparison.Ordinal) ? stage[3..] : stage;

        return string.Join(' ', body.Split('_').Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }

    private static string Plain(string relative) => relative.Replace('/', Path.DirectorySeparatorChar);

    private void Sweep()
    {
        string bg = Path.Combine(_root, "data", "bg");

        if (!Directory.Exists(bg))
            return;

        foreach (string group in Directory.EnumerateDirectories(bg))
            SweepGroup(group, $"data/bg/{Path.GetFileName(group).ToLowerInvariant()}/");
    }

    private void SweepGroup(string folder, string relative)
    {
        SortedSet<string> archives = new(Directory.EnumerateFiles(folder, "*.pac").Select(path => Path.GetFileName(path).ToLowerInvariant()), StringComparer.Ordinal);

        foreach (string leaf in archives.Where(leaf => leaf.EndsWith(GeometrySuffix, StringComparison.Ordinal)))
        {
            string stage = leaf[..^GeometrySuffix.Length];

            if (Empty(relative + stage + GeometrySuffix))
                continue;

            long bytes = Size(Path.Combine(folder, leaf)) + Size(Path.Combine(folder, stage + SceneSuffix)) + Size(Path.Combine(folder, stage + ArtSuffix));
            _stages[stage] = new Held(relative + stage, relative + stage + GeometrySuffix, bytes, false);
        }

        foreach (string leaf in archives)
        {
            if (!leaf.EndsWith(SceneSuffix, StringComparison.Ordinal) || leaf.EndsWith(GeometrySuffix, StringComparison.Ordinal) || leaf.EndsWith(ArtSuffix, StringComparison.Ordinal))
                continue;

            string stage = leaf[..^SceneSuffix.Length];

            if (archives.Contains(stage + GeometrySuffix) || _stages.ContainsKey(stage))
                continue;

            byte[] archive = File.ReadAllBytes(Path.Combine(folder, leaf));

            if (!ArcStageImporter.HoldsWholeModel(archive))
                continue;

            _stages[stage] = new Held(relative + stage, relative + stage + SceneSuffix, archive.Length + Size(Path.Combine(folder, stage + ArtSuffix)), true);
        }
    }

    private void Listed()
    {
        Hashed();

        if (_hashed.Count == 0)
            return;

        foreach (string stem in BbtagStagePaths.Stages)
        {
            string relative = "data/bg/" + stem;
            string? geometry = Encrypted(relative + GeometrySuffix);

            if (geometry is null || Empty(relative + GeometrySuffix))
                continue;

            long bytes = Size(geometry) + Size(Encrypted(relative + SceneSuffix)) + Size(Encrypted(relative + ArtSuffix));
            _stages[StageOf(stem)] = new Held(relative, relative + GeometrySuffix, bytes, false);
        }

        foreach (string stem in BbtagStagePaths.WholeStages)
        {
            if (_stages.ContainsKey(StageOf(stem)))
                continue;

            string relative = "data/bg/" + stem;
            byte[]? archive = Whole(relative + SceneSuffix);

            if (archive is null || !ArcStageImporter.HoldsWholeModel(archive))
                continue;

            _stages[StageOf(stem)] = new Held(relative, relative + SceneSuffix, archive.Length + Size(Encrypted(relative + ArtSuffix)), true);
        }
    }

    private static string StageOf(string stem) => stem[(stem.LastIndexOf('/') + 1)..];

    private void Hashed()
    {
        Stack<string> pending = new(HashedRoots.Select(folder => Path.Combine(_root, folder)).Where(Directory.Exists).Reverse());

        while (pending.Count > 0 && _hashed.Count < MostHashed)
        {
            string folder = pending.Pop();

            foreach (string child in Directory.EnumerateDirectories(folder))
                pending.Push(child);

            foreach (string file in Directory.EnumerateFiles(folder))
            {
                string name = Path.GetFileName(file);

                if (name.Length == 32)
                    _hashed[name.ToLowerInvariant()] = file;
            }
        }
    }

    private string? Encrypted(string relative) => _hashed.GetValueOrDefault(ArcCrypt.NameOf(relative));

    private static long Size(string? path) => path is not null && File.Exists(path) ? new FileInfo(path).Length : 0;

    private byte[]? Whole(string relative)
    {
        string plain = Path.Combine(_root, Plain(relative));

        if (File.Exists(plain))
            return File.ReadAllBytes(plain);

        string? hashed = Encrypted(relative);

        if (hashed is null)
            return null;

        byte[] data = File.ReadAllBytes(hashed);
        ArcCrypt.Apply(_key, relative, data);

        return data;
    }

    private bool Empty(string relative)
    {
        byte[]? head = Head(relative);

        return head is not null && LittleEndian.Starts(head, "FPAC") && LittleEndian.U32(head, PacCount) == 0;
    }

    private byte[]? Head(string relative)
    {
        string plain = Path.Combine(_root, Plain(relative));
        string? path = File.Exists(plain) ? plain : Encrypted(relative);

        if (path is null)
            return null;

        byte[] head = new byte[PacHeader];

        using (FileStream stream = File.OpenRead(path))
        {
            if (stream.ReadAtLeast(head, PacHeader, throwOnEndOfStream: false) != PacHeader)
                return null;
        }

        if (path != plain)
            ArcCrypt.Apply(_key, relative, head);

        return head;
    }
}
