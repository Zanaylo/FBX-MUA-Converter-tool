using FbxToMua.Core.Formats.Mua;
using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Formats;

public class MuaVertexUnitTest
{
    private static MuaVertex Vertex(float z, float u, byte grey, Float3 normal)
    {
        MuaVertex vertex = MuaVertex.Unrigged();
        vertex.Position = new Float3(10.0f, 0.0f, z);
        vertex.Normal = normal;
        vertex.U = u;
        vertex.V = 1.0f - u;
        vertex.Colour = new MuaColour(grey, grey, grey, byte.MaxValue);

        return vertex;
    }

    [Fact]
    public void Between_HalfWay_BlendsPositionUvAndColour()
    {
        // Arrange
        MuaVertex from = Vertex(-100.0f, 0.0f, 0, new Float3(0.0f, 1.0f, 0.0f));
        MuaVertex to = Vertex(100.0f, 1.0f, 200, new Float3(0.0f, 1.0f, 0.0f));

        // Act
        MuaVertex middle = MuaVertex.Between(from, to, 0.5f);

        // Assert
        Assert.Equal(new Float3(10.0f, 0.0f, 0.0f), middle.Position);
        Assert.Equal(0.5f, middle.U);
        Assert.Equal(0.5f, middle.V);
        Assert.Equal(new MuaColour(100, 100, 100, byte.MaxValue), middle.Colour);
    }

    [Fact]
    public void Between_TurningNormals_KeepsTheNormalUnitLength()
    {
        // Arrange
        MuaVertex from = Vertex(0.0f, 0.0f, 0, new Float3(1.0f, 0.0f, 0.0f));
        MuaVertex to = Vertex(0.0f, 0.0f, 0, new Float3(0.0f, 1.0f, 0.0f));

        // Act
        MuaVertex middle = MuaVertex.Between(from, to, 0.5f);

        // Assert
        float length = MathF.Sqrt(middle.Normal[0] * middle.Normal[0] + middle.Normal[1] * middle.Normal[1] + middle.Normal[2] * middle.Normal[2]);
        Assert.Equal(1.0f, length, 1e-5f);
    }

    [Fact]
    public void Between_RiggedVertices_KeepsTheFirstBinding()
    {
        // Arrange
        MuaVertex from = Vertex(0.0f, 0.0f, 0, new Float3(0.0f, 1.0f, 0.0f)).RiggedTo(3);
        MuaVertex to = Vertex(10.0f, 0.0f, 0, new Float3(0.0f, 1.0f, 0.0f)).RiggedTo(3);

        // Act
        MuaVertex middle = MuaVertex.Between(from, to, 0.25f);

        // Assert
        Assert.Equal(from.BoneIndex, middle.BoneIndex);
        Assert.Equal(from.Weight, middle.Weight);
    }

    [Fact]
    public void TryNormalise_ZeroVector_False()
    {
        // Arrange
        Float3 vector = new();

        // Act
        bool normalised = Float3.TryNormalise(ref vector);

        // Assert
        Assert.False(normalised);
    }

    [Fact]
    public void TryNormalise_LongVector_ScalesItToUnitLength()
    {
        // Arrange
        Float3 vector = new(3.0f, 0.0f, 4.0f);

        // Act
        bool normalised = Float3.TryNormalise(ref vector);

        // Assert
        Assert.True(normalised);
        Assert.Equal(new Float3(0.6f, 0.0f, 0.8f), vector);
    }
}
