public interface IHitStopable
{
    void StartHitStop(float duration);
    void EndHitStop();
    void SetAnimationSpeed(float speed);
    bool IsHitStopActive { get; }
}