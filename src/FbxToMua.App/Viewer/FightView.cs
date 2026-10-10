using System.Windows;
using System.Windows.Media.Media3D;
using FbxToMua.Core.Export;

namespace FbxToMua.App.Viewer;

public sealed class FightView
{
    private const double NearPlane = 5.0;
    private const double OrbitFarPlane = 1000000.0;
    private const double DegreesPerPixel = 0.3;
    private const double ZoomPerNotch = 0.9;
    private const double WheelNotch = 120.0;
    private const double SteepestOrbit = 85.0;
    private const double ClosestOrbit = 20.0;
    private const double FarthestOrbit = 50000.0;
    private static readonly double HorizontalFov = 2.0 * Math.Atan(Math.Tan(Radians(BattleCamera.Fov) * 0.5) * BattleCamera.Aspect) * 180.0 / Math.PI;
    private static readonly Vector3D Up = new(0.0, 1.0, 0.0);

    private double _yaw;
    private double _pitch;
    private double _distance = BattleCamera.EyeDistance;

    public void ResetOrbit()
    {
        _yaw = 0.0;
        _pitch = 0.0;
        _distance = BattleCamera.EyeDistance;
    }

    public void Drag(Vector pixels)
    {
        _yaw += pixels.X * DegreesPerPixel;
        _pitch = Math.Clamp(_pitch + pixels.Y * DegreesPerPixel, -SteepestOrbit, SteepestOrbit);
    }

    public void Zoom(int wheel)
    {
        _distance = Math.Clamp(_distance * Math.Pow(ZoomPerNotch, wheel / WheelNotch), ClosestOrbit, FarthestOrbit);
    }

    public static void Game(PerspectiveCamera camera, double cameraX, int tiltDegrees)
    {
        double tilt = Radians(tiltDegrees);
        camera.Position = new Point3D(-cameraX, BattleCamera.EyeHeight, -BattleCamera.EyeDistance);
        camera.LookDirection = new Vector3D(0.0, -Math.Sin(tilt), Math.Cos(tilt));
        camera.UpDirection = new Vector3D(0.0, Math.Cos(tilt), Math.Sin(tilt));
        Lens(camera, BattleCamera.FarPlane);
    }

    public void Orbit(PerspectiveCamera camera, double cameraX)
    {
        double yaw = Radians(_yaw);
        double pitch = Radians(_pitch);
        Point3D target = new(-cameraX, BattleCamera.EyeHeight, 0.0);
        Vector3D back = new(Math.Sin(yaw) * Math.Cos(pitch), Math.Sin(pitch), -Math.Cos(yaw) * Math.Cos(pitch));
        camera.Position = target + back * _distance;
        camera.LookDirection = -back;
        camera.UpDirection = Up;
        Lens(camera, OrbitFarPlane);
    }

    private static void Lens(PerspectiveCamera camera, double far)
    {
        camera.FieldOfView = HorizontalFov;
        camera.NearPlaneDistance = NearPlane;
        camera.FarPlaneDistance = far;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180.0;
}
