using FbxToMua.Core.Export;
using FbxToMua.Core.Geometry;
using FbxToMua.Core.Preview;

namespace FbxToMua.Core.Tests.Preview;

public class ShadowCastUnitTest
{
    private const float Width = 64.0f;
    private const float Height = 150.0f;
    private static readonly ShadowCaster TowardsTheCamera = new(0.0f, Width, Height, 0.0f, -1.0f);

    private static ShadowPiece<Float3> Ground(float near, float far)
    {
        return new ShadowPiece<Float3>([new(-1000.0f, 0.0f, near), new(1000.0f, 0.0f, near), new(1000.0f, 0.0f, far), new(-1000.0f, 0.0f, far)], [[0, 2, 1, 0, 3, 2]]);
    }

    private static double Area(ShadowSurface surface)
    {
        double area = 0.0;

        for (int i = 0; i + 2 < surface.Triangles.Length; i += 3)
        {
            Float3 a = surface.Positions[surface.Triangles[i]];
            Float3 b = surface.Positions[surface.Triangles[i + 1]];
            Float3 c = surface.Positions[surface.Triangles[i + 2]];
            area += Math.Abs((b[0] - a[0]) * (c[2] - a[2]) - (c[0] - a[0]) * (b[2] - a[2])) * 0.5;
        }

        return area;
    }

    [Fact]
    public void Onto_FloorUnderTheFighter_CoversTheFighterTallShadow()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [Ground(-1000.0f, 1000.0f)];

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, TowardsTheCamera);

        // Assert
        Assert.Equal(Width * Height, Area(surface), 1);
        Assert.All(surface.Positions, point => Assert.InRange(point[2], -Height - 1e-3f, 1e-3f));
        Assert.All(surface.Uvs, uv => Assert.InRange(uv, -1e-5f, 1.0f + 1e-5f));
    }

    [Fact]
    public void Onto_FloorCutToAStrip_StopsAtTheStrip()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [Ground(-50.0f, 50.0f)];

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, TowardsTheCamera);

        // Assert
        Assert.Equal(Width * 50.0f, Area(surface), 1);
    }

    [Fact]
    public void Onto_FighterFarFromTheFloor_IsEmpty()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [Ground(-1000.0f, 1000.0f)];
        ShadowCaster away = TowardsTheCamera with { X = 5000.0f };

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, away);

        // Assert
        Assert.Empty(surface.Triangles);
    }

    [Fact]
    public void Onto_NoLitFloor_IsEmpty()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [];

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, TowardsTheCamera);

        // Assert
        Assert.Empty(surface.Triangles);
    }

    [Fact]
    public void Onto_LightFromTheSide_IsEmpty()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [Ground(-1000.0f, 1000.0f)];
        ShadowCaster sideways = TowardsTheCamera with { FallX = -1.0f, FallZ = 0.0f };

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, sideways);

        // Assert
        Assert.Empty(surface.Triangles);
    }

    [Fact]
    public void Onto_LeaningLight_ShearsTheShadowSideways()
    {
        // Arrange
        List<ShadowPiece<Float3>> lit = [Ground(-1000.0f, 1000.0f)];
        ShadowCaster leaning = TowardsTheCamera with { FallX = -1.0f };

        // Act
        ShadowSurface surface = ShadowCast.Onto(lit, Matrix.Identity, leaning);

        // Assert
        Assert.InRange(surface.Positions.Min(point => point[0]), -Height - Width * 0.5f - 1e-2f, -Height - Width * 0.5f + 1e-2f);
    }
}
