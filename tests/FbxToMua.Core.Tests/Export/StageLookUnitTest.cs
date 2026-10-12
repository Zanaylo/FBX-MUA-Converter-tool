using FbxToMua.Core.Export;
using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Tests.Export;

public class StageLookUnitTest
{
    private const uint OpaqueWhite = 0xffffffff;
    private const uint OpaqueBlack = 0xff000000;

    private static FogLook Fog(float start, float end) => new(true, start, end, 1.0f, -0.5f, 0.2f);

    private static BloomLook Glow(float radius) => new(true, 1.0f, 1.0f, 1.0f, 0.0f, 0.0f, radius, 0.5f);

    [Fact]
    public void Factor_FogRange_IsWhatTheClampedDepthOfOneGives()
    {
        // Arrange
        FogLook fog = Fog(0.0f, 1000.0f);

        // Act
        float factor = fog.FactorAt(StageDepth.Near);

        // Assert
        Assert.Equal(0.999f, factor, 1e-6f);
    }

    [Fact]
    public void Factor_StartPastOne_LeansAwayFromTheFog()
    {
        // Arrange
        FogLook fog = Fog(3.0f, 100.0f);

        // Act
        float factor = fog.FactorAt(StageDepth.Near);

        // Assert
        Assert.Equal(99.0f / 97.0f, factor, 1e-6f);
    }

    [Fact]
    public void Factor_SameStartAndEnd_PutsThemAHundredthApart()
    {
        // Arrange
        FogLook fog = Fog(20.0f, 20.0f);

        // Act
        float factor = fog.FactorAt(StageDepth.Near);

        // Assert
        Assert.Equal(1901.0f, factor, 0.5f);
    }

    [Fact]
    public void Visible_SlightFogOfAMildColour_IsNot()
    {
        // Arrange
        FogLook fog = Fog(0.0f, 1000.0f);

        // Act
        bool visible = fog.VisibleAt(StageDepth.Near);

        // Assert
        Assert.False(visible);
    }

    [Fact]
    public void Visible_SlightFogOfAStrongColour_Is()
    {
        // Arrange
        FogLook fog = new(true, 0.0f, 100.0f, -8.0f, 0.0f, 0.0f);

        // Act
        bool visible = fog.VisibleAt(StageDepth.Near);

        // Assert
        Assert.True(visible);
    }

    [Fact]
    public void Visible_SwitchedOff_IsNot()
    {
        // Arrange
        FogLook fog = new(false, 3.0f, 10.0f, -8.0f, 0.0f, 0.0f);

        // Act
        bool visible = fog.VisibleAt(StageDepth.Near);

        // Assert
        Assert.False(visible);
    }

    [Fact]
    public void Applied_Fog_LeansTheTexelTowardsItsColourAndKeepsItsAlpha()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(1, 1, 0x80214061);
        FogLook fog = new(true, -1.0f, 3.0f, 1.0f, -0.5f, 0.2f);

        // Act
        BgraImage fogged = fog.Applied(image, StageDepth.Near);

        // Assert
        Assert.Equal([0x4a, 0x00, 0x90, 0x80], fogged.Pixels);
    }

    [Fact]
    public void Applied_BloomOff_LeavesTheImageAlone()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(8, 8, OpaqueWhite);
        BloomLook bloom = Glow(1.0f) with { On = false };

        // Act
        BgraImage bloomed = bloom.Applied(image);

        // Assert
        Assert.Same(image, bloomed);
    }

    [Fact]
    public void Applied_Bloom_AddsItsBiasToBrightTexels()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(16, 16, 0xff404040);
        ImageOps.Fill(image, new PixelRect(4, 4, 8, 8), OpaqueWhite);

        // Act
        BgraImage bloomed = Glow(0.0f).Applied(image);

        // Assert
        int centre = bloomed.At(8, 8);
        Assert.Equal(0xff, bloomed.Pixels[centre + 2]);
        Assert.Equal(0xff, bloomed.Pixels[centre + 1]);
        Assert.True(bloomed.Pixels[bloomed.At(0, 0) + 2] < 0x80);
    }

    [Fact]
    public void Applied_BloomWithARadius_SpreadsPastTheBrightTexels()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(64, 64, OpaqueBlack);
        ImageOps.Fill(image, new PixelRect(28, 28, 8, 8), OpaqueWhite);

        // Act
        BgraImage bloomed = Glow(16.0f).Applied(image);

        // Assert
        int beside = bloomed.At(40, 32);
        Assert.True(bloomed.Pixels[beside + 2] > 0);
        Assert.Equal(0, bloomed.Pixels[beside]);
    }

    [Fact]
    public void Changes_PlainLook_IsFalse()
    {
        // Arrange
        StageLook look = StageLook.Plain;

        // Act
        bool changes = look.ChangesAt(StageDepth.Near);

        // Assert
        Assert.False(changes);
    }

    [Fact]
    public void Baked_NotADds_LeavesTheBytesAlone()
    {
        // Arrange
        byte[] png = [0x89, 0x50, 0x4e, 0x47];
        StageLook look = new(Fog(3.0f, 10.0f), Glow(1.0f));

        // Act
        byte[] baked = look.Baked(png, StageDepth.Near);

        // Assert
        Assert.Same(png, baked);
    }

    [Fact]
    public void FactorAt_FogByDistance_FollowsTheDepth()
    {
        // Arrange
        FogLook fog = Fog(0.0f, 1000.0f) with { ByDistance = true };

        // Act
        float factor = fog.FactorAt(178.0f);

        // Assert
        Assert.Equal(0.822f, factor, 1e-6f);
    }

    [Fact]
    public void FactorAt_FogNotByDistance_IgnoresTheDepth()
    {
        // Arrange
        FogLook fog = Fog(0.0f, 1000.0f);

        // Act
        float factor = fog.FactorAt(178.0f);

        // Assert
        Assert.Equal(0.999f, factor, 1e-6f);
    }
}
