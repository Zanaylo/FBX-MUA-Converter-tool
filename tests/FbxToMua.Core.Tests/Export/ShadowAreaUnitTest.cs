using FbxToMua.Core.Export;

namespace FbxToMua.Core.Tests.Export;

public class ShadowAreaUnitTest
{
    private static readonly int[] ShippedLight = [0, 900, -1000];
    private static readonly int[] LeaningLight = [-326, 690, -946];
    private static readonly int[] OtherLeaningLight = [326, 690, -946];
    private static readonly int[] BackLight = [0, 333, 1000];
    private static readonly int[] SideLight = [-1000, 900, 0];

    [Fact]
    public void Takes_ShippedLightOnAWideFloor_True()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        bool takes = ShadowArea.Takes(light, 3.0f, -2772.0f, 1499.0f);

        // Assert
        Assert.True(takes);
    }

    [Fact]
    public void Takes_ShippedLightOnAFloorBehindTheFightLine_False()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        bool takes = ShadowArea.Takes(light, 0.0f, 50.0f, 900.0f);

        // Assert
        Assert.False(takes);
    }

    [Fact]
    public void Takes_LightLeaningToNegativeXOnAWideFloor_False()
    {
        // Arrange
        int[] light = LeaningLight;

        // Act
        bool takes = ShadowArea.Takes(light, 3.0f, -2772.0f, 1499.0f);

        // Assert
        Assert.False(takes);
    }

    [Fact]
    public void Takes_LightLeaningToNegativeXOnANarrowFloor_True()
    {
        // Arrange
        int[] light = LeaningLight;

        // Act
        bool takes = ShadowArea.Takes(light, 0.0f, -150.0f, 150.0f);

        // Assert
        Assert.True(takes);
    }

    [Fact]
    public void Takes_LightLeaningToPositiveXOnAWideFloor_True()
    {
        // Arrange
        int[] light = OtherLeaningLight;

        // Act
        bool takes = ShadowArea.Takes(light, 3.0f, -2772.0f, 1499.0f);

        // Assert
        Assert.True(takes);
    }

    [Fact]
    public void Takes_FloorBottomAboveTheArea_False()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        bool takes = ShadowArea.Takes(light, 1200.0f, -500.0f, 500.0f);

        // Assert
        Assert.False(takes);
    }

    [Fact]
    public void Takes_BackLightOnAWideFloor_True()
    {
        // Arrange
        int[] light = BackLight;

        // Act
        bool takes = ShadowArea.Takes(light, 0.0f, -2000.0f, 2000.0f);

        // Assert
        Assert.True(takes);
    }

    [Fact]
    public void Takes_SideLightOnAWideFloor_True()
    {
        // Arrange
        int[] light = SideLight;

        // Act
        bool takes = ShadowArea.Takes(light, 0.0f, -2000.0f, 2000.0f);

        // Assert
        Assert.True(takes);
    }

    [Fact]
    public void Band_ShippedLight_IsOpen()
    {
        // Arrange
        int[] light = ShippedLight;

        // Act
        ShadowBand band = ShadowArea.Band(light);

        // Assert
        Assert.True(band.IsOpen);
    }

    [Fact]
    public void Band_LightLeaningToNegativeX_IsTheStripTheGameAccepts()
    {
        // Arrange
        int[] light = LeaningLight;
        float edge = (float)(2048.0 / BattleCamera.LayerUnits() / (1.0 + 946.0 / 326.0));

        // Act
        ShadowBand band = ShadowArea.Band(light);

        // Assert
        Assert.False(band.IsOpen);
        Assert.InRange(band.High, edge * 0.97f, edge);
        Assert.InRange(band.Low, -edge, -edge * 0.97f);
    }

    [Fact]
    public void Band_LightLeaningToNegativeX_IsTakenByTheGame()
    {
        // Arrange
        int[] light = LeaningLight;

        // Act
        ShadowBand band = ShadowArea.Band(light);

        // Assert
        Assert.True(ShadowArea.Takes(light, 0.0f, band.Low, band.High));
    }

    [Fact]
    public void Holds_RangeInsideTheBand_True()
    {
        // Arrange
        ShadowBand band = new(-100.0f, 100.0f);

        // Act
        bool holds = band.Holds(-50.0f, 80.0f);

        // Assert
        Assert.True(holds);
    }

    [Fact]
    public void Holds_RangeCrossingTheBand_False()
    {
        // Arrange
        ShadowBand band = new(-100.0f, 100.0f);

        // Act
        bool holds = band.Holds(-50.0f, 180.0f);

        // Assert
        Assert.False(holds);
    }
}
