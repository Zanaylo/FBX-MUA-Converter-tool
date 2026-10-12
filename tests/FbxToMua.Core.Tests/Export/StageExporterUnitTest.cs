using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Binary;
using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Dds;
using FbxToMua.Core.Formats.Evb;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Formats.Mmot;
using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Imaging;
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
    public void Convert_ForPreview_WritesTheSameModelAndNoMotions()
    {
        // Arrange
        ExportSource source = SmallSource() with { ForPreview = true };

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(SmallExport().Model, result.Model);
        Assert.Empty(result.Motions);
    }

    [Fact]
    public void Convert_TwoSpinnersOnTheSamePeriod_ShareOneSkeletonScriptAndTake()
    {
        // Arrange
        FbxExNode second = StageFactory.Spinner();
        second.Matrix[12] = 0.4f;
        FbxExModel stage = StageFactory.Row((StageFactory.Floor(), StageFactory.IdentityTrack()), (StageFactory.Spinner(), StageFactory.Spin()),
            (second, StageFactory.Spin()));
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Image = FloorOnly, Stage = "bg_twins" };

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh spinners = read.Meshes[1];

        // Assert
        Assert.Equal(2, read.Meshes.Count);
        Assert.Single(result.Motions);
        Assert.Equal(2, result.Scripts.Count);
        Assert.Equal(2, result.Animated);
        Assert.Equal(6, spinners.Vertices);
        Assert.Equal(3, read.Skeletons[spinners.Skeleton].Bones);
        Assert.Equal([1.0f, 2.0f], Enumerable.Range(spinners.FirstVertex, spinners.Vertices).Select(v => (float)read.VertexAt(v).Bone).Distinct());
        Assert.Equal(read.Bones[read.Skeletons[spinners.Skeleton].FirstBone].Name, MmotMotion.Read(result.Motions[0].Data)!.Target);
        Assert.Equal([spinners.Name], MmotBoundMeshes(result.Motions[0].Data));
    }

    private static List<string> MmotBoundMeshes(byte[] motion)
    {
        const int linkTable = 0x20 + 4 * 0x10;
        const int stringTable = 0x20 + 9 * 0x10;
        const int firstMesh = 2;
        int links = (int)LittleEndian.U32(motion, linkTable + 4);
        string[] strings = System.Text.Encoding.Latin1.GetString(motion, (int)LittleEndian.U32(motion, stringTable), motion.Length - (int)LittleEndian.U32(motion, stringTable)).Split('\0');

        return strings.Skip(firstMesh).Take(links).ToList();
    }

    [Fact]
    public void Convert_NeighbouringStillNodes_BecomeOneMesh()
    {
        // Arrange
        FbxExNode beside = StageFactory.Floor();
        beside.Matrix[12] = 3.0f;
        FbxExModel stage = StageFactory.Row((StageFactory.Floor(), StageFactory.IdentityTrack()), (beside, StageFactory.IdentityTrack()),
            (StageFactory.Spinner(), StageFactory.Spin()));
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Image = FloorOnly, Stage = "bg_pair" };

        // Act
        MuaReader read = MuaReader.Read(StageExporter.Convert(source).Model)!;
        MuaReadMesh floors = read.Meshes[0];

        // Assert
        Assert.Equal(2, read.Meshes.Count);
        Assert.Equal(8, floors.Vertices);
        Assert.Equal(1, floors.Parts);
        Assert.Equal(floors.Name, read.Bones[read.Skeletons[floors.Skeleton].FirstBone].Name);
    }

    [Fact]
    public void Convert_FloorUnderTheFighters_CatchesTheirShadow()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        MuaReader read = MuaReader.Read(StageExporter.Convert(source).Model)!;

        // Assert
        Assert.False(HasFlag(read, read.Meshes[0], MuaFlags.NoShadow));
        Assert.True(HasFlag(read, read.Meshes[1], MuaFlags.NoShadow));
    }

    [Fact]
    public void Convert_ShippedShadow_KeepsTheFloorWhole()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(2, MuaReader.Read(result.Model)!.Meshes.Count);
        Assert.Equal(1, result.ShadowFloors);
        Assert.Equal(0, result.ShadowCuts);
    }

    [Fact]
    public void Convert_ShadowLeaningToNegativeX_CutsTheFloorToTheStripTheGameShades()
    {
        // Arrange
        Reframe leaning = Reframe.None with { ShadowTurn = 19.0f, ShadowLength = 1.45f };
        ExportSource source = SmallSource() with { Reframe = leaning };
        int[] light = SceneLight.Vector(leaning);

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;

        // Assert
        MuaReadMesh lit = Assert.Single(read.Meshes, mesh => !HasFlag(read, mesh, MuaFlags.NoShadow));
        List<Float3> points = Enumerable.Range(0, lit.Vertices).Select(v => read.VertexAt(lit.FirstVertex + v).Position).ToList();
        Assert.True(ShadowArea.Takes(light, points.Min(point => point[1]), points.Min(point => point[2]), points.Max(point => point[2])));
        Assert.Equal(3, read.Meshes.Count);
        Assert.Equal(1, result.ShadowFloors);
        Assert.Equal(1, result.ShadowCuts);
    }

    [Fact]
    public void Convert_SmallStage_HandsTheStillFloorToThePreview()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        ShadowCandidate floor = Assert.Single(result.ShadowCandidates);
        Assert.Equal(4, floor.Chunks.Sum(chunk => chunk.Positions.Length));
    }

    [Fact]
    public void Convert_StandingWall_StaysOutOfTheShadow()
    {
        // Arrange
        List<float> upright = StageFactory.IdentityTrack();
        (upright[5], upright[6], upright[9], upright[10]) = (0.0f, 1.0f, -1.0f, 0.0f);
        FbxExNode wall = StageFactory.Floor();
        (wall.Matrix[5], wall.Matrix[6], wall.Matrix[9], wall.Matrix[10]) = (0.0f, 1.0f, -1.0f, 0.0f);
        FbxExModel stage = StageFactory.Row((StageFactory.Floor(), StageFactory.IdentityTrack()), (wall, upright));
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Image = FloorOnly, Stage = "bg_wall" };

        // Act
        MuaReader read = MuaReader.Read(StageExporter.Convert(source).Model)!;

        // Assert
        Assert.Equal(2, read.Meshes.Count);
        Assert.False(HasFlag(read, read.Meshes[0], MuaFlags.NoShadow));
        Assert.True(HasFlag(read, read.Meshes[1], MuaFlags.NoShadow));
    }

    [Fact]
    public void Convert_SpinningInPlace_KeysOnlyTheTurn()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);
        int[] spinner = MmotTrackLengths(result.Motions[0].Data, 1);

        // Assert
        Assert.Equal(2, spinner[(int)MmotKind.Translation]);
        Assert.Equal(2, spinner[(int)MmotKind.Scale]);
        Assert.True(spinner[(int)MmotKind.Turn] > 2);
        Assert.Equal(spinner[(int)MmotKind.Turn], spinner[(int)MmotKind.Rotation]);
    }

    private static int[] MmotTrackLengths(byte[] motion, int bone)
    {
        const int keyListTable = 0x20 + 2 * 0x10;
        const int listStride = 0x10;
        int lists = (int)LittleEndian.U32(motion, keyListTable);

        return Enumerable.Range(0, MmotBone.Tracks)
            .Select(track => (int)LittleEndian.U32(motion, lists + (bone * MmotBone.Tracks + track) * listStride + 4))
            .ToArray();
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
    public void Package_Bbtag_StaysPlainWithTheFourEntriesTheGameLooksUp()
    {
        // Arrange
        ExportResult export = SmallExport();

        // Act
        ExportArchives archives = StagePackager.Package(export, ArcGame.Bbtag, "small");

        // Assert
        Assert.True(LittleEndian.Starts(archives.Scene, "FPAC"));
        Assert.Equal(["mdl.pac", "scr.pac", "mot.pac", "cammot.pac"], Fpac.Names(archives.Scene));
        Assert.True(InstallRules.Loadable(archives.Scene));
        Assert.NotNull(Fpac.Named(Fpac.Walk(archives.Scene), "small_cam_000.mmot"));
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
        Assert.True(InstallRules.Loadable(bbcf.Scene));
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
    public void Convert_TextureNamedAfterItsSourceArt_PacksTheGamesDds()
    {
        // Arrange
        FbxExModel stage = StageFactory.SmallStage();
        stage.Textures[0] = "floor.png";
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Image = FloorOnly, Stage = "bg_png" };

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;

        // Assert
        Assert.Equal(["floor.dds"], read.Textures);
        Assert.Equal(["floor.dds"], result.Images.Select(image => image.Name));
        Assert.Empty(result.Missing);
    }

    [Fact]
    public void Convert_TextureNowhereToBeFound_StillNamesADds()
    {
        // Arrange
        FbxExModel stage = StageFactory.SmallStage();
        stage.Textures[0] = "gone.psd";
        ExportSource source = new() { Model = FbxExWriter.Build(stage), Image = FloorOnly, Stage = "bg_gone" };

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;

        // Assert
        Assert.Equal(["gone.dds"], read.Textures);
        Assert.Equal(["gone.dds"], result.Missing);
    }

    [Fact]
    public void Convert_FoggedStage_TintsItsTexturesInPlace()
    {
        // Arrange
        byte[] red = DdsCodec.EncodeDxt(ImageOps.Blank(4, 4, 0xffff0000));
        Framing fogged = Framing.Neutral with { Look = new StageLook(new FogLook(true, 0.0f, 2.0f, 0.0f, 0.0f, 1.0f), BloomLook.Off) };
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Image = name => name == "floor.dds" ? red : null,
            Framing = fogged,
            Stage = "bg_fog",
        };

        // Act
        ExportResult result = StageExporter.Convert(source);
        BgraImage baked = DdsCodec.Decode(Assert.Single(result.Images).Data)!;

        // Assert
        Assert.Equal("floor.dds", result.Images[0].Name);
        Assert.True(baked.Pixels[0] > 0x40 && baked.Pixels[2] < 0xc0);
    }

    [Fact]
    public void Convert_FoggedStage_ReportsItsOwnLook()
    {
        // Arrange
        StageLook own = new(new FogLook(true, 0.0f, 2.0f, 0.0f, 0.0f, 1.0f), BloomLook.Off);
        ExportSource source = SmallSource() with { Framing = Framing.Neutral with { Look = own } };

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(own, result.OwnLook);
    }

    [Fact]
    public void Convert_KeptLook_WinsOverTheStagesOwn()
    {
        // Arrange
        byte[] red = DdsCodec.EncodeDxt(ImageOps.Blank(4, 4, 0xffff0000));
        StageLook kept = new(new FogLook(true, 0.0f, 2.0f, 0.0f, 0.0f, 1.0f), BloomLook.Off);
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Image = name => name == "floor.dds" ? red : null,
            Reframe = Reframe.None with { Look = kept },
            Stage = "bg_kept",
        };

        // Act
        BgraImage baked = DdsCodec.Decode(Assert.Single(StageExporter.Convert(source).Images).Data)!;

        // Assert
        Assert.True(baked.Pixels[0] > 0x40);
    }

    [Fact]
    public void Convert_ForPreview_LeavesTheTexturesRaw()
    {
        // Arrange
        byte[] red = DdsCodec.EncodeDxt(ImageOps.Blank(4, 4, 0xffff0000));
        StageLook own = new(new FogLook(true, 0.0f, 2.0f, 0.0f, 0.0f, 1.0f), BloomLook.Off);
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Image = name => name == "floor.dds" ? red : null,
            Framing = Framing.Neutral with { Look = own },
            Stage = "bg_raw",
            ForPreview = true,
        };

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(red, Assert.Single(result.Images).Data);
    }

    [Fact]
    public void Convert_SmallStage_ReportsEachTexturesDepth()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(10.0f, result.Depths["floor.dds"], 1e-4f);
    }

    [Fact]
    public void Convert_FogByDistance_UsesTheTexturesDepth()
    {
        // Arrange
        byte[] red = DdsCodec.EncodeDxt(ImageOps.Blank(4, 4, 0xffff0000));
        StageLook own = new(new FogLook(true, 0.0f, 20.0f, 0.0f, 0.0f, 1.0f, true), BloomLook.Off);
        ExportSource source = new()
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Image = name => name == "floor.dds" ? red : null,
            Framing = Framing.Neutral with { Look = own },
            Stage = "bg_far",
        };

        // Act
        BgraImage baked = DdsCodec.Decode(Assert.Single(StageExporter.Convert(source).Images).Data)!;

        // Assert
        Assert.InRange(baked.Pixels[2], (byte)0x70, (byte)0x90);
    }

    [Fact]
    public void Convert_FoggedLayer_LeavesTheSpriteSheetAlone()
    {
        // Arrange
        Framing fogged = Framing.Neutral with { Look = new StageLook(new FogLook(true, 0.0f, 2.0f, 0.0f, 0.0f, 1.0f), BloomLook.Off) };
        ExportSource clear = LayeredSource();

        // Act
        ExportResult plain = StageExporter.Convert(clear);
        ExportResult foggy = StageExporter.Convert(clear with { Framing = fogged });

        // Assert
        Assert.Equal(plain.Images.Select(image => image.Data), foggy.Images.Select(image => image.Data));
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
    public void Convert_Reframed_MovesEveryStageVertexByTheReframe()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Height = 25.0f, Turn = 15.0f, Scale = 1.5f };
        MuaReader neutral = MuaReader.Read(SmallExport().Model)!;

        // Act
        MuaReader moved = MuaReader.Read(StageExporter.Convert(SmallSource() with { Reframe = reframe }).Model)!;

        // Assert
        for (int v = 0; v < neutral.Meshes[0].Vertices; ++v)
        {
            Float3 expected = MatrixMath.Transform(neutral.VertexAt(v).Position, reframe.StageMatrix());
            Float3 actual = moved.VertexAt(v).Position;

            for (int k = 0; k < 3; ++k)
                Assert.True(Tolerance.Near(expected[k], actual[k], 1e-5f));
        }
    }

    [Fact]
    public void Convert_Reframed_MovesTheLayerWithTheStage()
    {
        // Arrange
        Reframe reframe = Reframe.None with { Side = 60.0f, Distance = 40.0f };
        MuaReader neutral = MuaReader.Read(StageExporter.Convert(LayeredSource()).Model)!;
        MuaReadMesh walkers = neutral.Meshes[2];

        // Act
        MuaReader moved = MuaReader.Read(StageExporter.Convert(LayeredSource() with { Reframe = reframe }).Model)!;

        // Assert
        for (int v = walkers.FirstVertex; v < walkers.FirstVertex + walkers.Vertices; ++v)
        {
            Float3 expected = MatrixMath.Transform(neutral.VertexAt(v).Position, reframe.StageMatrix());
            Float3 actual = moved.VertexAt(v).Position;

            for (int k = 0; k < 3; ++k)
                Assert.True(Tolerance.Near(expected[k], actual[k], 1e-4f));
        }
    }

    [Fact]
    public void Convert_ReframedTilt_AddsItToTheSceneScript()
    {
        // Arrange
        ExportSource source = SmallSource() with { Framing = Framing.Neutral with { Tilt = 3.0f }, Reframe = Reframe.None with { Tilt = 2.0f } };

        // Act
        ExportResult result = StageExporter.Convert(source);
        ExportFile scene = result.Scripts.First(file => file.Name == "base.evb");

        // Assert
        Assert.Equal(5, result.Tilt);
        Assert.Equal(5.0f, EvbPlayer.Tilt(scene.Data));
    }

    [Fact]
    public void Convert_NoTilt_ReportsAFlatScene()
    {
        // Arrange
        ExportSource source = SmallSource();

        // Act
        ExportResult result = StageExporter.Convert(source);

        // Assert
        Assert.Equal(0, result.Tilt);
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

    private static ExportSource FloorTextured(uint bgra, bool clearOfGameBloom)
    {
        byte[] floor = DdsCodec.EncodeDxt(ImageOps.Blank(4, 4, bgra));
        StageLook look = StageLook.Plain with { ClearOfGameBloom = clearOfGameBloom };

        return new ExportSource
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Image = name => name == "floor.dds" ? floor : null,
            Framing = Framing.Neutral with { Look = look },
            Stage = "bg_bloom",
        };
    }

    private static IEnumerable<byte> AlphasOf(MuaReader read, MuaReadMesh mesh)
    {
        return Enumerable.Range(0, mesh.Vertices).Select(v => read.VertexAt(mesh.FirstVertex + v).Colour.A);
    }

    [Fact]
    public void Convert_ClearOfGameBloom_DrawsTheSolidFloorOpaqueWithAlmostNoAlpha()
    {
        // Arrange
        ExportSource source = FloorTextured(0xffff0000, true);

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh floor = read.Meshes[0];

        // Assert
        Assert.Equal((int)MuaBlend.Opaque, read.Skeletons[floor.Skeleton].Blend);
        Assert.All(AlphasOf(read, floor), alpha => Assert.Equal(GameBloomShield.HiddenAlpha, alpha));
        Assert.False(HasFlag(read, floor, MuaFlags.NoShadow));
        Assert.Equal(1, result.ClearOfGameBloom);
    }

    [Fact]
    public void Convert_ClearOfGameBloom_LeavesTheAdditiveSpinnerAlone()
    {
        // Arrange
        ExportSource source = FloorTextured(0xffff0000, true);

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh spinner = read.Meshes[1];

        // Assert
        Assert.Equal((int)MuaBlend.Add, read.Skeletons[spinner.Skeleton].Blend);
        Assert.All(AlphasOf(read, spinner), alpha => Assert.Equal(128, alpha));
    }

    [Fact]
    public void Convert_ClearOfGameBloomOnSeeThroughTexture_KeepsTheFloorBlended()
    {
        // Arrange
        ExportSource source = FloorTextured(0x80ff0000, true);

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh floor = read.Meshes[0];

        // Assert
        Assert.Equal((int)MuaBlend.Unset, read.Skeletons[floor.Skeleton].Blend);
        Assert.All(AlphasOf(read, floor), alpha => Assert.Equal(255, alpha));
        Assert.Equal(0, result.ClearOfGameBloom);
    }

    [Fact]
    public void Convert_GameBloomLeftAlone_KeepsTheFloorBlended()
    {
        // Arrange
        ExportSource source = FloorTextured(0xffff0000, false);

        // Act
        ExportResult result = StageExporter.Convert(source);
        MuaReader read = MuaReader.Read(result.Model)!;
        MuaReadMesh floor = read.Meshes[0];

        // Assert
        Assert.Equal((int)MuaBlend.Unset, read.Skeletons[floor.Skeleton].Blend);
        Assert.All(AlphasOf(read, floor), alpha => Assert.Equal(255, alpha));
        Assert.Equal(0, result.ClearOfGameBloom);
    }
}
