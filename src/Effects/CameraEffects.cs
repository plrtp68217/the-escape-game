using Godot;

namespace EscapeGame;

/// <summary>
/// Эффекты камеры при движении: покачивание при ходьбе, наклон при стрейфе,
/// толчок при приземлении. Применяются к локальной позиции и вращению камеры,
/// не мешая обзору мыши.
/// </summary>
public class CameraEffects
{
    private readonly Camera3D _camera;

    private readonly Vector3 _basePosition;
    private readonly Vector3 _baseRotation;

    private float _bobPhase;
    private float _landingImpact;
    private bool _wasOnFloor;
    private float _lastVerticalVelocity;

    public CameraEffects(Camera3D camera)
    {
        _camera = camera;
        _basePosition = camera.Position;
        _baseRotation = camera.Rotation;
    }

    public void Update(Vector3 velocity, bool isOnFloor, float speed, float delta)
    {
        Vector3 positionOffset = Vector3.Zero;
        Vector3 rotationOffset = Vector3.Zero;

        float horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();

        ApplyBobbing(ref positionOffset, horizontalSpeed, speed, isOnFloor, delta);
        ApplyStrafeTilt(ref rotationOffset);
        ApplyLandingImpact(ref positionOffset, isOnFloor, delta);

        _camera.Position = _basePosition + positionOffset;
        _camera.Rotation = _baseRotation + rotationOffset;

        _lastVerticalVelocity = velocity.Y;
        _wasOnFloor = isOnFloor;
    }

    private void ApplyBobbing(
        ref Vector3 positionOffset,
        float horizontalSpeed,
        float speed,
        bool isOnFloor,
        float delta
    )
    {
        if (!isOnFloor || horizontalSpeed <= 0.1f)
        {
            return;
        }

        _bobPhase += horizontalSpeed * delta * G.Camera.BobFrequency;
        float intensity = Mathf.Clamp(horizontalSpeed / speed, 0f, 1f);
        positionOffset.Y += Mathf.Sin(_bobPhase) * G.Camera.BobAmplitude * intensity;
    }

    private void ApplyStrafeTilt(ref Vector3 rotationOffset)
    {
        float strafe = Input.GetActionStrength("move_right") - Input.GetActionStrength("move_left");
        rotationOffset.Z -= strafe * G.Camera.StrafeTilt;
    }

    private void ApplyLandingImpact(ref Vector3 positionOffset, bool isOnFloor, float delta)
    {
        if (isOnFloor && !_wasOnFloor && _lastVerticalVelocity < -5f)
        {
            _landingImpact = Mathf.Abs(_lastVerticalVelocity) * G.Camera.LandingImpact;
        }

        _landingImpact = Mathf.MoveToward(_landingImpact, 0f, G.Camera.LandingRecovery * delta);
        positionOffset.Y -= _landingImpact;
    }
}
