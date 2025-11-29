using UnityEngine;

public class PlayAreaManager : MonoBehaviour
{
    public static PlayAreaManager Instance { get; private set; }

    public Vector3 center = Vector3.zero;
    public Vector3 size = new Vector3(200f, 50f, 200f); // default roomy area

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(this);
        Instance = this;
    }

    public Bounds GetBounds()
    {
        return new Bounds(center, size);
    }

    // Utility: Check if point is outside area
    public bool IsOutside(Vector3 position)
    {
        return !GetBounds().Contains(position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
}
