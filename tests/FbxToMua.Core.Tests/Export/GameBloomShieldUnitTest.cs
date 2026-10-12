using FbxToMua.Core.Export;
using FbxToMua.Core.Imaging;

namespace FbxToMua.Core.Tests.Export;

public class GameBloomShieldUnitTest
{
    private static DepthSpan Span(double nearest, double farthest) => new() { Nearest = nearest, Farthest = farthest };

    private static readonly FarPull Kept = new(false, true, 1.0f);

    [Fact]
    public void UnwrittenFront_EveryNodeWritesDepth_IsEndless()
    {
        // Arrange
        DepthSpan[] spans = [Span(300.0, 900.0), Span(1000.0, 5000.0)];
        FarPull[] pulls = [Kept, Kept];

        // Act
        double front = GameBloomShield.UnwrittenFront(spans, pulls);

        // Assert
        Assert.Equal(double.MaxValue, front);
    }

    [Fact]
    public void UnwrittenFront_PulledNodeWithoutDepth_IsItsNearestOncePulled()
    {
        // Arrange
        DepthSpan[] spans = [Span(300.0, 50000.0), Span(30000.0, 200000.0)];
        FarPull[] pulls = [Kept, new FarPull(true, false, 0.5f)];

        // Act
        double front = GameBloomShield.UnwrittenFront(spans, pulls);

        // Assert
        Assert.Equal(15000.0, front, 1e-6);
    }

    [Fact]
    public void DrawsFirstSafely_WholeNodeInFrontOfTheUnwritten_IsTrue()
    {
        // Arrange
        DepthSpan span = Span(300.0, 900.0);

        // Act
        bool safe = GameBloomShield.DrawsFirstSafely(span, Kept, 15000.0);

        // Assert
        Assert.True(safe);
    }

    [Fact]
    public void DrawsFirstSafely_NodeReachingBehindTheUnwritten_IsFalse()
    {
        // Arrange
        DepthSpan span = Span(300.0, 50000.0);

        // Act
        bool safe = GameBloomShield.DrawsFirstSafely(span, Kept, 15000.0);

        // Assert
        Assert.False(safe);
    }

    [Fact]
    public void DrawsFirstSafely_PulledNode_MeasuresItWherePulled()
    {
        // Arrange
        DepthSpan span = Span(20000.0, 200000.0);
        FarPull pull = new(true, true, 0.05f);

        // Act
        bool safe = GameBloomShield.DrawsFirstSafely(span, pull, 15000.0);

        // Assert
        Assert.True(safe);
    }

    [Fact]
    public void DrawsFirstSafely_NodeWithoutDepth_IsFalse()
    {
        // Arrange
        DepthSpan span = Span(300.0, 900.0);
        FarPull pull = new(true, false, 1.0f);

        // Act
        bool safe = GameBloomShield.DrawsFirstSafely(span, pull, double.MaxValue);

        // Assert
        Assert.False(safe);
    }

    [Fact]
    public void IsSolid_EveryTexelOpaque_IsTrue()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(4, 4, 0xffff0000);

        // Act
        bool solid = GameBloomShield.IsSolid(image);

        // Assert
        Assert.True(solid);
    }

    [Fact]
    public void IsSolid_OneTexelSeeThrough_IsFalse()
    {
        // Arrange
        BgraImage image = ImageOps.Blank(4, 4, 0xffff0000);
        image.Pixels[image.At(3, 2) + BgraImage.Alpha] = 0xfe;

        // Act
        bool solid = GameBloomShield.IsSolid(image);

        // Assert
        Assert.False(solid);
    }
}
