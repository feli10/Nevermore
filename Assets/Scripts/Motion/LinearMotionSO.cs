using UnityEngine;

[CreateAssetMenu(menuName = "MotionFunctions/LinearUnit")]
public class LinearMotionSO : MotionFunctionSO
{
    // This returns a unit forward motion (z+) scaled by time.
    // The projectile instance will rotate/scale this to implement arbitrary direction & speed.
    public override Vector3 EvaluatePosition(float t)
    {
        return new Vector3(0f, 0f, t); // unit forward * t
    }

    public override Quaternion EvaluateRotation(float t)
    {
        return Quaternion.identity;
    }

    public override bool AllowNegativeTime => false;
}
