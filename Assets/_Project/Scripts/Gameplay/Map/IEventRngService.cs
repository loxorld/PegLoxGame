public interface IEventRngService
{
    float Roll01();
}

public sealed class UnityEventRngService : IEventRngService
{
    public float Roll01()
    {
        return UnityEngine.Random.value;
    }
}