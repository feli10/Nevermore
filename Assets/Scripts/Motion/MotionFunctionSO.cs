using UnityEngine;

public abstract class MotionFunctionSO : ScriptableObject
{
    // Return position at local motion time t (seconds since the motion started).
    // Motion functions should treat t=0 as the motion start. Components using
    // these functions will pass a time relative to that start (e.g. spawn
    // time or the object's motion start time).
    public abstract Vector3 EvaluatePosition(float t);

    // Optional rotation evaluated at local motion time t
    public virtual Quaternion EvaluateRotation(float t) => Quaternion.identity;

    // By default allow negative time; override if a motion shouldn't be evaluated before t=0
    public virtual bool AllowNegativeTime => true;
}