using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Tests.Fakes;
using FbxToMua.Core.Workflows;

namespace FbxToMua.Core.Tests.Workflows;

public sealed class StageWorkflowsUnitTest : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "FbxToMuaTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private string StageFolderNamed(string leaf)
    {
        string folder = Path.Combine(_root, leaf);
        Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, "bg.fbx.bin"), FbxExWriter.Build(StageFactory.SmallStage()));

        return folder;
    }

    private string EmptyFolder()
    {
        string folder = Path.Combine(_root, "empty");
        Directory.CreateDirectory(folder);

        return folder;
    }

    [Fact]
    public void Export_StageFolder_IsNamedAfterTheFolder()
    {
        // Arrange
        string folder = StageFolderNamed("Last Corridor");

        // Act
        ExportedStage exported = StageWorkflows.Export(folder, null);

        // Assert
        Assert.Equal("Last Corridor", exported.Name);
        Assert.Equal("bg_last_corridor", exported.Result.Stage);
    }

    [Fact]
    public void Export_Reframed_ReachesTheExport()
    {
        // Arrange
        string folder = StageFolderNamed("bg113");
        Reframe reframe = Reframe.None with { Tilt = 7.0f };

        // Act
        ExportedStage exported = StageWorkflows.Export(folder, null, null, reframe);

        // Assert
        Assert.Equal(7, exported.Result.Tilt);
    }

    [Fact]
    public void Export_StageFolderWithAName_UsesTheName()
    {
        // Arrange
        string folder = StageFolderNamed("bg112");

        // Act
        ExportedStage exported = StageWorkflows.Export(folder, null, "Night Garden");

        // Assert
        Assert.Equal("Night Garden", exported.Name);
        Assert.Equal("bg_night_garden", exported.Result.Stage);
    }

    [Fact]
    public void Export_FolderWithoutStages_Throws()
    {
        // Arrange
        string folder = EmptyFolder();

        // Act
        Exception? failure = Record.Exception(() => StageWorkflows.Export(folder, "bg001"));

        // Assert
        Assert.IsType<StageConversionException>(failure);
        Assert.Equal($"no French-Bread stages found in {folder}", failure.Message);
    }

    [Fact]
    public void Import_FolderWithoutStages_Throws()
    {
        // Arrange
        string folder = EmptyFolder();

        // Act
        Exception? failure = Record.Exception(() => StageWorkflows.Import(folder, "bg_town"));

        // Assert
        Assert.IsType<StageConversionException>(failure);
        Assert.Equal($"no BBTAG, BBCF or P4U2 stages found in {folder}", failure.Message);
    }
}
