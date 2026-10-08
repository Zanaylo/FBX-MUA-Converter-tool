using System.Text;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Sources;

public class ArcIdListUnitTest
{
    [Fact]
    public void StageNames_StageEntry_MapsTheFolderToItsName()
    {
        // Arrange
        string english = "BG_CASTLE\r\nCrimson Throne\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, string.Empty);

        // Assert
        Assert.Equal("Crimson Throne", Assert.Single(names, pair => pair.Key == "bg_castle").Value);
    }

    [Fact]
    public void StageNames_GlyphCodes_BecomeTheCharactersTheGameDraws()
    {
        // Arrange
        string english = "BG_ISHANA\r\nMagister|s City -ISHANA-\r\nBG_MAIN_NIGHT\r\nKagutsuchi PORT PM 9^00\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, string.Empty);

        // Assert
        Assert.Equal("Magister's City -ISHANA-", names["bg_ishana"]);
        Assert.Equal("Kagutsuchi PORT PM 9:00", names["bg_main_night"]);
    }

    [Fact]
    public void StageNames_SqueezedEnglishName_TakesTheSpacingFromTheJapaneseList()
    {
        // Arrange
        string english = "BG_CHURCH_3\r\nSymbolicDomination-Cathedral-\r\n";
        string japanese = "BG_CHURCH_3\r\nSymbolic Domination -Cathedral-\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, japanese);

        // Assert
        Assert.Equal("Symbolic Domination -Cathedral-", names["bg_church_3"]);
    }

    [Fact]
    public void StageNames_JapaneseListWithAnotherName_KeepsTheEnglishName()
    {
        // Arrange
        string english = "BG_CASTLE\r\nCrimson Throne\r\n";
        string japanese = "BG_CASTLE\r\nCrimson Castle\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, japanese);

        // Assert
        Assert.Equal("Crimson Throne", names["bg_castle"]);
    }

    [Fact]
    public void StageNames_PlaceholderRepeatingTheId_IsLeftOut()
    {
        // Arrange
        string english = "BG_BLUEGATE_2\r\nBLUEGATE_2\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, string.Empty);

        // Assert
        Assert.Empty(names);
    }

    [Fact]
    public void StageNames_OtherIds_AreLeftOut()
    {
        // Arrange
        string english = "Chara_NameES_Short\r\nEs\r\n";

        // Act
        Dictionary<string, string> names = ArcIdList.StageNames(english, string.Empty);

        // Assert
        Assert.Empty(names);
    }

    [Fact]
    public void Text_Utf16WithByteOrderMark_IsReadWithoutTheMark()
    {
        // Arrange
        byte[] data = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("BG_CASTLE")];

        // Act
        string text = ArcIdList.Text(data);

        // Assert
        Assert.Equal("BG_CASTLE", text);
    }

    [Fact]
    public void Text_MissingFile_IsEmpty()
    {
        // Arrange
        byte[]? data = null;

        // Act
        string text = ArcIdList.Text(data);

        // Assert
        Assert.Empty(text);
    }
}
