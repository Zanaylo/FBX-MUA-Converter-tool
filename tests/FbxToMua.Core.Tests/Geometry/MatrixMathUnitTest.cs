using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Tests.Geometry;

public class MatrixMathUnitTest
{
    [Fact]
    public void Invert_TrsMatrix_MultipliesToIdentity()
    {
        // Arrange
        Matrix matrix = PoseMath.Compose(PoseMathUnitTest.TurnedPose());

        // Act
        Matrix product = MatrixMath.Multiply(matrix, MatrixMath.Invert(matrix));

        // Assert
        Assert.True(Tolerance.Near(Matrix.Identity, product, 1e-5f));
    }

    [Fact]
    public void Invert_SingularMatrix_GivesIdentity()
    {
        // Arrange
        Matrix singular = new();

        // Act
        Matrix inverse = MatrixMath.Invert(singular);

        // Assert
        Assert.Equal(Matrix.Identity, inverse);
    }

    [Fact]
    public void Multiply_Identity_KeepsTheMatrix()
    {
        // Arrange
        Matrix matrix = PoseMath.Compose(PoseMathUnitTest.TurnedPose());

        // Act
        Matrix product = MatrixMath.Multiply(Matrix.Identity, matrix);

        // Assert
        Assert.Equal(matrix, product);
    }
}
