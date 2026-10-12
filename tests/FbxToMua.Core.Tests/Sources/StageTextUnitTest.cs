using FbxToMua.Core.Export;
using FbxToMua.Core.Formats;
using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Sources;

public class StageTextUnitTest
{
    [Fact]
    public void Framing_StageNote_ReadsScalePositionAndRotations()
    {
        // Arrange
        string note = "// UNI2 Improvement Mod\r\nScale = [ 0.2, 0.3, 0.4 ],\r\nPosition = [ 1, -2, 3.5 ],\r\nViewRotationX = 1,\r\nViewRotationY = -2.5,\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.Equal((0.2f, 0.3f, 0.4f), (framing.Scale[0], framing.Scale[1], framing.Scale[2]));
        Assert.Equal((1.0f, -2.0f, 3.5f), (framing.Position[0], framing.Position[1], framing.Position[2]));
        Assert.Equal(1.0f, framing.Tilt);
        Assert.Equal(-2.5f, framing.Turn);
    }

    [Fact]
    public void Framing_StageNoteWithClearFog_IsNotFogged()
    {
        // Arrange
        string note = "IsFog = 1,\r\nFogStart = -100.0, FogEnd = 1000.0,\r\nFogColor = [ 0.0, 0.0, 0.0, 0.0 ],\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.Equal((-100.0f, 1000.0f), (framing.Look.Fog.Start, framing.Look.Fog.End));
        Assert.False(framing.Look.Fog.On);
    }

    [Fact]
    public void Framing_FogSwitchedOff_IsNotFogged()
    {
        // Arrange
        string note = "IsFog = 0,\r\nFogStart = 0.0, FogEnd = 100.0,\r\nFogColor = [ 1.0, 0.0, 0.0, 1.0 ],\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.False(framing.Look.Fog.On);
    }

    [Fact]
    public void Framing_StageNote_ReadsTheBloomButLeavesItOff()
    {
        // Arrange
        string note = "BGBloomEnable = 1,\r\nBGBloomBlightness = 0.6,\r\nBGBloomPower = 2.00,\r\nBGBloomBiassR = 2.3,\r\n" +
            "BGBloomBiassG = 1.5,\r\nBGBloomBiassB = 0.6,\r\nBGBloomBlurRadius = 1.20,\r\nBGBloomTextureSize = 256,\r\nBGBloomAlpha = 0.4,\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.Equal(new BloomLook(false, 0.6f, 2.0f, 2.3f, 1.5f, 0.6f, 1.2f, 0.4f), framing.Look.Bloom);
    }

    [Fact]
    public void Framing_NoteWithoutLook_IsPlain()
    {
        // Arrange
        string note = "Scale = [ 1.0, 1.0, 1.0 ],\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.Equal(StageLook.Plain, framing.Look);
    }

    [Fact]
    public void Framing_EmptyNote_IsNeutral()
    {
        // Arrange
        string note = string.Empty;

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.Equal(10.0f, framing.Scale[0]);
        Assert.Equal(0.0f, framing.Tilt);
    }

    [Theory]
    [InlineData("Central Station", "bg_central_station")]
    [InlineData("Magician's Night EX -Stars-", "bg_magician_s_night_ex_stars")]
    [InlineData("Tohno's Mansion: Main Gate", "bg_tohno_s_mansion_main_gate")]
    [InlineData("???", "bg_stage")]
    [InlineData("A very long stage name that goes on and on forever", "bg_a_very_long_stage_name_that_goes_on_a")]
    public void Stem_StageName_IsAShortAsciiName(string name, string expected)
    {
        // Act
        string stem = StageNames.Stem(name);

        // Assert
        Assert.Equal(expected, stem);
    }

    [Fact]
    public void Stem_NameWithoutLatinLetters_UsesTheFallback()
    {
        // Arrange
        string name = "???";
        string fallback = "bg001";

        // Act
        string stem = StageNames.Stem(name, fallback);

        // Assert
        Assert.Equal("bg001", stem);
    }

    [Fact]
    public void Stem_LatinNameWithFallback_IgnoresTheFallback()
    {
        // Arrange
        string name = "Central Station";
        string fallback = "bg001";

        // Act
        string stem = StageNames.Stem(name, fallback);

        // Assert
        Assert.Equal("bg_central_station", stem);
    }

    [Theory]
    [InlineData("  42abc", 42)]
    [InlineData("-7", -7)]
    [InlineData("abc", 0)]
    [InlineData("", 0)]
    public void Atoi_Text_ReadsLikeC(string text, int expected)
    {
        // Act
        int value = CText.Atoi(text);

        // Assert
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-20.5", -20.5)]
    [InlineData(" 1735 ", 1735.0)]
    [InlineData("1e3x", 1000.0)]
    [InlineData("x", 0.0)]
    public void Atof_Text_ReadsLikeC(string text, double expected)
    {
        // Act
        double value = CText.Atof(text);

        // Assert
        Assert.Equal(expected, value);
    }

    [Fact]
    public void Framing_StageWithAVanishingPoint_FogsByDistance()
    {
        // Arrange
        string note = "VanishingPoint = 0.2\r\nIsFog = 1,\r\nFogStart = 0.0, FogEnd = 1000.0,\r\nFogColor = [ 1.0, -0.5, 0.2, 1.0 ],\r\n";

        // Act
        Framing framing = StageText.Framing(note);

        // Assert
        Assert.True(framing.Look.Fog.ByDistance);
    }
}
