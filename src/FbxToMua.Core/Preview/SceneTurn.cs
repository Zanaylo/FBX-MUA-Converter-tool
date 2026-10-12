using FbxToMua.Core.Geometry;

namespace FbxToMua.Core.Preview;

public static class SceneTurn
{
    private const double DegreesToRadians = Math.PI / 180.0;

    public static Matrix Of(int degrees)
    {
        if (degrees == 0)
            return Matrix.Identity;

        double angle = degrees * DegreesToRadians;
        float cosine = (float)Math.Cos(angle);
        float sine = (float)Math.Sin(angle);
        Matrix turn = Matrix.Identity;
        turn[5] = cosine;
        turn[6] = sine;
        turn[9] = -sine;
        turn[10] = cosine;

        return turn;
    }
}
