using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Tests.Fakes;
using FbxToMua.Core.Workflows;
using NSubstitute;

namespace FbxToMua.Core.Tests.Workflows;

public sealed class KnownInstallsUnitTest : IDisposable
{
    private readonly ISteamLibrary _steamMock = Substitute.For<ISteamLibrary>();

    private readonly FakeGameFolder _folder = FakeGameFolder.Loose("BBCF.exe", "bg_castle", "castle");

    public void Dispose() => _folder.Dispose();

    private static KnownGame Known(GameKind game) => KnownInstalls.FrenchBread.Concat(KnownInstalls.ArcSystem).First(known => known.Game == game);

    [Fact]
    public void Find_SteamFolderHoldingTheGame_GivesTheFolder()
    {
        // Arrange
        _steamMock.GameFolder("BlazBlue Centralfiction").Returns(_folder.Game);

        // Act
        string? found = KnownInstalls.Find(_steamMock, Known(GameKind.Bbcf));

        // Assert
        Assert.Equal(_folder.Game, found);
    }

    [Fact]
    public void Find_SteamFolderHoldingAnotherGame_GivesNothing()
    {
        // Arrange
        _steamMock.GameFolder(Arg.Any<string>()).Returns(_folder.Game);

        // Act
        string? found = KnownInstalls.Find(_steamMock, Known(GameKind.Bbtag));

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void Find_GameNotInstalled_GivesNothing()
    {
        // Arrange
        _steamMock.GameFolder(Arg.Any<string>()).Returns((string?)null);

        // Act
        string? found = KnownInstalls.Find(_steamMock, Known(GameKind.Mbtl));

        // Assert
        Assert.Null(found);
    }

    [Fact]
    public void SteamFoldersOf_Bbtag_ListsBothInstallNames()
    {
        // Arrange
        GameKind game = GameKind.Bbtag;

        // Act
        IReadOnlyList<string> folders = KnownInstalls.SteamFoldersOf(game);

        // Assert
        Assert.Equal(["BlazBlue Cross Tag Battle", "BBTAG"], folders);
    }

    [Fact]
    public void SteamFoldersOf_NoGame_IsEmpty()
    {
        // Arrange
        GameKind game = GameKind.None;

        // Act
        IReadOnlyList<string> folders = KnownInstalls.SteamFoldersOf(game);

        // Assert
        Assert.Empty(folders);
    }
}
