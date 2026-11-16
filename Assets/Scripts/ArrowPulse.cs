using UnityEngine;

/// <summary>
/// Gently pulses the arrow scale when GPS accuracy is good (<= 15m),
/// and dims the visual when signal is poor (> 50m) or not yet acquired.
/// Placed on ArrowVisual (child of GPS_Arrow_ProBuilder).
/// </summary>
public class ArrowPulse : MonoBehaviour
{
    [Header("Pulse Settings (Good Signal <= 15m)")]
    [Tooltip("Maximum scale expansion during pulse")]
    public float pulseAmount = 0.08f;
    [Tooltip("Speed of the pulse animation")]
    public float pulseSpeed = 1.2f;

    [Header("Signal Thresholds (Meters)")]
    public float goodSignalThreshold = 15f;
    public float poorSignalThreshold = 50f;

    GpsDestinationArrow gpsArrow;
    MeshRenderer meshRenderer;
    Material arrowMaterial;
    Color originalColor;
    Color dimmedColor;
    Vector3 baseScale;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    void Start()
    {
        baseScale = transform.localScale;
        gpsArrow = GetComponentInParent<GpsDestinationArrow>();
        meshRenderer = GetComponentInChildren<MeshRenderer>();

        if (meshRenderer != null)
        {
            arrowMaterial = meshRenderer.material;
            if (arrowMaterial.HasProperty(BaseColorId))
                originalColor = arrowMaterial.GetColor(BaseColorId);
            else if (arrowMaterial.HasProperty(ColorId))
                originalColor = arrowMaterial.GetColor(ColorId);
            else
                originalColor = arrowMaterial.color;

            // Dimmed color: desaturated grey with lower intensity
            dimmedColor = new Color(0.35f, 0.4f, 0.45f, 1f);
        }
    }

    void Update()
    {
        bool hasFix = gpsArrow != null && gpsArrow.HasLocationFix;
        float accuracy = gpsArrow != null ? gpsArrow.LastAccuracy : 999f;

        if (hasFix && accuracy <= goodSignalThreshold)
        {
            // Good signal: gentle pulse bobbing
            float pulse = 1f + Mathf.PingPong(Time.unscaledTime * pulseSpeed, pulseAmount);
            transform.localScale = baseScale * pulse;
            SetColor(Color.Lerp(GetCurrentColor(), originalColor, Time.deltaTime * 5f));
        }
        else if (hasFix && accuracy <= poorSignalThreshold)
        {
            // Medium signal: static, normal color
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 5f);
            SetColor(Color.Lerp(GetCurrentColor(), originalColor, Time.deltaTime * 5f));
        }
        else
        {
            // Poor signal / no fix: return to base scale and dim slightly
            transform.localScale = Vector3.Lerp(transform.localScale, baseScale, Time.deltaTime * 5f);
            SetColor(Color.Lerp(GetCurrentColor(), dimmedColor, Time.deltaTime * 3f));
        }
    }

    Color GetCurrentColor()
    {
        if (arrowMaterial == null) return originalColor;
        if (arrowMaterial.HasProperty(BaseColorId))
            return arrowMaterial.GetColor(BaseColorId);
        return arrowMaterial.color;
    }

    void SetColor(Color c)
    {
        if (arrowMaterial == null) return;
        if (arrowMaterial.HasProperty(BaseColorId))
            arrowMaterial.SetColor(BaseColorId, c);
        else if (arrowMaterial.HasProperty(ColorId))
            arrowMaterial.SetColor(ColorId, c);
        else
            arrowMaterial.color = c;
    }

    void OnDestroy()
    {
        // Restore color on cleanup if material instance exists
        if (arrowMaterial != null)
        {
            SetColor(originalColor);
        }
    }
}
