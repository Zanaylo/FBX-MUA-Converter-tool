using FbxToMua.Core.Formats.Objects;
using FbxToMua.Core.Tests.Fakes;

namespace FbxToMua.Core.Tests.Formats;

public class ObjectListUnitTest
{
    [Fact]
    public void Read_ObjectText_LeavesTheCommentedEntryOutAndSortsByNumber()
    {
        // Arrange
        string text = StageFactory.ObjectText;

        // Act
        List<ObjectEntry> entries = ObjectList.Read(text);

        // Assert
        Assert.Equal(2, entries.Count);
        Assert.Equal(1, entries[0].Number);
        Assert.Equal(2, entries[1].Number);
    }

    [Fact]
    public void Read_ObjectText_ReadsFramesPrioStartAndDelay()
    {
        // Arrange
        string text = StageFactory.ObjectText;

        // Act
        ObjectEntry first = ObjectList.Read(text)[0];

        // Assert
        Assert.Equal([new ObjectFrame("walk0", 10), new ObjectFrame("walk1", 30)], first.Frames);
        Assert.Equal(271, first.Prio);
        Assert.Equal(50.0f, first.Start[0]);
        Assert.Equal(-20.5f, first.Start[1]);
        Assert.Equal(1735.0f, first.Start[2]);
        Assert.Equal(4, first.Delay);
    }

    [Fact]
    public void Read_ObjectText_ReadsTheSecondEntry()
    {
        // Arrange
        string text = StageFactory.ObjectText;

        // Act
        ObjectEntry second = ObjectList.Read(text)[1];

        // Assert
        Assert.Equal(402, second.Prio);
        Assert.Equal(-120.0f, second.Start[1]);
        Assert.Equal(0, second.Delay);
    }

    [Fact]
    public void Read_HundredAndTwentyEntries_KeepsTheFirstNinetyNine()
    {
        // Arrange
        string many = "BgObject <-\n{\n";

        for (int i = 1; i <= 120; ++i)
            many += $"data{i:D3} = [ {{ tag=\"frm\", name=\"a\", wait=1 }}, ]\n";

        many += "}\n";

        // Act
        List<ObjectEntry> capped = ObjectList.Read(many);

        // Assert
        Assert.Equal(ObjectList.MostEntries, capped.Count);
        Assert.Equal(ObjectList.MostEntries, capped[^1].Number);
    }

    [Fact]
    public void Read_Garbage_HasNoEntries()
    {
        // Arrange
        string garbage = "garbage";

        // Act
        List<ObjectEntry> entries = ObjectList.Read(garbage);

        // Assert
        Assert.Empty(entries);
    }

    [Fact]
    public void SpriteFile_ObjectText_NamesTheSheetLeaf()
    {
        // Arrange
        string text = StageFactory.ObjectText;

        // Act
        string sheet = ObjectList.SpriteFile(text);

        // Assert
        Assert.Equal("bg017.pat", sheet);
    }
}
