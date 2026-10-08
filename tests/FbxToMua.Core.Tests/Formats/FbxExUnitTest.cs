using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Formats;

public class FbxExUnitTest
{
    [Fact]
    public void Read_WrittenStage_GivesNodeFlagsAndFramesBack()
    {
        // Arrange
        byte[] blob = FbxExWriter.Build(StageFactory.SmallStage());

        // Act
        FbxExModel? read = FbxExReader.Read(blob);

        // Assert
        Assert.NotNull(read);
        Assert.Equal(3, read.Nodes.Count);
        Assert.Equal(1, read.Nodes[2].Alpha);
        Assert.Equal(1, read.Nodes[2].BlendMode);
        Assert.Equal(8 * 16, read.Animes[2].Count);
    }

    [Fact]
    public void Build_ReadStage_GivesTheSameBytes()
    {
        // Arrange
        byte[] blob = FbxExWriter.Build(StageFactory.SmallStage());

        // Act
        byte[] again = FbxExWriter.Build(FbxExReader.Read(blob)!);

        // Assert
        Assert.Equal(blob, again);
    }

    [Fact]
    public void Read_Garbage_IsRefused()
    {
        // Arrange
        byte[] garbage = new byte[10];

        // Act
        FbxExModel? read = FbxExReader.Read(garbage);

        // Assert
        Assert.Null(read);
    }

    [Fact]
    public void Hierarchy_SmallStage_FindsParentsAndTheSpinningNode()
    {
        // Arrange
        FbxExModel stage = StageFactory.SmallStage();

        // Act
        FbxExHierarchy scene = new(stage);

        // Assert
        Assert.Equal(0, scene.Parent(1));
        Assert.Equal(0, scene.Parent(2));
        Assert.Equal(-1, scene.Parent(0));
        Assert.True(scene.Varies(2));
        Assert.False(scene.Varies(1));
        Assert.Equal(8, scene.Frames(2));
    }
}
