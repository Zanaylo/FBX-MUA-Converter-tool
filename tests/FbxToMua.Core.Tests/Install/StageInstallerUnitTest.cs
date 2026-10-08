using FbxToMua.Core.Export;
using FbxToMua.Core.Formats.FbxEx;
using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Install;
using FbxToMua.Core.Sources;
using FbxToMua.Core.Tests.Fakes;
using NSubstitute;

namespace FbxToMua.Core.Tests.Install;

public sealed class StageInstallerUnitTest : IDisposable
{
    private static readonly StageTarget Castle = new("main", "bg_castle");

    private readonly ISteamLibrary _steamMock = Substitute.For<ISteamLibrary>();

    private FakeGameFolder _folder = FakeGameFolder.Loose("BBCF.exe", "bg_castle", "castle");
    private MemoryInstallRecords _recordsMock = null!;

    public StageInstaller InstanciarStageInstaller(FakeGameFolder? folder = null)
    {
        _folder = folder ?? _folder;
        _recordsMock = new MemoryInstallRecords(_folder.Workspace);
        _steamMock.GameFolder(Arg.Any<string>()).Returns(_folder.Game);

        return new StageInstaller(
            _recordsMock,
            _steamMock
        );
    }

    public void Dispose() => _folder.Dispose();

    private static ExportResult SmallExport()
    {
        return StageExporter.Convert(new ExportSource { Model = FbxExWriter.Build(StageFactory.SmallStage()), Stage = "bg_small" });
    }

    private string GameFile(string suffix) => Path.Combine(_folder.Game, "data", "bg", "main", "bg_castle" + suffix);

    [Fact]
    public void Targets_LooseBbcf_ListsTheStageWithItsThreeArchives()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();

        // Act
        IReadOnlyList<StageTarget> targets = installer.Targets(ArcGame.Bbcf);

        // Assert
        Assert.Equal([Castle], targets);
    }

    [Fact]
    public void Install_FirstTime_BacksUpTheOriginalAndRecordsTheStage()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();
        byte[] original = File.ReadAllBytes(GameFile(".pac"));

        // Act
        InstallReport report = installer.Install(ArcGame.Bbcf, Castle, SmallExport(), "Small Stage");

        // Assert
        Assert.True(report.Done);
        Assert.True(installer.HasBackup(ArcGame.Bbcf, Castle));
        Assert.Equal("Small Stage", installer.Installed(ArcGame.Bbcf, Castle));
        Assert.NotEqual(original, File.ReadAllBytes(GameFile(".pac")));
    }

    [Fact]
    public void Install_OverBbcf_NamesTheModelAfterTheReplacedStage()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();

        // Act
        installer.Install(ArcGame.Bbcf, Castle, SmallExport(), "Small Stage");
        byte[] scene = File.ReadAllBytes(GameFile(".pac"));

        // Assert
        Assert.Equal("castle", InstallRules.ModelName(scene));
        Assert.True(InstallRules.Loadable(ArcGame.Bbcf, scene));
    }

    [Fact]
    public void Install_Twice_KeepsTheFirstBackup()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();
        byte[] original = File.ReadAllBytes(GameFile(".pac"));

        // Act
        installer.Install(ArcGame.Bbcf, Castle, SmallExport(), "First");
        installer.Install(ArcGame.Bbcf, Castle, SmallExport(), "Second");
        installer.Restore(ArcGame.Bbcf, Castle);

        // Assert
        Assert.Equal(original, File.ReadAllBytes(GameFile(".pac")));
    }

    [Fact]
    public void Restore_AfterInstall_PutsEveryArchiveBackAndClearsTheRecord()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();
        byte[][] originals = [File.ReadAllBytes(GameFile(".pac")), File.ReadAllBytes(GameFile("_vtx.pac")), File.ReadAllBytes(GameFile("_img.pac"))];
        installer.Install(ArcGame.Bbcf, Castle, SmallExport(), "Small Stage");

        // Act
        InstallReport report = installer.Restore(ArcGame.Bbcf, Castle);

        // Assert
        Assert.True(report.Done);
        Assert.Equal(originals[0], File.ReadAllBytes(GameFile(".pac")));
        Assert.Equal(originals[1], File.ReadAllBytes(GameFile("_vtx.pac")));
        Assert.Equal(originals[2], File.ReadAllBytes(GameFile("_img.pac")));
        Assert.Null(installer.Installed(ArcGame.Bbcf, Castle));
        Assert.False(installer.HasBackup(ArcGame.Bbcf, Castle));
    }

    [Fact]
    public void Restore_WithoutBackup_SaysSo()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();

        // Act
        InstallReport report = installer.Restore(ArcGame.Bbcf, Castle);

        // Assert
        Assert.False(report.Done);
        Assert.Equal("There is no backup of bg_castle.", report.Message);
    }

    [Fact]
    public void Install_RetailBbtag_WritesTheArchivesEncryptedUnderTheirHashedNames()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller(FakeGameFolder.Hashed("bg_castle", "castle"));
        HashedStageStore store = new(_folder.Game);

        // Act
        InstallReport report = installer.Install(ArcGame.Bbtag, Castle, SmallExport(), "Small Stage");
        byte[] scene = store.ReadPlain(Castle, ArchivePart.Scene)!;

        // Assert
        Assert.True(report.Done);
        Assert.Equal(["mdl.pac", "scr.pac", "mot.pac"], Fpac.Names(scene));
        Assert.Equal("castle", InstallRules.ModelName(scene));
    }

    [Fact]
    public void Restore_RetailBbtag_PutsTheEncryptedOriginalBack()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller(FakeGameFolder.Hashed("bg_castle", "castle"));
        string scenePath = Path.Combine(_folder.Game, "asset", ArcCrypt.NameOf("data/bg/main/bg_castle.pac"));
        byte[] original = File.ReadAllBytes(scenePath);
        installer.Install(ArcGame.Bbtag, Castle, SmallExport(), "Small Stage");

        // Act
        installer.Restore(ArcGame.Bbtag, Castle);

        // Assert
        Assert.Equal(original, File.ReadAllBytes(scenePath));
    }

    [Fact]
    public void GameFolder_WrongGame_IsNotChosen()
    {
        // Arrange
        StageInstaller installer = InstanciarStageInstaller();

        // Act
        bool chosen = installer.ChooseGameFolder(ArcGame.Bbtag, _folder.Game);

        // Assert
        Assert.False(chosen);
    }
}
