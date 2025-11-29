using UnityEngine;

public class RealPositionGhost : MonoBehaviour
{
    public Transform targetRealTransform;  // always real position
    public bool followRotation = true;

    void Update()
    {
        if (!GhostToggleSystem.GhostsEnabled || targetRealTransform == null)
            return;

        transform.position = targetRealTransform.position;

        if (followRotation)
            transform.rotation = targetRealTransform.rotation;
    }
}
