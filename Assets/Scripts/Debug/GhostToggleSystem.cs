using UnityEngine;

public class GhostToggleSystem : MonoBehaviour
{
    public static bool GhostsEnabled { get; private set; } = false;

    void Update()
    {
        // Toggle with G
        if (Input.GetKeyDown(KeyCode.G))
        {
            GhostsEnabled = !GhostsEnabled;
            Debug.Log("Ghosts " + (GhostsEnabled ? "ENABLED" : "DISABLED"));
        }
    }
}
