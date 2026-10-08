using FbxToMua.Core.Import;

namespace FbxToMua.Core.Tests.Import;

public sealed class ImLibraryUnitTest : IDisposable
{
    private readonly string _game = Path.Combine(Path.GetTempPath(), "FbxToMuaTests", Guid.NewGuid().ToString("N"));

    public ImLibrary InstanciarImLibrary(params string[] iniLines)
    {
        Directory.CreateDirectory(Path.Combine(_game, "UNI2-IM", "Mods", "bg"));
        File.WriteAllLines(Path.Combine(_game, "UNI2-IM", "UNI2_IM.ini"), ["[StageLibrary]", .. iniLines]);

        return new ImLibrary(
            _game
        );
    }

    public void Dispose()
    {
        if (Directory.Exists(_game))
            Directory.Delete(_game, true);
    }

    [Fact]
    public void FirstFree_EmptyLibrary_StartsAtTheFirstModSlot()
    {
        // Arrange
        ImLibrary library = InstanciarImLibrary();

        // Act
        int id = library.FirstFree();

        // Assert
        Assert.Equal(28, id);
    }

    [Fact]
    public void FirstFree_RegisteredAndExistingFolders_AreSkipped()
    {
        // Arrange
        ImLibrary library = InstanciarImLibrary("Lib28=1|BBTAG|bg_town|Town", "Lib29=1|BBTAG|bg_ring|Ring");
        Directory.CreateDirectory(Path.Combine(library.Root, "bg030"));

        // Act
        int id = library.FirstFree();

        // Assert
        Assert.Equal(31, id);
    }

    [Fact]
    public void Installed_WithoutTheModFolder_IsFalse()
    {
        // Arrange
        ImLibrary library = new(Path.Combine(_game, "nowhere"));

        // Act
        bool installed = library.Installed;

        // Assert
        Assert.False(installed);
    }
}
