using System.Text;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Sources;

public sealed class ArcSourceUnitTest : IDisposable
{
    private FakeGameFolder? _folder;

    public ArcSource InstanciarArcSource(string exe, string stem, string? englishIdList = null)
    {
        _folder = FakeGameFolder.Loose(exe, stem, "model");

        if (englishIdList is not null)
            WriteIdList(englishIdList);

        return ArcSource.Open(
            _folder.Game
        )!;
    }

    public void Dispose() => _folder?.Dispose();

    private void WriteIdList(string text)
    {
        string path = Path.Combine(_folder!.Game, "data", "localize", "eng_idlist.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes(text)]);
    }

    [Fact]
    public void Stages_BbcfWithItsIdList_UsesTheGamesEnglishName()
    {
        // Arrange
        ArcSource source = InstanciarArcSource("BBCF.exe", "bg_castle", "BG_CASTLE\r\nCrimson Throne\r\n");

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal("Crimson Throne", stage.Name);
    }

    [Fact]
    public void Stages_BbtagStageMissingFromItsIdList_UsesTheNameTable()
    {
        // Arrange
        ArcSource source = InstanciarArcSource("BBTAG.exe", "bg_beach", "BG_CASTLE\r\nCrimson Throne\r\n");

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal("Tropical Shrine", stage.Name);
    }

    [Fact]
    public void Stages_StageNamedNowhere_UsesItsReadableFolderName()
    {
        // Arrange
        ArcSource source = InstanciarArcSource("BBCF.exe", "bg_back_stage");

        // Act
        SourceStage stage = Assert.Single(source.Stages());

        // Assert
        Assert.Equal("Back Stage", stage.Name);
    }

    [Fact]
    public void NameOf_StageInCapitals_FindsItsName()
    {
        // Arrange
        ArcSource source = InstanciarArcSource("BBCF.exe", "bg_castle", "BG_CASTLE\r\nCrimson Throne\r\n");

        // Act
        string name = source.NameOf("BG_Castle");

        // Assert
        Assert.Equal("Crimson Throne", name);
    }
}
