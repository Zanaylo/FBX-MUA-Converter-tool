using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Export;

public class ObjectLayerUnitTest
{
    private const string SwayText =
        "BgObject <-\r\n{\r\n\tdata001 =\r\n\t[\r\n" +
        "\t\t{ tag=\"frm\", name=\"sway0\", wait=10 },\r\n" +
        "\t\t{ tag=\"frm\", name=\"sway1\", wait=10 },\r\n" +
        "\t\t{ tag=\"prio\", val=266 },\r\n" +
        "\t\t{ tag=\"startpos\", x=0, y=0 ,z=0 },\r\n" +
        "\t]\r\n}\r\n";

    private static readonly float[,] LeafCorners = { { -64.0f, -90.0f }, { 64.0f, -90.0f }, { 64.0f, 6.0f }, { -64.0f, 6.0f } };

    private static List<ObjectEntry> SmallEntries()
    {
        List<ObjectEntry> entries = ObjectList.Read(StageFactory.ObjectText);
        entries[0].Delay = 0;
        entries[1].Frames.Add(new ObjectFrame("missing", 5));

        return entries;
    }

    private static PatDocument SmallSheet() => PatReader.Read(StageFactory.SmallSheet());

    private static double[] LayerScreen(double[] layer)
    {
        double horizon = -BattleCamera.EyeHeight * BattleCamera.LayerUnits();
        double scale = 1735.0 / (1735.0 + layer[2]);

        return [layer[0] * scale, horizon + (layer[1] - horizon) * scale];
    }

    private static double[] WorldScreen(Float3 world)
    {
        double factor = BattleCamera.LayerUnits() * BattleCamera.EyeDistance / (BattleCamera.EyeDistance + world[2]);

        return [world[0] * factor, -((world[1] - BattleCamera.EyeHeight) * factor + BattleCamera.EyeHeight * BattleCamera.LayerUnits())];
    }

    private static double[] ExpectedLayer(float x, float y, float zoomX, float zoomY, float turns, float pitch, float yaw, int corner, Float3 start)
    {
        double tau = 2.0 * Math.PI;
        double px = LeafCorners[corner, 0] * zoomX;
        double py = LeafCorners[corner, 1] * zoomY;
        double pz = 0.0;

        double angle = turns * tau;
        double rx = px * Math.Cos(angle) - py * Math.Sin(angle);
        py = px * Math.Sin(angle) + py * Math.Cos(angle);
        px = rx;

        angle = -pitch * tau;
        double ry = py * Math.Cos(angle) - pz * Math.Sin(angle);
        pz = py * Math.Sin(angle) + pz * Math.Cos(angle);
        py = ry;

        angle = -yaw * tau;
        rx = px * Math.Cos(angle) + pz * Math.Sin(angle);
        pz = -px * Math.Sin(angle) + pz * Math.Cos(angle);
        px = rx;

        return [px + x + start[0], py + y + start[1], start[2] - pz];
    }

    private static Float3 WorldOfCorner(LayerGroup group, LayerSprite sprite, int corner, int frame)
    {
        Matrix world = MatrixMath.Multiply(PoseMath.Compose(sprite.Poses[frame]), PoseMath.Compose(group.Frame));

        return MatrixMath.Transform(sprite.Corners[corner].Position, world);
    }

    private static bool LandsOnLayer(LayerGroup group, LayerSprite sprite, int corner, int frame, double[] layer)
    {
        double[] expected = LayerScreen(layer);
        double[] found = WorldScreen(WorldOfCorner(group, sprite, corner, frame));

        return Math.Abs(expected[0] - found[0]) < 1e-2 && Math.Abs(expected[1] - found[1]) < 1e-2;
    }

    [Fact]
    public void Convert_SmallSheet_GroupsByEntryAndBlend()
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");

        // Assert
        Assert.Equal(2, layer.Groups.Count);
        Assert.Equal(3, layer.Sprites);
        Assert.Equal(1, layer.Front);
        Assert.Equal(["missing"], layer.Missing);
        Assert.Equal("bg017_atlas0.dds", Assert.Single(layer.Atlases).Name);
    }

    [Fact]
    public void Convert_SmallSheet_DescribesTheWalkersAndTheGlow()
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");
        LayerGroup walkers = layer.Groups[0];
        LayerGroup glow = layer.Groups[1];

        // Assert
        Assert.Equal((1, 271, (int)PatBlend.Normal), (walkers.Entry, walkers.Prio, walkers.Blend));
        Assert.True(walkers.Moves);
        Assert.Equal(40, walkers.Span);
        Assert.Equal(2, walkers.Sprites.Count);
        Assert.Equal((2, (int)PatBlend.Additive, 402), (glow.Entry, glow.Blend, glow.Prio));
        Assert.False(glow.Moves);
    }

    [Theory]
    [InlineData(0, 100.0f)]
    [InlineData(5, 200.0f)]
    [InlineData(10, 300.0f)]
    [InlineData(25, 200.0f)]
    public void Convert_Walker_LandsWhereTheLayerDrawsIt(int frame, float x)
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();
        Float3 start = new(50.0f, -20.5f, 1735.0f);

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");
        LayerGroup walkers = layer.Groups[0];

        // Assert
        for (int corner = 0; corner < 4; ++corner)
        {
            double[] expected = ExpectedLayer(x, -50.0f, 1.0f, 1.0f, 0.25f, 0.125f, -0.0625f, corner, start);
            Assert.True(LandsOnLayer(walkers, walkers.Sprites[0], corner, frame, expected));
        }
    }

    [Fact]
    public void Convert_Walker_ClosesTheLoopAndAveragesItsColour()
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");
        LayerSprite walker = layer.Groups[0].Sprites[0];

        // Assert
        Assert.Equal(41, walker.Poses.Count);
        Assert.True(Tolerance.Near(walker.Poses[0].Translation[0], walker.Poses[40].Translation[0], 1e-6f));
        Assert.True(MathF.Abs(walker.Corners[0].U - 16.0f / 256.0f) < 1e-6f);
        Assert.True(MathF.Abs(walker.Corners[2].V - 80.0f / 256.0f) < 1e-6f);
        Assert.Equal((10, 20, 30, 100), (walker.Colour.R, walker.Colour.G, walker.Colour.B, walker.Colour.A));
    }

    [Fact]
    public void Convert_Blinker_HoldsItsWaitAndHidesWhileTheNextPatternLacksIt()
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");
        LayerSprite blinker = layer.Groups[0].Sprites[1];

        // Assert
        Assert.True(blinker.Poses[9].Scale[0] > 0.5f);
        Assert.True(blinker.Poses[10].Scale[0] < 1e-3f);
        Assert.True(blinker.Poses[39].Scale[0] < 1e-3f);
        Assert.Equal(100, blinker.Colour.A);
    }

    [Fact]
    public void Convert_Glow_LandsWhereTheLayerDrawsIt()
    {
        // Arrange
        List<ObjectEntry> entries = SmallEntries();
        PatDocument sheet = SmallSheet();
        double[] expected = ExpectedLayer(-20.0f, 10.0f, 2.0f, 0.5f, 0.0f, 0.125f, -0.0625f, 0, new Float3(0.0f, -120.0f, 0.0f));

        // Act
        ObjectLayer layer = ObjectLayer.Convert(entries, sheet, "bg017");
        LayerGroup glow = layer.Groups[1];

        // Assert
        Assert.True(LandsOnLayer(glow, glow.Sprites[0], 0, 0, expected));
    }

    [Fact]
    public void Convert_SwayAcrossTheWrap_TakesTheShortestTurn()
    {
        // Arrange
        PatWriteSprite blade = new() { Id = 4, Part = 3, Turn = 0.0f };
        byte[] blob = PatWriter.Build([new PatWritePattern("sway0", [blade]), new PatWritePattern("sway1", [blade])],
            [PatSheetBuilder.LeafCut()], PatSheetBuilder.WhiteAtlas());
        PatSheetBuilder.Tilt(blob, [(0.0f, 0.0f), (0.95f, 0.9697f)]);

        // Act
        ObjectLayer layer = ObjectLayer.Convert(ObjectList.Read(SwayText), PatReader.Read(blob), "bg017");
        LayerGroup group = Assert.Single(layer.Groups);
        LayerSprite sprite = Assert.Single(group.Sprites);

        // Assert
        for (int corner = 0; corner < 4; ++corner)
        {
            Assert.True(LandsOnLayer(group, sprite, corner, 5, ExpectedLayer(0, 0, 1, 1, 0, -0.025f, -0.01515f, corner, new Float3())));
            Assert.True(LandsOnLayer(group, sprite, corner, 15, ExpectedLayer(0, 0, 1, 1, 0, 0.975f, 0.98485f, corner, new Float3())));
        }
    }

    [Fact]
    public void Convert_FlatZoom_KeepsARealRotationAndBindsWithPrecision()
    {
        // Arrange
        PatWriteSprite flat = new() { Id = 1, ZoomX = 0.5f, ZoomY = 0.0f, Part = 3 };
        PatWriteSprite open = flat.Copy();
        open.ZoomY = 0.5f;
        PatWriteCutout cut = new(3, "flower", 0, 0, 0, 0, 64, 64, 64, 64);
        byte[] blob = PatWriter.Build([new PatWritePattern("f0", [flat]), new PatWritePattern("f1", [open])], [cut], PatSheetBuilder.WhiteAtlas(4));
        ObjectEntry entry = new() { Number = 1 };
        entry.Frames.AddRange([new ObjectFrame("f0", 10), new ObjectFrame("f1", 10)]);

        // Act
        ObjectLayer layer = ObjectLayer.Convert([entry], PatReader.Read(blob), "flat");
        LayerSprite sprite = Assert.Single(Assert.Single(layer.Groups).Sprites);

        // Assert
        Assert.All(sprite.Poses, pose => Assert.True(Tolerance.Near(pose.Turn.Dot(pose.Turn), 1.0f, 1e-4f)));
        Assert.True(sprite.Poses[0].Scale[1] < 1e-3f);
        Assert.True(Tolerance.Near(sprite.Poses[10].Scale[1], 0.5f, 1e-5f));
        Assert.True(sprite.Rest.Scale[0] > 0.1f && sprite.Rest.Scale[1] > 0.1f);
    }
}
