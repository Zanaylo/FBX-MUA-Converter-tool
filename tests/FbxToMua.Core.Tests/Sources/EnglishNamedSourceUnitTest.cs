using FbxToMua.Core.Sources;
using NSubstitute;

namespace FbxToMua.Core.Tests.Sources;

public class EnglishNamedSourceUnitTest
{
    private const string BgListName = "bglist name";

    private readonly IStageSource _sourceMock = Substitute.For<IStageSource>();

    public EnglishNamedSource InstanciarEnglishNamedSource(GameKind game, params SourceStage[] stages)
    {
        _sourceMock.Game.Returns(game);
        _sourceMock.Stages().Returns(stages);

        return new EnglishNamedSource(
            _sourceMock
        );
    }

    [Fact]
    public void Stages_StageTheGameNamesInEnglish_TakesTheEnglishName()
    {
        // Arrange
        EnglishNamedSource source = InstanciarEnglishNamedSource(GameKind.Uni2, new SourceStage("bg001", BgListName, 10));

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal("Metropolitan Center: Intersection", TextDisplay.Of(stage.Name));
        Assert.Equal(("bg001", 10L), (stage.Folder, stage.Bytes));
    }

    [Fact]
    public void Stages_StageWithASymbolInItsEnglishName_ShowsTheSymbol()
    {
        // Arrange
        EnglishNamedSource source = InstanciarEnglishNamedSource(GameKind.Dfci, new SourceStage("bg17", BgListName, 10));

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal("Hi☆sCoool! SeHa Girls Stage", TextDisplay.Of(stage.Name));
    }

    [Fact]
    public void Stages_StageMissingFromTheTable_KeepsItsOwnName()
    {
        // Arrange
        EnglishNamedSource source = InstanciarEnglishNamedSource(GameKind.Uni2, new SourceStage("bg65535", BgListName, 10));

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal(BgListName, stage.Name);
    }

    [Fact]
    public void Read_AnyFile_ComesFromTheWrappedSource()
    {
        // Arrange
        EnglishNamedSource source = InstanciarEnglishNamedSource(GameKind.Uni2);
        byte[] model = [1, 2, 3];
        _sourceMock.Read("bg001", StageFolder.ModelFile).Returns(model);

        // Act
        byte[]? read = source.Read("bg001", StageFolder.ModelFile);

        // Assert
        Assert.Same(model, read);
    }

    [Fact]
    public void Dispose_Source_DisposesTheWrappedSource()
    {
        // Arrange
        EnglishNamedSource source = InstanciarEnglishNamedSource(GameKind.Uni2);

        // Act
        source.Dispose();

        // Assert
        _sourceMock.Received(1).Dispose();
    }
}
