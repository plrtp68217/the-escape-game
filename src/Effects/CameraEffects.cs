using Godot;

namespace EscapeGame.Effects;

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

    // Тряска (trauma): накапливается при уроне, затухает со временем.
    private float _trauma;
    // Толчок (punch): мгновенное смещение/поворот, плавно возвращается к нулю.
    private Vector3 _punchPosition;
    private Vector3 _punchRotation;
    private readonly RandomNumberGenerator _rng = new();

    public CameraEffects(Camera3D camera)
    {
        _camera = camera;
        _basePosition = camera.Position;
        _baseRotation = camera.Rotation;
        _rng.Randomize();
    }

    // Добавить тряску (0..1). Вызывается при получении урона.
    public void AddTrauma(float amount)
    {
        _trauma = Mathf.Clamp(_trauma + amount, 0f, 1f);
    }

    // Резкий толчок камеры (например, отдача удара топором). Затухает сам.
    public void Punch(Vector3 positionOffset, Vector3 rotationOffset)
    {
        _punchPosition = positionOffset;
        _punchRotation = rotationOffset;
    }

    public void Update(Vector3 velocity, bool isOnFloor, float speed, float delta)
    {
        Vector3 positionOffset = Vector3.Zero;
        Vector3 rotationOffset = Vector3.Zero;

        float horizontalSpeed = new Vector2(velocity.X, velocity.Z).Length();

        ApplyBobbing(ref positionOffset, horizontalSpeed, speed, isOnFloor, delta);
        ApplyStrafeTilt(ref rotationOffset);
        ApplyLandingImpact(ref positionOffset, isOnFloor, delta);
        ApplyShake(ref positionOffset, ref rotationOffset, delta);
        ApplyPunch(ref positionOffset, ref rotationOffset, delta);

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

    // Тряска: сила растёт квадратично от травмы, направление — случайное.
    // Так резкие толчки заметны, а «хвост» быстро успокаивается.
    private void ApplyShake(ref Vector3 positionOffset, ref Vector3 rotationOffset, float delta)
    {
        if (_trauma > 0f)
        {
            float shake = _trauma * _trauma;

            positionOffset.X += _rng.RandfRange(-1f, 1f) * G.Camera.ShakeMaxOffset * shake;
            positionOffset.Y += _rng.RandfRange(-1f, 1f) * G.Camera.ShakeMaxOffset * shake;
            rotationOffset.Z += _rng.RandfRange(-1f, 1f) * G.Camera.ShakeMaxRotation * shake;
            rotationOffset.X += _rng.RandfRange(-1f, 1f) * G.Camera.ShakeMaxRotation * shake;

            _trauma = Mathf.MoveToward(_trauma, 0f, G.Camera.ShakeDecay * delta);
        }
    }

    private void ApplyPunch(ref Vector3 positionOffset, ref Vector3 rotationOffset, float delta)
    {
        positionOffset += _punchPosition;
        rotationOffset += _punchRotation;

        _punchPosition = _punchPosition.MoveToward(Vector3.Zero, G.Camera.PunchPositionRecovery * delta);
        _punchRotation = _punchRotation.MoveToward(Vector3.Zero, G.Camera.PunchRotationRecovery * delta);
    }
}
