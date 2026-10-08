using FbxToMua.Core.Formats.Pat;
using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.FbxEx;

namespace FbxToMua.Core.Tests.Fakes;

public static class StageFactory
{
    public const string ObjectText =
        "BgObject <-\r\n" +
        "{\r\n" +
        "\tpanidata = \"./bg/bg045/bg017.pat\", // \u0082â\u0082â\r\n" +
        "\tdata002 =\r\n" +
        "\t[\r\n" +
        "\t\t{ tag=\"frm\", name=\"glow\", wait=1500 }, // \u0095{\r\n" +
        "\t\t{ tag=\"prio\", val=402 },\r\n" +
        "\t\t{ tag=\"flag\", val=1 },\r\n" +
        "\t\t{ tag=\"startpos\", x=0, y=-120 ,z=0 },\r\n" +
        "\t]\r\n" +
        "/*\r\n" +
        "\tdata003 =\r\n" +
        "\t[\r\n" +
        "\t\t{ tag=\"frm\", name=\"ita\", wait=400 },\r\n" +
        "\t]\r\n" +
        "*/\r\n" +
        "\tdata001 =\r\n" +
        "\t[\r\n" +
        "\t\t{ tag=\"frm\", name=\"walk0\", wait=10 },\r\n" +
        "\t\t{ tag=\"frm\", name=\"walk1\", wait=30 },\r\n" +
        "\t\t{ tag=\"prio\", val=271 },\r\n" +
        "\t\t{ tag=\"prio_ex\", val = 267 },\r\n" +
        "\t\t{ tag=\"startpos\", x=50, y=-20.5 ,z=1735 },\r\n" +
        "\t\t{ tag=\"startdelay\", val=4 },\r\n" +
        "\t]\r\n" +
        "}\r\n";

    public static byte[] ObjectBytes() => LittleEndian.Latin1(ObjectText);

    public static List<float> IdentityTrack() => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    public static FbxExModel SmallStage()
    {
        FbxExModel model = new() { Textures = ["floor.dds"] };
        model.Materials.Add(new FbxExMaterial { FileName = "floor", TextureIndex = 0 });
        model.Materials.Add(new FbxExMaterial { FileName = "floor", TextureIndex = 0 });

        FbxExNode root = FbxExNode.Branch();
        root.Child = 1;

        FbxExNode floor = FbxExNode.Leaf();
        floor.Sibling = 2;
        floor.Vertices =
        [
            -1, 0, -1, 0, 1, 0, 1, 1, 1, 1, 0, 0,
            1, 0, -1, 0, 1, 0, 1, 1, 1, 1, 1, 0,
            1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 1, 1,
            -1, 0, 1, 0, 1, 0, 1, 1, 1, 1, 0, 1,
        ];
        floor.Submeshes = [new FbxExSubmesh { Material = 0, Indices = [0, 2, 1, 0, 3, 2] }];

        FbxExNode spinner = FbxExNode.Leaf();
        spinner.BlendMode = 1;
        spinner.Alpha = 1;
        spinner.Matrix[13] = 0.5f;
        spinner.Vertices =
        [
            0, 0, 0, 0, 0, 1, 1, 0, 0, 0.5f, 0, 0,
            0.1f, 0, 0, 0, 0, 1, 1, 0, 0, 0.5f, 1, 0,
            0, 0.1f, 0, 0, 0, 1, 1, 0, 0, 0.5f, 0, 1,
        ];
        spinner.Submeshes = [new FbxExSubmesh { Material = 1, Indices = [0, 2, 1] }];

        model.Nodes = [root, floor, spinner];

        List<float> spin = [];

        for (int frame = 0; frame < 8; ++frame)
        {
            float angle = frame * 0.25f;
            List<float> step = IdentityTrack();
            step[0] = MathF.Cos(angle);
            step[2] = -MathF.Sin(angle);
            step[8] = MathF.Sin(angle);
            step[10] = MathF.Cos(angle);
            step[13] = 0.5f;
            spin.AddRange(step);
        }

        model.Animes = [IdentityTrack(), IdentityTrack(), spin];

        return model;
    }

    public static FbxExNode FarSky()
    {
        FbxExNode sky = FbxExNode.Leaf();
        sky.Vertices =
        [
            -50, 0, -113, 0, 0, 1, 1, 1, 1, 1, 0, 0,
            50, 0, -113, 0, 0, 1, 1, 1, 1, 1, 1, 0,
            50, 30, -113, 0, 0, 1, 1, 1, 1, 1, 1, 1,
            -50, 30, -113, 0, 0, 1, 1, 1, 1, 1, 0, 1,
        ];
        sky.Submeshes = [new FbxExSubmesh { Material = 0, Indices = [0, 2, 1, 0, 3, 2] }];

        return sky;
    }

    public static byte[] SmallSheet()
    {
        PatWriteSprite walker = new() { Id = 7, X = 100, Y = -50, Priority = 10, Part = 3, Tint = [10, 20, 30, 200], Turn = 0.25f };

        PatWriteSprite blinker = walker.Copy();
        blinker.Id = 9;
        blinker.X = 0;
        blinker.Y = 0;
        blinker.Turn = 0.0f;
        blinker.Tint = [10, 20, 30, 100];

        PatWriteSprite walked = walker.Copy();
        walked.X = 300;
        walked.Tint = [10, 20, 30, 0];

        PatWriteSprite glow = walker.Copy();
        glow.Id = 1;
        glow.Additive = 1;
        glow.X = -20;
        glow.Y = 10;
        glow.ZoomX = 2.0f;
        glow.ZoomY = 0.5f;
        glow.Turn = 0.0f;

        byte[] blob = PatWriter.Build(
        [
            new PatWritePattern("walk0", [walker, blinker]),
            new PatWritePattern(" walk1 ", [walked]),
            new PatWritePattern("glow", [glow]),
        ], [PatSheetBuilder.LeafCut()], PatSheetBuilder.WhiteAtlas());

        PatSheetBuilder.Tilt(blob, Enumerable.Repeat((0.125f, -0.0625f), 4).ToList());

        return blob;
    }
}
