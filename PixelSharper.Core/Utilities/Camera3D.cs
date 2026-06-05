using System;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Hardware3D;

// Port of olc::utils::hw3d::Camera3D and its two derived cameras. Maintains projection + view
// matrices (feed GetViewMatrix()/GetProjectionMatrix().ToArray() to the engine's HW3D path) and
// can cast a screen-space ray into the world. Derived cameras add FPS and orbit controls.
public class Camera3D
{
    protected Vector3d VecPosition = new(0, 0, 0);
    protected Vector3d VecTarget = new(0, 0, 1);
    protected Matrix4x4 MatView = new();
    protected Matrix4x4 MatViewInv = new();
    protected Vector3d VecViewUp = new(0, 1, 0);
    protected Vector3d VecAxisUp = new(0, 1, 0);
    protected Vector3d VecAxisRight = new(1, 0, 0);
    protected Vector3d VecAxisForward = new(0, 0, 1);
    protected Vector3d VecViewForward;
    protected Vector3d VecViewRight;
    protected Matrix4x4 MatProjection = new();
    protected Matrix4x4 MatProjectionInv = new();
    protected Vector2d<float> ScreenSize;
    protected float FieldOfView = 3.14159f;
    protected float AspectRatio = 1.333333f;
    protected float NearPlane = 0.1f;
    protected float FarPlane = 1000.0f;

    public Camera3D()
    {
        VecPosition = new Vector3d(0, 0, 0);
        VecTarget = new Vector3d(0, 0, 1);
        RegenerateProjectionMatrix();
        RegenerateViewMatrix();
    }

    protected void RegenerateProjectionMatrix()
    {
        MatProjection = Matrix4x4.Projection(FieldOfView, AspectRatio, NearPlane, FarPlane);
        MatProjectionInv = MatProjection.Invert();
    }

    protected void RegenerateViewMatrix()
    {
        VecViewForward = -(VecTarget - VecPosition).Norm();
        VecViewUp = (VecAxisUp - VecAxisUp.Dot(VecViewForward) * VecViewForward).Norm();
        VecViewRight = VecViewUp.Cross(VecViewForward);
        MatViewInv = BuildPointAt(VecViewRight, VecViewUp, VecViewForward, VecPosition);
        MatView = MatViewInv.QuickInvert();
    }

    // Assemble the inverse-view ("point at") matrix from the camera basis vectors + position.
    protected static Matrix4x4 BuildPointAt(Vector3d right, Vector3d up, Vector3d forward, Vector3d pos)
    {
        var m = new Matrix4x4();
        m[0, 0] = right.X; m[0, 1] = right.Y; m[0, 2] = right.Z; m[0, 3] = 0f;
        m[1, 0] = up.X; m[1, 1] = up.Y; m[1, 2] = up.Z; m[1, 3] = 0f;
        m[2, 0] = forward.X; m[2, 1] = forward.Y; m[2, 2] = forward.Z; m[2, 3] = 0f;
        m[3, 0] = pos.X; m[3, 1] = pos.Y; m[3, 2] = pos.Z; m[3, 3] = 1f;
        return m;
    }

    public Matrix4x4 GetProjectionMatrix() => MatProjection;
    public Matrix4x4 GetViewMatrix() => MatView;
    public Vector3d GetViewUp() => VecViewUp;
    public Vector3d GetViewRight() => -VecViewRight;
    public Vector3d GetViewForward() => -VecViewForward;

    public void SetPosition(Vector3d position) => VecPosition = position;
    public void SetPosition(float x, float y, float z) => VecPosition = new Vector3d(x, y, z);
    public Vector3d GetPosition() => VecPosition;

    public void SetTarget(Vector3d target) => VecTarget = target;
    public void SetTarget(float x, float y, float z) => VecTarget = new Vector3d(x, y, z);
    public Vector3d GetTarget() => VecTarget;

    public virtual void Update() => RegenerateViewMatrix();

    public void SetFieldOfView(float theta) { FieldOfView = theta; RegenerateProjectionMatrix(); }

    public void SetScreenSize(Vector2d<int> size)
    {
        ScreenSize = new Vector2d<float>(size.X, size.Y);
        AspectRatio = (float)size.X / size.Y;
        RegenerateProjectionMatrix();
    }

    public void SetClippingPlanes(float near, float far) { NearPlane = near; FarPlane = far; RegenerateProjectionMatrix(); }

    // Casts a normalised world-space ray from a screen pixel (e.g. mouse-pick into the scene).
    public Vector3d ScreenRay(Vector2d<float> screenPos)
    {
        var rayParallel = new Vector3d(
            2f * screenPos.X / ScreenSize.X - 1f,
            1f - 2f * screenPos.Y / ScreenSize.Y,
            1f, 1f);
        var rayProjected = MatProjectionInv * rayParallel;
        rayProjected.W = 0f;
        var rayWorld = MatViewInv * rayProjected;
        rayWorld.W = 0f;
        return rayWorld.Norm();
    }
}

// A simple first-person camera: move along the view axes, turn via a heading angle.
public class Camera3DSimpleFps : Camera3D
{
    protected float Heading = MathF.PI * 0.5f;

    public void Forwards(float speed) => VecPosition += GetViewForward() * speed;
    public void Backwards(float speed) => VecPosition -= GetViewForward() * speed;
    public void Upwards(float speed) => VecPosition += GetViewUp() * speed;
    public void Downwards(float speed) => VecPosition -= GetViewUp() * speed;
    public void StrafeLeft(float speed) => VecPosition -= GetViewRight() * speed;
    public void StrafeRight(float speed) => VecPosition += GetViewRight() * speed;
    public void TurnLeft(float speed) { Heading += speed; WrapHeading(); }
    public void TurnRight(float speed) { Heading -= speed; WrapHeading(); }
    public void SetHeading(float angle) => Heading = angle;

    private void WrapHeading()
    {
        var twoPi = MathF.PI * 2f;
        if (Heading >= twoPi) Heading -= twoPi;
        if (Heading < 0) Heading += twoPi;
    }

    public override void Update()
    {
        SetTarget(VecPosition + new Vector3d(MathF.Cos(Heading), 0f, MathF.Sin(Heading)));
        base.Update();
    }
}

// An orbit camera: pan/zoom the focal point and spin around it via screen-space gestures.
public class Camera3DOrbit : Camera3D
{
    protected Vector3d FocalPoint = new(0, 0, 0);
    protected float CameraRadius = 10.0f;
    protected Vector2d<float> VSpin;

    public void Pan(Vector3d screenMoved)
    {
        VecTarget -= screenMoved * CameraRadius;
        VecPosition -= screenMoved * CameraRadius;
    }

    public void Zoom(float zoomDelta) => CameraRadius *= zoomDelta;

    public void Spin(Vector2d<float> screenMoved)
        => VSpin = new Vector2d<float>(
            2f * (screenMoved.X / ScreenSize.X) * AspectRatio,
            2f * (screenMoved.Y / ScreenSize.Y) * AspectRatio);

    public float GetDistance() => CameraRadius;

    public override void Update()
    {
        VecViewForward = -(VecTarget - VecPosition).Norm();
        VecViewUp = (VecAxisUp - VecAxisUp.Dot(VecViewForward) * VecViewForward).Norm();
        VecViewRight = VecViewUp.Cross(VecViewForward);

        // Turn the screen-space gesture into a rotation about an axis through the camera.
        var dirOfRotation = -MathF.Atan2(VSpin.Y, VSpin.X);
        var magOfRotation = MathF.Sqrt(VSpin.X * VSpin.X + VSpin.Y * VSpin.Y);
        var vRotation = VecViewUp * MathF.Sin(dirOfRotation) - VecViewRight * MathF.Cos(dirOfRotation);
        var vNewAxis = VecViewForward.Cross(vRotation);

        var s = MathF.Sin(magOfRotation);
        var c = MathF.Cos(magOfRotation);
        var dotFront = VecAxisUp.Dot(VecViewForward);
        var dotSide = VecAxisUp.Dot(vRotation);
        var ux = dotFront * c + dotSide * s;
        var uy = -dotFront * s + dotSide * c;
        var uz = VecAxisUp.Dot(vNewAxis);
        VecViewUp = ux * VecViewForward + uy * vRotation + uz * vNewAxis;

        vRotation *= -s;
        VecViewForward = VecViewForward * c + vRotation;

        // Offset back out to the requested distance from the target.
        VecViewForward *= CameraRadius;
        VecPosition = VecViewForward + VecTarget;

        // Re-derive the basis from the tweaked position.
        VecViewForward = -(VecTarget - VecPosition).Norm();
        VecViewUp = (VecAxisUp - VecAxisUp.Dot(VecViewForward) * VecViewForward).Norm();
        VecViewRight = VecViewUp.Cross(VecViewForward);

        MatViewInv = BuildPointAt(VecViewRight, VecViewUp, VecViewForward, VecPosition);
        MatView = MatViewInv.QuickInvert();
    }
}
