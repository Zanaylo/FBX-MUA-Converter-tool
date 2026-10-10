using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Imaging;
using FbxToMua.Core.Preview;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Preview;

public class StagePreviewUnitTest
{
    private static byte[]? RedFloor(string name)
    {
        if (name != "floor.dds")
            return null;

        BgraImage red = ImageOps.Blank(4, 4, 0xffff0000);

        return DdsCodec.EncodeArgb(red);
    }

    private static ExportResult SmallExport()
    {
        return StageExporter.Convert(new ExportSource { Model = FbxExWriter.Build(StageFactory.SmallStage()), Image = RedFloor, Stage = "bg_small" });
    }

    private static ExportResult LayeredExport()
    {
        return StageExporter.Convert(new ExportSource
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_layered",
            Objects = StageFactory.ObjectBytes(),
            Sheet = StageFactory.SmallSheet(),
            SheetName = "bg017",
        });
    }

    [Fact]
    public void Of_SmallStage_ListsTheMeshesInTheOrderTheGameDrawsThem()
    {
        // Arrange
        ExportResult result = SmallExport();

        // Act
        StagePreview preview = StagePreview.Of(result);

        // Assert
        Assert.Equal(["node_001", "node_002"], preview.Meshes.Select(mesh => mesh.Name));
    }

    [Fact]
    public void Of_SmallStage_UnrollsTheFloorStripIntoItsTwoTriangles()
    {
        // Arrange
        ExportResult result = SmallExport();

        // Act
        PreviewMesh floor = StagePreview.Of(result).Meshes[0];

        // Assert
        PreviewPart part = Assert.Single(floor.Parts);
        Assert.Equal("floor.dds", part.Texture);
        Assert.Equal(6, part.Triangles.Length);
        Assert.Equal(4 * 3, floor.Positions.Length);
        Assert.Equal(4 * 2, floor.Uvs.Length);
    }

    [Fact]
    public void Of_AdditiveNode_KeepsItsBlend()
    {
        // Arrange
        ExportResult result = SmallExport();

        // Act
        StagePreview preview = StagePreview.Of(result);

        // Assert
        Assert.Equal(PreviewBlend.Normal, preview.Meshes[0].Blend);
        Assert.Equal(PreviewBlend.Add, preview.Meshes[1].Blend);
    }

    [Fact]
    public void Of_ObjectLayer_MarksTheSpritesAsOverlay()
    {
        // Arrange
        ExportResult result = LayeredExport();

        // Act
        StagePreview preview = StagePreview.Of(result);

        // Assert
        Assert.Equal([false, false, true, true], preview.Meshes.Select(mesh => mesh.Overlay));
    }

    [Fact]
    public void Of_DdsTexture_DecodesItForDrawing()
    {
        // Arrange
        ExportResult result = SmallExport();

        // Act
        BgraImage floor = StagePreview.Of(result).Images["floor.dds"];

        // Assert
        Assert.Equal((4, 4), (floor.Width, floor.Height));
        Assert.Equal([0x00, 0x00, 0xff, 0xff], floor.Pixels[..4]);
    }

    [Fact]
    public void Of_UnreadableTexture_LeavesItOut()
    {
        // Arrange
        ExportResult result = SmallExport();
        result.Images.Add(new ExportFile("broken.dds", [1, 2, 3]));

        // Act
        StagePreview preview = StagePreview.Of(result);

        // Assert
        Assert.False(preview.Images.ContainsKey("broken.dds"));
    }

    [Fact]
    public void Of_TiltedScene_CarriesTheTilt()
    {
        // Arrange
        ExportResult result = StageExporter.Convert(new ExportSource
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_tilted",
            Framing = Framing.Neutral with { Tilt = 4.0f },
        });

        // Act
        StagePreview preview = StagePreview.Of(result);

        // Assert
        Assert.Equal(4, preview.Tilt);
    }
}
