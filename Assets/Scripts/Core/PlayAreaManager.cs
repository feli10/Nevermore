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

    // Return the closest point inside the bounds (clamps)
    public Vector3 ClampPosition(Vector3 position)
    {
        return GetBounds().ClosestPoint(position);
    }

    // Wrap a world position so that coordinates outside the axis-aligned bounds
    // appear on the opposite side (toroidal / wrap-around behavior per axis).
    public Vector3 WrapPosition(Vector3 position)
    {
        Bounds b = GetBounds();
        Vector3 min = b.min;
        Vector3 size = b.size;

        Vector3 rel = position - min; // relative to min

        rel.x = Mathf.Repeat(rel.x, size.x);
        rel.y = Mathf.Repeat(rel.y, size.y);
        rel.z = Mathf.Repeat(rel.z, size.z);

        return min + rel;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);
    }
}
