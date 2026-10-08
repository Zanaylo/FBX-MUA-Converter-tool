using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Binary;
using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Dds;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Install;
using FbxToMua.Core.Tests.Fakes;
using FbxToMua.Core.Tests.Formats;

namespace FbxToMua.Core.Tests.Export;

public class StageExporterUnitTest
{
    private static byte[]? FloorOnly(string name)
    {
        if (name != "floor.dds")
            return null;

        byte[] dds = new byte[128];
        LittleEndian.Latin1("DDS ").CopyTo(dds, 0);

        return dds;
    }

    private static ExportSource SmallSource()
    {
        return new ExportSource { Model = FbxExWriter.Build(StageFactory.SmallStage()), Image = FloorOnly, Stage = "bg_small" };
    }

    private static ExportResult SmallExport() => StageExporter.Convert(SmallSource());

    private static ExportSource LayeredSource()
    {
        return new ExportSource
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_layered",
            Objects = StageFactory.ObjectBytes(),
            Sheet = StageFactory.SmallSheet(),
            SheetName = "bg017",
        };
    }

    private static bool HasFlag(MuaReader model, MuaReadMesh mesh, uint flag) => (model.Skeletons[mesh.Skeleton].Flags & flag) == flag;

    [Fact]
    public void Convert_SmallStage_WritesOneMeshPerNodeAndOneTake()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;

        // Assert
        Assert.Equal(2, read.Meshes.Count);
        Assert.Single(result.Motions);
        Assert.Equal(2, result.Scripts.Count);
        Assert.Equal(1, result.Animated);
    }

    [Fact]
    public void Convert_NoMeshes_FailsWithAReason()
    {
        // Arrange
        FbxExModel empty = new() { Nodes = [FbxExNode.Branch()], Animes = [StageFactory.IdentityTrack()] };

        // Act
        Exception? failure = Record.Exception(() => StageExporter.Convert(new ExportSource { Model = FbxExWriter.Build(empty) }));

        // Assert
        Assert.IsType<StageConversionException>(failure);
        Assert.Equal("the stage has no meshes to export", failure.Message);
    }

    [Fact]
    public void Convert_Garbage_FailsWithAReason()
    {
        // Arrange
        ExportSource garbage = new() { Model = new byte[10] };

        // Act
        Exception? failure = Record.Exception(() => StageExporter.Convert(garbage));

        // Assert
        Assert.IsType<StageConversionException>(failure);
        Assert.Equal("bg.fbx.bin could not be read", failure.Message);
    }

    [Fact]
    public void Package_Bbtag_StaysPlainWithThreeSceneEntries()
    {
        // Arrange
        ExportResult export = SmallExport();

        // Act
        ExportArchives archives = StagePackager.Package(export, ArcGame.Bbtag, "small");

        // Assert
        Assert.True(LittleEndian.Starts(archives.Scene, "FPAC"));
        Assert.Equal(["mdl.pac", "scr.pac", "mot.pac"], Fpac.Names(archives.Scene));
    }

    [Fact]
    public void Package_Bbcf_PacksTheFourEntriesTheGameLooksUp()
    {
        // Arrange
        ExportResult export = SmallExport();

        // Act
        ExportArchives bbcf = StagePackager.Package(export, ArcGame.Bbcf, "small");

        // Assert
        Assert.True(LittleEndian.Starts(bbcf.Scene, "DFASFPAC"));
        Assert.True(LittleEndian.Starts(bbcf.Geometry, "DFASFPAC"));
        Assert.True(LittleEndian.Starts(bbcf.Art, "DFASFPAC"));
        Assert.Equal(["mdl.pac", "scr.pac", "mot.pac", "cammot.pac"], Fpac.Names(bbcf.Scene));
        Assert.True(InstallRules.Loadable(ArcGame.Bbcf, bbcf.Scene));
        Assert.Equal("small", InstallRules.ModelName(bbcf.Scene));
        Assert.NotNull(Fpac.Named(Fpac.Walk(bbcf.Scene), "small_cam_000.mmot"));
    }

    [Fact]
    public void Package_StillStage_StillGetsAMotionFolder()
    {
        // Arrange
        ExportResult still = SmallExport();
        still.Motions.Clear();

        // Act
        ExportArchives archives = StagePackager.Package(still, ArcGame.Bbcf, "small");

        // Assert
        Assert.Equal(["mdl.pac", "scr.pac", "mot.pac", "cammot.pac"], Fpac.Names(archives.Scene));
    }

    [Fact]
    public void Convert_FarSky_PullsOnlyTheSkyAndKeepsItOnItsRays()
    {
        // Arrange
        FbxExModel stage = StageFactory.SmallStage();
        stage.Nodes[2].Sibling = 3;
        stage.Nodes.Add(StageFactory.FarSky());
        stage.Animes.Add(StageFactory.IdentityTrack());
        Framing framing = Framing.Neutral;
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Stage = "bg_far" };

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh sky = read.Meshes[2];

        // Assert
        float farthest = 0.0f;

        for (int v = 0; v < sky.Vertices; ++v)
        {
            MuaReadVertex vertex = read.VertexAt(sky.FirstVertex + v);
            farthest = Math.Max(farthest, (float)FarField.Depth(vertex.Position));
            List<float> file = stage.Nodes[3].Vertices;
            Float3 placed = MatrixMath.Transform(new Float3(file[v * 12], file[v * 12 + 1], file[v * 12 + 2]), framing.Placement());
            Assert.True(FarFieldUnitTest.SameScreen(placed, vertex.Position, 1e-5f));
        }

        Assert.Equal(1, result.Pulled);
        Assert.Equal(3, Assert.Single(result.Pulls).Node);
        Assert.True(farthest <= FarField.Reach * 1.0001);
        Assert.False(HasFlag(read, sky, MuaFlags.NoDepthWrite));
        Assert.True(sky.Pivot[2] > read.Meshes[0].Pivot[2] && sky.Pivot[2] > read.Meshes[1].Pivot[2]);
    }

    [Fact]
    public void Convert_UnstatedTextures_StatesTheSizeBbcfCopies()
    {
        // Arrange
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_sized",
            Image = _ => DdsHeaderUnitTest.DdsOf(0x1 | 0x2 | 0x4 | 0x1000, 8, 8, 0, "DXT5", 0),
        };

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.NotEmpty(result.Images);
        Assert.All(result.Images, file => Assert.Equal(64u, DdsHeader.FirstLevelBytes(file.Data)));
    }

    [Fact]
    public void Convert_ObjectLayer_CountsTheSpritesAndShipsTheAtlas()
    {
        // Arrange
        ExportSource source = LayeredSource();

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(3, result.Sprites);
        Assert.Equal(1, result.Front);
        Assert.Contains(result.Images, file => file.Name == "bg017_atlas0.dds");
        Assert.Equal(2, result.Motions.Count);
        Assert.Equal(2, result.Animated);
    }

    [Fact]
    public void Convert_ObjectLayer_DrawsOverTheStageInPrioOrder()
    {
        // Arrange
        ExportSource source = LayeredSource();
        const uint overlay = MuaFlags.NoDepthTest | MuaFlags.NoDepthWrite | MuaFlags.BothFaces;

        // Act
        MuaReader read = MuaReader.Read(StageExporter.Convert(source).Model)!;
        MuaReadMesh walkers = read.Meshes[2];
        MuaReadMesh glow = read.Meshes[3];

        // Assert
        Assert.Equal(4, read.Meshes.Count);
        Assert.True(HasFlag(read, walkers, overlay) && HasFlag(read, glow, overlay));
        Assert.Equal((int)MuaBlend.Add, read.Skeletons[glow.Skeleton].Blend);
        Assert.True(walkers.Pivot[2] < read.Meshes[0].Pivot[2] && walkers.Pivot[2] < read.Meshes[1].Pivot[2]);
        Assert.True(glow.Pivot[2] < walkers.Pivot[2]);
        Assert.Equal((8, 4), (walkers.Vertices, glow.Vertices));
        Assert.Equal(4, read.Skeletons[walkers.Skeleton].Bones);
    }

    [Fact]
    public void Convert_ThirtyBlades_SplitsIntoMeshesOfAtMostTwelve()
    {
        // Arrange
        List<PatWriteSprite> still = [];
        List<PatWriteSprite> swayed = [];

        for (int i = 0; i < 30; ++i)
        {
            still.Add(new PatWriteSprite { Id = i, X = i * 40, Part = 3, Turn = 0.0f });
            swayed.Add(new PatWriteSprite { Id = i, X = i * 40, Part = 3, Turn = 0.05f });
        }

        PatWriteCutout cut = new(3, "grass", 0, 0, 0, 0, 64, 64, 64, 64);
        const string text = "BgObject <- { panidata = \"./bg/x/grass.pat\", data001 = [ { tag=\"frm\", name=\"sway0\", " +
            "wait=20 }, { tag=\"frm\", name=\"sway1\", wait=20 }, { tag=\"prio\", val=266 }, ] }";
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_field",
            Objects = LittleEndian.Latin1(text),
            Sheet = PatWriter.Build([new PatWritePattern("sway0", still), new PatWritePattern("sway1", swayed)], [cut], PatSheetBuilder.WhiteAtlas(4)),
            SheetName = "grass",
        };

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        List<MuaReadMesh> layers = read.Meshes.Where(mesh => mesh.Name.StartsWith("layer_", StringComparison.Ordinal)).ToList();

        // Assert
        Assert.Equal(30, result.Sprites);
        Assert.Equal(3, layers.Count);
        Assert.Equal(30, layers.Sum(mesh => mesh.Vertices / 4));
        Assert.All(layers, mesh =>
        {
            int bones = Enumerable.Range(0, mesh.Vertices).Select(v => read.VertexAt(mesh.FirstVertex + v).Bone).Distinct().Count();
            Assert.True(bones <= ObjectLayer.MostSprites);
            Assert.True(read.Skeletons[mesh.Skeleton].Bones <= ObjectLayer.MostSprites + 2);
        });
    }
}
