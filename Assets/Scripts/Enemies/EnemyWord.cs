using UnityEngine;
using TMPro;

/// <summary>
/// Enemy that is represented by a word. Each letter is one hit of health.
/// When hit, one letter is removed and the displayed text is updated.
/// When no letters remain, the enemy destroys itself.
/// </summary>
public class EnemyWord : MonoBehaviour
{
    [Tooltip("The word for this enemy. Its length is the health.")]
    public string word = "Nevermore";

    [Tooltip("TextMeshPro (3D) component used to render the word.")]
    public TMP_Text textMesh;

    [Tooltip("Optional VFX prefab to spawn when a letter is removed.")]
    public GameObject letterVFX;

    [Tooltip("Optional VFX prefab for death.")]
    public GameObject deathVFX;

    [Header("Facing")]
    [Tooltip("If true, the text will rotate to face the camera (Y-axis only).")]
    public bool faceCamera = true;

    private Transform camT;

    void Start()
    {
        if (textMesh == null)
            textMesh = GetComponentInChildren<TMP_Text>();

        camT = Camera.main?.transform;
        UpdateVisual();
    }

    void LateUpdate()
    {
        if (!faceCamera || textMesh == null || camT == null) return;

        Vector3 dir = camT.position - textMesh.transform.position;
        dir.y = 0f; // lock pitch so text stays upright

        if (dir.sqrMagnitude > 0.0001f)
            textMesh.transform.rotation = Quaternion.LookRotation(-dir); // flip direction so text faces camera
    }

    // Call when the enemy is hit by a projectile.
    // Returns true if the enemy was destroyed by this hit.
    public bool ApplyHit()
    {
        if (string.IsNullOrEmpty(word))
            return true;

        // Remove one letter — here we remove the last letter.
        // You could instead remove the first or a random index.
        int removeIndex = word.Length - 1;

        // Optional: spawn a VFX at the enemy position to indicate a letter removal
        if (letterVFX != null)
        {
            Instantiate(letterVFX, transform.position, Quaternion.identity);
        }

        // Remove the letter
        word = word.Remove(removeIndex, 1);

        UpdateVisual();

        if (word.Length == 0)
        {
            Die();
            return true;
        }

        return false;
    }

    void UpdateVisual()
    {
        if (textMesh != null)
            textMesh.text = word;
    }

    void Die()
    {
        if (deathVFX != null)
            Instantiate(deathVFX, transform.position, Quaternion.identity);

        // Destroy the entire enemy (MotionFunctionComponent.OnDestroy will clean up ghosts)
        Destroy(gameObject);
    }
}
