namespace FbxToMua.Core.Formats.FbxText;

internal sealed class DoubleMatrix
{
    public double[] M { get; } = new double[16];

    public static DoubleMatrix Identity()
    {
        DoubleMatrix identity = new();

        for (int i = 0; i < 4; ++i)
            identity.M[i * 4 + i] = 1.0;

        return identity;
    }

    public static DoubleMatrix Multiply(DoubleMatrix a, DoubleMatrix b)
    {
        DoubleMatrix product = new();

        for (int r = 0; r < 4; ++r)
        {
            for (int c = 0; c < 4; ++c)
            {
                double sum = 0.0;

                for (int k = 0; k < 4; ++k)
                    sum += a.M[r * 4 + k] * b.M[k * 4 + c];

                product.M[r * 4 + c] = sum;
            }
        }

        return product;
    }

    public static DoubleMatrix Translation(double x, double y, double z)
    {
        DoubleMatrix translation = Identity();
        translation.M[12] = x;
        translation.M[13] = y;
        translation.M[14] = z;

        return translation;
    }

    public static DoubleMatrix Scaling(double x, double y, double z)
    {
        DoubleMatrix scaling = Identity();
        scaling.M[0] = x;
        scaling.M[5] = y;
        scaling.M[10] = z;

        return scaling;
    }

    public static DoubleMatrix Rotation(double rx, double ry, double rz, int order)
    {
        const double degree = Math.PI / 180.0;
        double sx = Math.Sin(rx * degree), cx = Math.Cos(rx * degree);
        double sy = Math.Sin(ry * degree), cy = Math.Cos(ry * degree);
        double sz = Math.Sin(rz * degree), cz = Math.Cos(rz * degree);

        DoubleMatrix mx = Identity();
        mx.M[5] = cx;
        mx.M[6] = sx;
        mx.M[9] = -sx;
        mx.M[10] = cx;

        DoubleMatrix my = Identity();
        my.M[0] = cy;
        my.M[2] = -sy;
        my.M[8] = sy;
        my.M[10] = cy;

        DoubleMatrix mz = Identity();
        mz.M[0] = cz;
        mz.M[1] = sz;
        mz.M[4] = -sz;
        mz.M[5] = cz;

        return order switch
        {
            1 => Multiply(Multiply(mx, mz), my),
            2 => Multiply(Multiply(my, mz), mx),
            3 => Multiply(Multiply(my, mx), mz),
            4 => Multiply(Multiply(mz, mx), my),
            5 => Multiply(Multiply(mz, my), mx),
            _ => Multiply(Multiply(mx, my), mz),
        };
    }

    public static DoubleMatrix TransposeRotation(DoubleMatrix m)
    {
        DoubleMatrix transposed = Identity();

        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
                transposed.M[r * 4 + c] = m.M[c * 4 + r];
        }

        return transposed;
    }

    public double[] Apply(double x, double y, double z, double w)
    {
        return
        [
            x * M[0] + y * M[4] + z * M[8] + w * M[12],
            x * M[1] + y * M[5] + z * M[9] + w * M[13],
            x * M[2] + y * M[6] + z * M[10] + w * M[14],
        ];
    }
}
