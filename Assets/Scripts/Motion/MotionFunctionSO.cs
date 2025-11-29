using UnityEngine;

public abstract class MotionFunctionSO : ScriptableObject
{
    // Return position at absolute world time t
    public abstract Vector3 EvaluatePosition(float t);

    // Optional rotation
    public virtual Quaternion EvaluateRotation(float t) => Quaternion.identity;

    // By default allow negative time; override if a motion shouldn't be evaluated before t=0
    public virtual bool AllowNegativeTime => true;
}