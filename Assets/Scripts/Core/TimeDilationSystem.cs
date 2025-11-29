using UnityEngine;

public class TimeDilationSystem : MonoBehaviour
{
    public static TimeDilationSystem Instance { get; private set; }

    [Tooltip("Player subjective time multiplier. Use for UI/feel. Does NOT allow future sight.")]
    public float playerTimeScale = 1f;

    [Tooltip("In-game speed of light (units per second). No object should exceed this.")]
    public float speedOfLight = 10f;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
    }

    public float WorldToPlayerTime(float worldTime) => worldTime * playerTimeScale;
    public float C => speedOfLight;
}
