using System;
using PixelSharper.Core.Types;

namespace PixelSharper.Core.Utilities.Hardware3D;

/// <summary>Port of olc::utils::hw3d::Camera3D — maintains projection and view matrices and casts screen-space rays into the world; derived cameras add FPS and orbit controls.</summary>
/// <remarks>
/// <para>Feed <c>GetViewMatrix().ToArray()</c> and <c>GetProjectionMatrix().ToArray()</c> to the engine's HW3D path (e.g. HW3D_Projection / HW3D_DrawObject).</para>
/// <para>Derived cameras add controls: <see cref="Camera3DSimpleFps"/> (first-person move/turn) and <see cref="Camera3DOrbit"/> (pan/zoom/spin about a focal point).</para>
/// </remarks>
/// <seealso cref="Camera3DSimpleFps"/>
/// <seealso cref="Camera3DOrbit"/>
// Port of olc::utils::hw3d::Camera3D and its two derived cameras. Maintains projection + view
// matrices (feed GetViewMatrix()/GetProjectionMatrix().ToArray() to the engine's HW3D path) and
// can cast a screen-space ray into the world. Derived cameras add FPS and orbit controls.
public class Camera3D
{
    /// <summary>World-space camera (eye) position.</summary>
    protected Vector3d VecPosition = new(0, 0, 0);
    /// <summary>World-space point the camera looks at.</summary>
    protected Vector3d VecTarget = new(0, 0, 1);
    /// <summary>View matrix (world to camera space).</summary>
    protected Matrix4x4 MatView = new();
    /// <summary>Inverse view ("point at") matrix; transforms camera space to world.</summary>
    protected Matrix4x4 MatViewInv = new();
    /// <summary>Camera up basis vector, re-orthonormalised each view rebuild.</summary>
    protected Vector3d VecViewUp = new(0, 1, 0);
    /// <summary>World up axis used as the up reference when deriving the basis.</summary>
    protected Vector3d VecAxisUp = new(0, 1, 0);
    /// <summary>World right axis reference.</summary>
    protected Vector3d VecAxisRight = new(1, 0, 0);
    /// <summary>World forward axis reference.</summary>
    protected Vector3d VecAxisForward = new(0, 0, 1);
    /// <summary>Camera forward basis vector (toward the target, negated).</summary>
    protected Vector3d VecViewForward;
    /// <summary>Camera right basis vector.</summary>
    protected Vector3d VecViewRight;
    /// <summary>Projection matrix (camera to clip space).</summary>
    protected Matrix4x4 MatProjection = new();
    /// <summary>Inverse projection matrix, used to unproject screen rays.</summary>
    protected Matrix4x4 MatProjectionInv = new();
    /// <summary>Viewport size in pixels, used for ray casting.</summary>
    protected Vector2d<float> ScreenSize;
    /// <summary>Vertical field of view in radians.</summary>
    protected float FieldOfView = 3.14159f;
    /// <summary>Viewport aspect ratio (width / height).</summary>
    protected float AspectRatio = 1.333333f;
    /// <summary>Near clipping plane distance.</summary>
    protected float NearPlane = 0.1f;
    /// <summary>Far clipping plane distance.</summary>
    protected float FarPlane = 1000.0f;

    /// <summary>Initialises the camera at the origin looking down +Z and builds initial matrices.</summary>
    public Camera3D()
    {
        VecPosition = new Vector3d(0, 0, 0);
        VecTarget = new Vector3d(0, 0, 1);
        RegenerateProjectionMatrix();
        RegenerateViewMatrix();
    }

    /// <summary>Rebuilds the projection and its inverse from the current FOV/aspect/clip planes.</summary>
    /// <remarks>The inverse (<see cref="MatProjectionInv"/>) is used to unproject screen rays in <see cref="ScreenRay"/>.</remarks>
    protected void RegenerateProjectionMatrix()
    {
        MatProjection = Matrix4x4.Projection(FieldOfView, AspectRatio, NearPlane, FarPlane);
        MatProjectionInv = MatProjection.Invert();
    }

    /// <summary>Re-derives the orthonormal camera basis from position/target and rebuilds the view matrices.</summary>
    /// <remarks>The up vector is re-orthonormalised against the forward axis; right is their cross product.</remarks>
    protected void RegenerateViewMatrix()
    {
        VecViewForward = -(VecTarget - VecPosition).Norm();
        VecViewUp = (VecAxisUp - VecAxisUp.Dot(VecViewForward) * VecViewForward).Norm();
        VecViewRight = VecViewUp.Cross(VecViewForward);
        MatViewInv = BuildPointAt(VecViewRight, VecViewUp, VecViewForward, VecPosition);
        MatView = MatViewInv.QuickInvert();
    }

    /// <summary>Assembles the inverse-view ("point at") matrix from the camera basis vectors and position.</summary>
    /// <param name="right">Camera right basis vector (row 0).</param>
    /// <param name="up">Camera up basis vector (row 1).</param>
    /// <param name="forward">Camera forward basis vector (row 2).</param>
    /// <param name="pos">Camera world position (row 3 translation).</param>
    /// <returns>The "point at" matrix transforming camera space to world space (the inverse view matrix).</returns>
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

    /// <summary>Returns the projection matrix.</summary>
    /// <returns>The camera-to-clip-space projection matrix; feed <c>.ToArray()</c> to the engine's HW3D_Projection.</returns>
    public Matrix4x4 GetProjectionMatrix() => MatProjection;
    /// <summary>Returns the view matrix.</summary>
    /// <returns>The world-to-camera-space view matrix; feed <c>.ToArray()</c> to the engine's HW3D path.</returns>
    public Matrix4x4 GetViewMatrix() => MatView;
    /// <summary>Returns the camera up basis vector.</summary>
    /// <returns>The orthonormal camera up axis.</returns>
    public Vector3d GetViewUp() => VecViewUp;
    /// <summary>Returns the camera right basis vector (sign-corrected for world space).</summary>
    /// <returns>The world-space camera right axis.</returns>
    public Vector3d GetViewRight() => -VecViewRight;
    /// <summary>Returns the camera forward basis vector (sign-corrected for world space).</summary>
    /// <returns>The world-space camera forward axis (toward the target).</returns>
    public Vector3d GetViewForward() => -VecViewForward;

    /// <summary>Sets the camera eye position.</summary>
    /// <param name="position">New world-space eye position.</param>
    public void SetPosition(Vector3d position) => VecPosition = position;
    /// <summary>Sets the camera eye position from components.</summary>
    /// <param name="x">World-space X coordinate.</param>
    /// <param name="y">World-space Y coordinate.</param>
    /// <param name="z">World-space Z coordinate.</param>
    public void SetPosition(float x, float y, float z) => VecPosition = new Vector3d(x, y, z);
    /// <summary>Returns the camera eye position.</summary>
    /// <returns>The world-space eye position.</returns>
    public Vector3d GetPosition() => VecPosition;

    /// <summary>Sets the look-at target.</summary>
    /// <param name="target">New world-space point the camera looks at.</param>
    public void SetTarget(Vector3d target) => VecTarget = target;
    /// <summary>Sets the look-at target from components.</summary>
    /// <param name="x">World-space X coordinate of the target.</param>
    /// <param name="y">World-space Y coordinate of the target.</param>
    /// <param name="z">World-space Z coordinate of the target.</param>
    public void SetTarget(float x, float y, float z) => VecTarget = new Vector3d(x, y, z);
    /// <summary>Returns the look-at target.</summary>
    /// <returns>The world-space look-at target.</returns>
    public Vector3d GetTarget() => VecTarget;

    /// <summary>Recomputes the view matrix from the current position/target.</summary>
    /// <remarks>Overridden by derived cameras to first update position/target from their controls.</remarks>
    public virtual void Update() => RegenerateViewMatrix();

    /// <summary>Sets the vertical field of view (radians) and rebuilds the projection.</summary>
    /// <param name="theta">Vertical field of view in radians.</param>
    public void SetFieldOfView(float theta) { FieldOfView = theta; RegenerateProjectionMatrix(); }

    /// <summary>Sets the viewport size, recomputes aspect ratio, and rebuilds the projection.</summary>
    /// <param name="size">Viewport size in pixels; aspect ratio is derived as width / height.</param>
    public void SetScreenSize(Vector2d<int> size)
    {
        ScreenSize = new Vector2d<float>(size.X, size.Y);
        AspectRatio = (float)size.X / size.Y;
        RegenerateProjectionMatrix();
    }

    /// <summary>Sets the near/far clipping plane distances and rebuilds the projection.</summary>
    /// <param name="near">Near clipping plane distance.</param>
    /// <param name="far">Far clipping plane distance.</param>
    public void SetClippingPlanes(float near, float far) { NearPlane = near; FarPlane = far; RegenerateProjectionMatrix(); }

    /// <summary>Casts a normalised world-space ray from a screen pixel (e.g. mouse-pick into the scene).</summary>
    /// <param name="screenPos">Pixel position on the viewport (origin top-left) to unproject.</param>
    /// <returns>A unit-length world-space ray direction from the camera through <paramref name="screenPos"/>.</returns>
    /// <remarks>Unprojects through the inverse projection then inverse view; requires <see cref="SetScreenSize"/> to have been called.</remarks>
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

/// <summary>A simple first-person camera: moves along the view axes and turns via a heading angle.</summary>
/// <remarks>Movement helpers translate the eye; <see cref="Update"/> re-aims the target one unit ahead along <c>Heading</c>.</remarks>
/// <seealso cref="Camera3D"/>
// A simple first-person camera: move along the view axes, turn via a heading angle.
public class Camera3DSimpleFps : Camera3D
{
    /// <summary>Yaw heading in radians (defaults to facing +Z).</summary>
    protected float Heading = MathF.PI * 0.5f;

    /// <summary>Moves the eye along the forward axis.</summary>
    /// <param name="speed">Distance to translate along the forward axis this step.</param>
    public void Forwards(float speed) => VecPosition += GetViewForward() * speed;
    /// <summary>Moves the eye backward along the forward axis.</summary>
    /// <param name="speed">Distance to translate against the forward axis this step.</param>
    public void Backwards(float speed) => VecPosition -= GetViewForward() * speed;
    /// <summary>Moves the eye up along the view-up axis.</summary>
    /// <param name="speed">Distance to translate along the up axis this step.</param>
    public void Upwards(float speed) => VecPosition += GetViewUp() * speed;
    /// <summary>Moves the eye down along the view-up axis.</summary>
    /// <param name="speed">Distance to translate against the up axis this step.</param>
    public void Downwards(float speed) => VecPosition -= GetViewUp() * speed;
    /// <summary>Strafes the eye left along the right axis.</summary>
    /// <param name="speed">Distance to translate against the right axis this step.</param>
    public void StrafeLeft(float speed) => VecPosition -= GetViewRight() * speed;
    /// <summary>Strafes the eye right along the right axis.</summary>
    /// <param name="speed">Distance to translate along the right axis this step.</param>
    public void StrafeRight(float speed) => VecPosition += GetViewRight() * speed;
    /// <summary>Turns the heading left (counter-clockwise) and wraps it.</summary>
    /// <param name="speed">Angle in radians to add to the heading.</param>
    public void TurnLeft(float speed) { Heading += speed; WrapHeading(); }
    /// <summary>Turns the heading right (clockwise) and wraps it.</summary>
    /// <param name="speed">Angle in radians to subtract from the heading.</param>
    public void TurnRight(float speed) { Heading -= speed; WrapHeading(); }
    /// <summary>Sets the heading angle directly.</summary>
    /// <param name="angle">New yaw heading in radians.</param>
    public void SetHeading(float angle) => Heading = angle;

    /// <summary>Keeps the heading within [0, 2pi).</summary>
    private void WrapHeading()
    {
        var twoPi = MathF.PI * 2f;
        if (Heading >= twoPi) Heading -= twoPi;
        if (Heading < 0) Heading += twoPi;
    }

    /// <summary>Aims the target one unit ahead along the heading, then rebuilds the view matrix.</summary>
    /// <remarks>Overrides <see cref="Camera3D.Update"/>; the heading drives yaw only (Y stays level).</remarks>
    public override void Update()
    {
        SetTarget(VecPosition + new Vector3d(MathF.Cos(Heading), 0f, MathF.Sin(Heading)));
        base.Update();
    }
}

/// <summary>An orbit camera: pans/zooms the focal point and spins around it via screen-space gestures.</summary>
/// <remarks>Accumulate gestures with <see cref="Pan"/>, <see cref="Zoom"/> and <see cref="Spin"/>, then call <see cref="Update"/> to apply them and rebuild the view.</remarks>
/// <seealso cref="Camera3D"/>
// An orbit camera: pan/zoom the focal point and spin around it via screen-space gestures.
public class Camera3DOrbit : Camera3D
{
    /// <summary>The point the camera orbits around.</summary>
    protected Vector3d FocalPoint = new(0, 0, 0);
    /// <summary>Orbit distance from the target.</summary>
    protected float CameraRadius = 10.0f;
    /// <summary>Accumulated screen-space spin gesture applied next Update.</summary>
    protected Vector2d<float> VSpin;

    /// <summary>Pans both target and eye by a screen-space delta scaled by the orbit radius.</summary>
    /// <param name="screenMoved">Screen-space movement delta; scaled by the current orbit radius.</param>
    public void Pan(Vector3d screenMoved)
    {
        VecTarget -= screenMoved * CameraRadius;
        VecPosition -= screenMoved * CameraRadius;
    }

    /// <summary>Scales the orbit radius (zoom in/out).</summary>
    /// <param name="zoomDelta">Multiplier applied to the orbit radius (greater than 1 zooms out, less than 1 zooms in).</param>
    public void Zoom(float zoomDelta) => CameraRadius *= zoomDelta;

    /// <summary>Records a screen-space spin gesture, scaled to normalised/aspect-corrected units.</summary>
    /// <param name="screenMoved">Screen-space drag delta in pixels; normalised by screen size and aspect ratio.</param>
    public void Spin(Vector2d<float> screenMoved)
        => VSpin = new Vector2d<float>(
            2f * (screenMoved.X / ScreenSize.X) * AspectRatio,
            2f * (screenMoved.Y / ScreenSize.Y) * AspectRatio);

    /// <summary>Returns the current orbit distance.</summary>
    /// <returns>The orbit radius (distance from the eye to the target).</returns>
    public float GetDistance() => CameraRadius;

    /// <summary>Applies the pending spin gesture, rotates the eye about the target, and rebuilds the view matrix.</summary>
    /// <remarks>Overrides <see cref="Camera3D.Update"/>; consumes the gesture recorded by <see cref="Spin"/> and re-derives the basis from the rotated position.</remarks>
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
