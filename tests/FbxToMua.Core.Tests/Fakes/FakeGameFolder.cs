using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Fakes;

public sealed class FakeGameFolder : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "FbxToMuaTests", Guid.NewGuid().ToString("N"));

    public string Game => Path.Combine(Root, "game");

    public string Workspace => Path.Combine(Root, "workspace");

    public static byte[] ShippedScene(string model)
    {
        byte[] empty = Fpac.Build([]);
        byte[] modelFolder = Fpac.Build([new FpacEntry(model + ".MUA", Enumerable.Repeat((byte)9, 24).ToArray())]);

        return Fpac.Build([new("mdl.pac", modelFolder), new("scr.pac", empty), new("mot.pac", empty), new("cammot.pac", empty)]);
    }

    public static FakeGameFolder Loose(string exe, string stem, string model)
    {
        FakeGameFolder folder = new();
        string stages = Path.Combine(folder.Game, "data", "bg", "main");
        Directory.CreateDirectory(stages);
        File.WriteAllBytes(Path.Combine(folder.Game, exe), []);
        File.WriteAllBytes(Path.Combine(stages, stem + ".pac"), ShippedScene(model));
        File.WriteAllBytes(Path.Combine(stages, stem + "_vtx.pac"), [1, 2, 3]);
        File.WriteAllBytes(Path.Combine(stages, stem + "_img.pac"), [4, 5, 6]);

        return folder;
    }

    public static FakeGameFolder Hashed(string stem, string model)
    {
        FakeGameFolder folder = new();
        string assets = Path.Combine(folder.Game, "asset");
        Directory.CreateDirectory(assets);
        File.WriteAllBytes(Path.Combine(folder.Game, "BBTAG.exe"), []);
        StageTarget target = new("main", stem);

        foreach ((ArchivePart part, byte[] plain) in new[] { (ArchivePart.Scene, ShippedScene(model)), (ArchivePart.Geometry, new byte[] { 1, 2, 3 }), (ArchivePart.Art, new byte[] { 4, 5, 6 }) })
        {
            string relative = target.Relative + ArchiveParts.Suffix(part);
            byte[] raw = (byte[])plain.Clone();
            ArcCrypt.Apply(ArcKey.Bbtag, relative, raw);
            File.WriteAllBytes(Path.Combine(assets, ArcCrypt.NameOf(relative)), raw);
        }

        return folder;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
