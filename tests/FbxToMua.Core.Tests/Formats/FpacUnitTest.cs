using FbxToMua.Core.Binary;
using FbxToMua.Core.Formats.Fpac;

namespace FbxToMua.Core.Tests.Formats;

public class FpacUnitTest
{
    private static List<FpacEntry> NestedEntries()
    {
        byte[] inner = Fpac.Build([new FpacEntry("a.MUA", Enumerable.Repeat((byte)1, 37).ToArray())]);

        return [
            new FpacEntry("mdl.pac", inner),
            new FpacEntry("a_rather_long_name_17.evb", Enumerable.Repeat((byte)2, 5).ToArray()),
            new FpacEntry("b.mmot", Enumerable.Repeat((byte)3, 16).ToArray()),
        ];
    }

    public static byte[] NestedArchive() => Fpac.Build(NestedEntries());

    [Fact]
    public void Build_Entries_StartsWithMagicAndStatesItsSize()
    {
        // Arrange
        List<FpacEntry> entries = NestedEntries();

        // Act
        byte[] archive = Fpac.Build(entries);

        // Assert
        Assert.True(Fpac.IsArchive(archive));
        Assert.Equal((uint)archive.Length, LittleEndian.U32(archive, 8));
    }

    [Fact]
    public void Walk_NestedArchive_NamesTheInnerEntryByItsFolder()
    {
        // Arrange
        byte[] archive = NestedArchive();

        // Act
        SortedDictionary<string, byte[]> files = Fpac.Walk(archive);

        // Assert
        Assert.Equal(37, files["mdl/a.MUA"].Length);
        Assert.Equal(2, files["a_rather_long_name_17.evb"][4]);
        Assert.Equal(16, files["b.mmot"].Length);
    }

    [Fact]
    public void Pack_Archive_WalksTheSameThroughDfas()
    {
        // Arrange
        byte[] archive = NestedArchive();

        // Act
        byte[] packed = Fpac.Pack(archive);
        SortedDictionary<string, byte[]> files = Fpac.Walk(packed);

        // Assert
        Assert.Equal("DFASFPAC", System.Text.Encoding.ASCII.GetString(packed, 0, 8));
        Assert.Equal(Fpac.Walk(archive).Keys, files.Keys);
    }

    [Fact]
    public void Names_PlainAndPacked_ListTheTopLevelInOrder()
    {
        // Arrange
        byte[] archive = NestedArchive();
        byte[] packed = Fpac.Pack(archive);
        string[] expected = ["mdl.pac", "a_rather_long_name_17.evb", "b.mmot"];

        // Act
        IReadOnlyList<string> plainNames = Fpac.Names(archive);
        IReadOnlyList<string> packedNames = Fpac.Names(packed);

        // Assert
        Assert.Equal(expected, plainNames);
        Assert.Equal(expected, packedNames);
    }

    [Fact]
    public void Names_Garbage_IsEmpty()
    {
        // Arrange
        byte[] garbage = Enumerable.Repeat((byte)7, 40).ToArray();

        // Act
        IReadOnlyList<string> names = Fpac.Names(garbage);

        // Assert
        Assert.Empty(names);
    }

    [Fact]
    public void Build_NoEntries_IsTheEmptyMotFolderBbcfShips()
    {
        // Arrange
        byte[] shipped = [(byte)'F', (byte)'P', (byte)'A', (byte)'C', 0x20, 0, 0, 0, 0x20, 0, 0, 0, 0, 0, 0, 0,
            1, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

        // Act
        byte[] empty = Fpac.Build([]);

        // Assert
        Assert.Equal(shipped, empty);
    }

    [Fact]
    public void Named_LeafName_FindsTheEntryInAnyFolder()
    {
        // Arrange
        SortedDictionary<string, byte[]> files = Fpac.Walk(NestedArchive());

        // Act
        byte[]? found = Fpac.Named(files, "A.mua");

        // Assert
        Assert.NotNull(found);
        Assert.Equal(37, found.Length);
    }
}
