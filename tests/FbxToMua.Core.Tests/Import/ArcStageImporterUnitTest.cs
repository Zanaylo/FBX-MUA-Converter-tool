using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Import;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Import;

public class ArcStageImporterUnitTest
{
    private const float PortUnit = 0.132f / 213.0f;

    private static ArcStageInput BackFrom(ExportResult result, string stage)
    {
        ExportArchives archives = StagePackager.Package(result, ArcGame.Bbtag, stage);

        return new ArcStageInput { Geometry = archives.Geometry, Scene = archives.Scene, Art = archives.Art, Stage = stage };
    }

    private static ExportResult SmallExport()
    {
        return StageExporter.Convert(new ExportSource { Model = FbxExWriter.Build(StageFactory.SmallStage()), Stage = "bg_small" });
    }

    [Fact]
    public void Convert_SmallExport_ComesBackAsAReadableStage()
    {
        // Arrange
        ArcStageInput input = BackFrom(SmallExport(), "bg_small");

        // Act
        ArcStageResult? result = ArcStageImporter.Convert(input);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(FbxExReader.Read(result.Model));
    }

    [Fact]
    public void Convert_SmallExport_PutsTheFloorBackWhereUni2HadIt()
    {
        // Arrange
        ArcStageInput input = BackFrom(SmallExport(), "bg_small");
        Matrix place = Framing.Neutral.Placement();
        Float3 corner = MatrixMath.Transform(new Float3(-1.0f, 0.0f, -1.0f), place);
        Float3 expected = new(corner[0] * PortUnit, corner[1] * PortUnit, -corner[2] * PortUnit);

        // Act
        FbxExModel returned = FbxExReader.Read(ArcStageImporter.Convert(input)!.Model)!;
        bool found = returned.Nodes.Where(node => node.Type == FbxExNode.MeshType).Any(node =>
            Enumerable.Range(0, node.VertexCount).Any(v =>
                Tolerance.Near(node.Vertices[v * 12], expected[0], 1e-4f)
                && Tolerance.Near(node.Vertices[v * 12 + 1], expected[1], 1e-4f)
                && Tolerance.Near(node.Vertices[v * 12 + 2], expected[2], 1e-4f)));

        // Assert
        Assert.True(found);
    }

    [Fact]
    public void Convert_LayeredExport_ComesBack()
    {
        // Arrange
        ExportResult layered = StageExporter.Convert(new ExportSource
        {
            Model = FbxExWriter.Build(StageFactory.SmallStage()),
            Stage = "bg_layered",
            Objects = StageFactory.ObjectBytes(),
            Sheet = StageFactory.SmallSheet(),
            SheetName = "bg017",
        });

        // Act
        ArcStageResult? result = ArcStageImporter.Convert(BackFrom(layered, "bg_layered"));

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void Convert_NoModelInTheGeometry_GivesNothing()
    {
        // Arrange
        ArcStageInput input = new() { Geometry = new byte[64] };

        // Act
        ArcStageResult? result = ArcStageImporter.Convert(input);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void HoldsWholeModel_GeometryArchive_IsTrueAndSceneArchiveIsFalse()
    {
        // Arrange
        ExportArchives archives = StagePackager.Package(SmallExport(), ArcGame.Bbtag, "small");

        // Act
        bool geometryHoldsIt = ArcStageImporter.HoldsWholeModel(archives.Geometry);
        bool sceneHoldsIt = ArcStageImporter.HoldsWholeModel(archives.Scene);

        // Assert
        Assert.True(geometryHoldsIt);
        Assert.False(sceneHoldsIt);
    }
}
