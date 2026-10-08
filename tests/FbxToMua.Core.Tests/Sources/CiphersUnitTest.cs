using FbxToMua.Core.Sources;

namespace FbxToMua.Core.Tests.Sources;

public class CiphersUnitTest
{
    [Fact]
    public void NameOf_BackslashesAndCapitals_HashTheSameAsTheCanonicalPath()
    {
        // Arrange
        string canonical = ArcCrypt.NameOf("data/bg/main/bg_town.pac");

        // Act
        string other = ArcCrypt.NameOf(@"DATA\BG\Main\BG_TOWN.pac");

        // Assert
        Assert.Equal(canonical, other);
        Assert.Equal(32, canonical.Length);
        Assert.True(canonical.All(letter => char.IsAsciiHexDigitLower(letter) || char.IsAsciiDigit(letter)));
    }

    [Fact]
    public void Apply_Twice_GivesTheDataBack()
    {
        // Arrange
        byte[] data = Enumerable.Range(0, 300).Select(i => (byte)i).ToArray();
        byte[] sealedBytes = (byte[])data.Clone();

        // Act
        ArcCrypt.Apply(ArcKey.Bbtag, "data/bg/main/bg_town.pac", sealedBytes);
        ArcCrypt.Apply(ArcKey.Bbtag, "data/bg/main/bg_town.pac", sealedBytes);

        // Assert
        Assert.Equal(data, sealedBytes);
    }

    [Fact]
    public void Apply_DifferentKeys_GiveDifferentBytes()
    {
        // Arrange
        byte[] bbtag = new byte[64];
        byte[] p4u2 = new byte[64];

        // Act
        ArcCrypt.Apply(ArcKey.Bbtag, "data/bg/main/bg_town.pac", bbtag);
        ArcCrypt.Apply(ArcKey.P4u2, "data/bg/main/bg_town.pac", p4u2);

        // Assert
        Assert.NotEqual(bbtag, p4u2);
    }

    [Fact]
    public void Md5_KnownText_IsTheStandardDigest()
    {
        // Arrange
        string text = "abc";

        // Act
        string digest = ArcCrypt.Md5(text);

        // Assert
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", digest);
    }
}
