using FbxToMua.Core.Formats.Fpac;
using FbxToMua.Core.Install;

namespace FbxToMua.Core.Tests.Install;

public class InstallRulesUnitTest
{
    private static readonly byte[] ModelFolder = Fpac.Build([new FpacEntry("castle.MUA", Enumerable.Repeat((byte)1, 16).ToArray())]);
    private static readonly byte[] Empty = Fpac.Build([]);

    private static byte[] Complete() => Fpac.Build([new("mdl.pac", ModelFolder), new("scr.pac", Empty), new("mot.pac", Empty), new("cammot.pac", Empty)]);

    private static byte[] Cameraless() => Fpac.Build([new("mdl.pac", ModelFolder), new("scr.pac", Empty), new("mot.pac", Empty)]);

    [Fact]
    public void ModelName_PlainAndPacked_ComesFromTheModelFolder()
    {
        // Arrange
        byte[] plain = Complete();
        byte[] packed = Fpac.Pack(plain);

        // Act
        string plainName = InstallRules.ModelName(plain);
        string packedName = InstallRules.ModelName(packed);

        // Assert
        Assert.Equal("castle", plainName);
        Assert.Equal("castle", packedName);
    }

    [Fact]
    public void ModelName_NoModelFolder_IsEmpty()
    {
        // Arrange
        byte[] scene = Empty;

        // Act
        string name = InstallRules.ModelName(scene);

        // Assert
        Assert.Empty(name);
    }

    [Fact]
    public void Loadable_PlainAndPackedWithAllFourEntries_Loads()
    {
        // Arrange
        byte[] plain = Complete();
        byte[] packed = Fpac.Pack(plain);

        // Act
        bool plainLoadable = InstallRules.Loadable(plain);
        bool packedLoadable = InstallRules.Loadable(packed);

        // Assert
        Assert.True(plainLoadable);
        Assert.True(packedLoadable);
    }

    [Fact]
    public void Loadable_WithoutCameraFolder_Crashes()
    {
        // Arrange
        byte[] scene = Cameraless();

        // Act
        bool loadable = InstallRules.Loadable(scene);

        // Assert
        Assert.False(loadable);
    }

    [Fact]
    public void Loadable_NothingToLoad_IsNotLoadable()
    {
        // Arrange
        byte[] scene = Empty;

        // Act
        bool loadable = InstallRules.Loadable(scene);

        // Assert
        Assert.False(loadable);
    }
}
