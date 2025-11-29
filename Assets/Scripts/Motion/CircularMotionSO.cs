using UnityEngine;

[CreateAssetMenu(menuName = "MotionFunctions/Circular")]
public class CircularMotionSO : MotionFunctionSO
{
    public float radius = 3f;
    public float angularSpeed = 1f; // radians / second
    public float height = 0f;

    // Returns motion relative to a (0,0,0) center
    public override Vector3 EvaluatePosition(float t)
    {
        float angle = t * angularSpeed;
        return new Vector3(
            Mathf.Cos(angle) * radius,
            height,
            Mathf.Sin(angle) * radius
        );
    }
}
