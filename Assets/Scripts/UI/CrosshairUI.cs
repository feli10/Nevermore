


using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Simple crosshair UI that centers on the screen and optionally changes color based on targeting.
/// Attach this script to a RectTransform (e.g., an Image) on a Canvas.
/// </summary>
public class CrosshairUI : MonoBehaviour
{
    [Header("Crosshair Appearance")]
    [SerializeField] private Color defaultColor = Color.white;
    [SerializeField] private Color targetColor = Color.red; // color when aiming at an enemy
    [SerializeField] private Image crosshairImage;

    [Header("Raycast Targeting")]
    [SerializeField] private float raycastDistance = 1000f;
    [SerializeField] private LayerMask targetLayers; // layers that count as targetable enemies
    [SerializeField] private string targetTag = "Enemy"; // fallback: check tag

    private RectTransform rectTransform;
    private Canvas canvas;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();

        if (crosshairImage == null)
            crosshairImage = GetComponent<Image>();

        if (crosshairImage == null)
            Debug.LogWarning("CrosshairUI: No Image component found. Attach this script to an Image or assign one in inspector.", gameObject);
    }

    void Update()
    {
        // Center the crosshair at screen center
        CenterCrosshair();

        // Check if we're aiming at a target and update color
        UpdateTargetingColor();
    }

    void CenterCrosshair()
    {
        if (rectTransform == null) return;

        // Position crosshair at screen center
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            rectTransform.anchoredPosition = Vector2.zero; // center of screen
        }
    }

    void UpdateTargetingColor()
    {
        if (crosshairImage == null) return;

        // Raycast from screen center forward
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 rayOrigin = cam.transform.position;
        Vector3 rayDirection = cam.transform.forward;

        // Check for hits in the target layers
        bool isTargeting = false;

        if (targetLayers != 0)
        {
            if (Physics.Raycast(rayOrigin, rayDirection, raycastDistance, targetLayers))
            {
                isTargeting = true;
            }
        }

        // Fallback: check for targets by tag if no layer hit
        if (!isTargeting && !string.IsNullOrEmpty(targetTag))
        {
            RaycastHit hit;
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, raycastDistance))
            {
                if (hit.collider.CompareTag(targetTag) || hit.collider.GetComponentInParent<MotionFunctionComponent>() != null)
                {
                    isTargeting = true;
                }
            }
        }

        // Update crosshair color
        crosshairImage.color = isTargeting ? targetColor : defaultColor;
    }
}

