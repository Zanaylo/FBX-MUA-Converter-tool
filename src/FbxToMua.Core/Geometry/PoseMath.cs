namespace FbxToMua.Core.Geometry;

public static class PoseMath
{
    private const float Singular = 1e-12f;

    public static bool TrySplit(Matrix matrix, out Pose pose)
    {
        pose = new Pose();
        float[,] rows = new float[3, 3];

        for (int r = 0; r < 3; ++r)
        {
            float length = 0.0f;

            for (int c = 0; c < 3; ++c)
                length += matrix[r * 4 + c] * matrix[r * 4 + c];

            length = MathF.Sqrt(length);

            if (length < Singular)
                return false;

            pose.Scale[r] = length;

            for (int c = 0; c < 3; ++c)
                rows[r, c] = matrix[r * 4 + c] / length;
        }

        float determinant = rows[0, 0] * (rows[1, 1] * rows[2, 2] - rows[1, 2] * rows[2, 1])
            - rows[0, 1] * (rows[1, 0] * rows[2, 2] - rows[1, 2] * rows[2, 0])
            + rows[0, 2] * (rows[1, 0] * rows[2, 1] - rows[1, 1] * rows[2, 0]);

        if (determinant < 0.0f)
        {
            pose.Scale[0] = -pose.Scale[0];

            for (int c = 0; c < 3; ++c)
                rows[0, c] = -rows[0, c];
        }

        for (int k = 0; k < 3; ++k)
            pose.Translation[k] = matrix[12 + k];

        pose.Turn = Quaternion(rows);
        pose.Rotation = Angles(rows);

        return true;
    }

    public static Pose Split(Matrix matrix)
    {
        TrySplit(matrix, out Pose pose);

        return pose;
    }

    public static Matrix Compose(Pose pose)
    {
        float x = pose.Turn[0];
        float y = pose.Turn[1];
        float z = pose.Turn[2];
        float w = pose.Turn[3];

        float[,] rows =
        {
            { 1.0f - 2.0f * (y * y + z * z), 2.0f * (x * y + z * w), 2.0f * (x * z - y * w) },
            { 2.0f * (x * y - z * w), 1.0f - 2.0f * (x * x + z * z), 2.0f * (y * z + x * w) },
            { 2.0f * (x * z + y * w), 2.0f * (y * z - x * w), 1.0f - 2.0f * (x * x + y * y) },
        };

        Matrix matrix = new();

        for (int r = 0; r < 3; ++r)
        {
            for (int c = 0; c < 3; ++c)
                matrix[r * 4 + c] = pose.Scale[r] * rows[r, c];
        }

        for (int k = 0; k < 3; ++k)
            matrix[12 + k] = pose.Translation[k];

        matrix[15] = 1.0f;

        return matrix;
    }

    public static Pose Turned(Pose pose)
    {
        pose.Turn = FromAngles(pose.Rotation);

        return pose;
    }

    private static Float4 Quaternion(float[,] rows)
    {
        float trace = rows[0, 0] + rows[1, 1] + rows[2, 2];

        if (trace > 0.0f)
        {
            float s = MathF.Sqrt(trace + 1.0f) * 2.0f;
            return new Float4((rows[1, 2] - rows[2, 1]) / s, (rows[2, 0] - rows[0, 2]) / s,
                (rows[0, 1] - rows[1, 0]) / s, 0.25f * s);
        }

        if (rows[0, 0] > rows[1, 1] && rows[0, 0] > rows[2, 2])
        {
            float s = MathF.Sqrt(1.0f + rows[0, 0] - rows[1, 1] - rows[2, 2]) * 2.0f;
            return new Float4(0.25f * s, (rows[1, 0] + rows[0, 1]) / s, (rows[2, 0] + rows[0, 2]) / s,
                (rows[1, 2] - rows[2, 1]) / s);
        }

        if (rows[1, 1] > rows[2, 2])
        {
            float s = MathF.Sqrt(1.0f + rows[1, 1] - rows[0, 0] - rows[2, 2]) * 2.0f;
            return new Float4((rows[1, 0] + rows[0, 1]) / s, 0.25f * s, (rows[2, 1] + rows[1, 2]) / s,
                (rows[2, 0] - rows[0, 2]) / s);
        }

        float last = MathF.Sqrt(1.0f + rows[2, 2] - rows[0, 0] - rows[1, 1]) * 2.0f;
        return new Float4((rows[2, 0] + rows[0, 2]) / last, (rows[2, 1] + rows[1, 2]) / last, 0.25f * last,
            (rows[0, 1] - rows[1, 0]) / last);
    }

    private static Float3 Angles(float[,] rows)
    {
        float sine = Math.Clamp(rows[1, 2], -1.0f, 1.0f);

        return new Float3(MathF.Asin(sine), MathF.Atan2(-rows[0, 2], rows[2, 2]), MathF.Atan2(-rows[1, 0], rows[1, 1]));
    }

    private static Float4 FromAngles(Float3 angles)
    {
        float hx = angles[0] * 0.5f;
        float hy = angles[1] * 0.5f;
        float hz = angles[2] * 0.5f;

        Float4 x = new(MathF.Sin(hx), 0.0f, 0.0f, MathF.Cos(hx));
        Float4 y = new(0.0f, MathF.Sin(hy), 0.0f, MathF.Cos(hy));
        Float4 z = new(0.0f, 0.0f, MathF.Sin(hz), MathF.Cos(hz));

        return Product(Product(y, x), z);
    }

    private static Float4 Product(Float4 a, Float4 b)
    {
        return new Float4(
            a[3] * b[0] + a[0] * b[3] + a[2] * b[1] - a[1] * b[2],
            a[3] * b[1] + a[1] * b[3] + a[0] * b[2] - a[2] * b[0],
            a[3] * b[2] + a[2] * b[3] + a[1] * b[0] - a[0] * b[1],
            a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2]);
    }
}
