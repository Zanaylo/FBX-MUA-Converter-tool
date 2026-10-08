namespace FbxToMua.Core.Geometry;

public static class MatrixMath
{
    private const float Singular = 1e-12f;

    public static Matrix Multiply(Matrix left, Matrix right)
    {
        Matrix product = new();

        for (int r = 0; r < 4; ++r)
        {
            for (int c = 0; c < 4; ++c)
            {
                float total = 0.0f;

                for (int k = 0; k < 4; ++k)
                    total += left[r * 4 + k] * right[k * 4 + c];

                product[r * 4 + c] = total;
            }
        }

        return product;
    }

    public static Matrix Invert(Matrix matrix)
    {
        float[,] a =
        {
            { matrix[0], matrix[1], matrix[2] },
            { matrix[4], matrix[5], matrix[6] },
            { matrix[8], matrix[9], matrix[10] },
        };

        float determinant = a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1])
            - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0])
            + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);

        if (determinant > -Singular && determinant < Singular)
            return Matrix.Identity;

        float[,] inverse = new float[3, 3];

        for (int c = 0; c < 3; ++c)
        {
            for (int r = 0; r < 3; ++r)
            {
                inverse[c, r] = (a[(r + 1) % 3, (c + 1) % 3] * a[(r + 2) % 3, (c + 2) % 3]
                    - a[(r + 1) % 3, (c + 2) % 3] * a[(r + 2) % 3, (c + 1) % 3]) / determinant;
            }
        }

        Matrix result = new();

        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
                result[r * 4 + c] = inverse[r, c];
        }

        for (int c = 0; c < 3; ++c)
        {
            float total = 0.0f;

            for (int k = 0; k < 3; ++k)
                total += matrix[12 + k] * inverse[k, c];

            result[12 + c] = -total;
        }

        result[15] = 1.0f;

        return result;
    }

    public static Float3 Transform(Float3 point, Matrix matrix)
    {
        Float3 moved = new();

        for (int c = 0; c < 3; ++c)
            moved[c] = point[0] * matrix[c] + point[1] * matrix[4 + c] + point[2] * matrix[8 + c] + matrix[12 + c];

        return moved;
    }

    public static Float3 Rotate(Float3 vector, Matrix matrix)
    {
        Float3 turned = new();

        for (int c = 0; c < 3; ++c)
            turned[c] = vector[0] * matrix[c] + vector[1] * matrix[4 + c] + vector[2] * matrix[8 + c];

        return turned;
    }

    public static Matrix NormalMatrix(Matrix matrix)
    {
        Matrix inverse = Invert(matrix);
        Matrix normals = Matrix.Identity;

        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
                normals[r * 4 + c] = inverse[c * 4 + r];
        }

        return normals;
    }
}
