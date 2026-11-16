using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class GpsDestinationArrow : MonoBehaviour
{
    [Header("Destination")]
    [Tooltip("Name shown in the UI while navigating")]
    public string destinationName = "Central Library";
    public double targetLatitude = 12.751270369862866d;
    public double targetLongitude = 80.19652878148224d;

    [Header("Arrow Tuning")]
    public float rotationSmoothness = 5f;
    public float compassUpdateInterval = 0.25f;
    [Tooltip("Extra yaw offset to compensate for device compass variance (degrees)")]
    public float arrowYawOffset;
    public bool showDebugLogs = true;

    [Header("Arrival")]
    [Tooltip("Distance in meters at which the destination counts as reached")]
    public float arrivalRadiusMeters = 15f;
    [Tooltip("Must move this far beyond arrival radius to un-arrive (prevents flickering)")]
    [Range(1.1f, 2f)]
    public float arrivalHysteresisMultiplier = 1.3f;

    [Header("Accuracy Gate")]
    [Tooltip("Arrow won't navigate when GPS accuracy is worse than this (meters)")]
    public float maxUsableAccuracy = 50f;

    [Header("UI Integration")]
    public Text destinationUiText;

    // Deadband filter to prevent jitter when standing still
    [Tooltip("Minimum bearing change required to update target rotation (degrees)")]
    public float headingDeadband = 1.5f;

    // Public properties for external visual components (e.g. ArrowPulse)
    public float LastAccuracy => lastAccuracy;
    public bool HasLocationFix => hasLocationFix;
    public bool HasArrived => hasArrived;
    public float LastDistance => lastDistance;

    // ── Private state ──────────────────────────────────────────────
    float nextSampleTime;
    float nextLogTime;
    float locationStartTime;
    float nextLocationRetryTime;
    float lastAccuracy;
    float lastHeading;
    float lastBearing;
    float lastDistance;
    double lastLatitude;
    double lastLongitude;
    Quaternion targetRotation;
    bool permissionDenied;
    bool warnedPoorAccuracy;
    bool hasLocationFix;
    bool hasArrived;
    string sensorState = "Starting";

#if UNITY_ANDROID && !UNITY_EDITOR
    PermissionCallbacks permissionCallbacks;
#endif

    void Start()
    {
        targetRotation = transform.localRotation;
        locationStartTime = Time.realtimeSinceStartup;

        if (!Input.compass.enabled) Input.compass.enabled = true;
        if (!Input.location.isEnabledByUser) Input.location.Start(5f, 1f);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation)
            || !Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
        {
            permissionCallbacks = new PermissionCallbacks();
            permissionCallbacks.PermissionDenied += _ => permissionDenied = true;
            string[] requests = { Permission.FineLocation, Permission.CoarseLocation };
            Permission.RequestUserPermissions(requests, permissionCallbacks);
            sensorState = "Waiting for foreground location permission";
        }
#endif
    }

    void Update()
    {
        if (Time.unscaledTime >= nextSampleTime)
        {
            nextSampleTime = Time.unscaledTime + Mathf.Max(0.05f, compassUpdateInterval);
            SampleSensors();
        }

        float smoothing = Mathf.Max(0.1f, rotationSmoothness);
        float blend = 1f - Mathf.Exp(-smoothing * Time.deltaTime);
        transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, blend);

        UpdateUiText();
        LogDebug();
    }

    // ── Sensor sampling ────────────────────────────────────────────
    void SampleSensors()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            sensorState = permissionDenied ? "Location permission denied" : "Waiting for location permission";
            return;
        }
#endif
        if (!Input.location.isEnabledByUser)
        {
            sensorState = "Device location is disabled";
            return;
        }

        LocationServiceStatus status = Input.location.status;
        if (status == LocationServiceStatus.Stopped || status == LocationServiceStatus.Failed)
        {
            if (Time.realtimeSinceStartup >= nextLocationRetryTime)
            {
                Input.location.Start(5f, 1f);
                locationStartTime = Time.realtimeSinceStartup;
                nextLocationRetryTime = locationStartTime + 5f;
            }
            sensorState = status == LocationServiceStatus.Failed ? "GPS failed; retrying" : "GPS starting";
            return;
        }
        if (status == LocationServiceStatus.Initializing)
        {
            sensorState = Time.realtimeSinceStartup - locationStartTime > 20f
                ? "GPS startup timed out"
                : "GPS initializing";
            return;
        }

        LocationInfo location = Input.location.lastData;
        if (location.timestamp <= 0d || double.IsNaN(location.latitude))
        {
            sensorState = "Waiting for GPS fix";
            return;
        }

        // Heading converts the world bearing into a direction relative to the phone.
        float heading = Input.compass.trueHeading >= 0 ? Input.compass.trueHeading : Input.compass.magneticHeading;
        if (float.IsNaN(heading) || float.IsInfinity(heading)) heading = -1f;

        lastLatitude = location.latitude;
        lastLongitude = location.longitude;
        lastAccuracy = location.horizontalAccuracy;
        hasLocationFix = true;
        lastHeading = heading;

        double bearing = Bearing(location.latitude, location.longitude, targetLatitude, targetLongitude);
        double distance = Distance(location.latitude, location.longitude, targetLatitude, targetLongitude);
        lastBearing = (float)bearing;
        lastDistance = (float)distance;

        // ── Phase 2: Arrival detection with hysteresis ──
        if (hasArrived)
        {
            // Only un-arrive if we move well past the radius
            if (lastDistance > arrivalRadiusMeters * arrivalHysteresisMultiplier)
                hasArrived = false;
        }
        else
        {
            if (lastDistance <= arrivalRadiusMeters)
                hasArrived = true;
        }

        // ── Phase 3: Accuracy gate — don't rotate arrow on garbage signal ──
        if (lastAccuracy > maxUsableAccuracy)
        {
            sensorState = "Waiting for better GPS signal\u2026";
            return;
        }

        // ── Phase 3: Dynamic deadband based on distance ──
        float dynamicDeadband;
        if (lastDistance > 200f)
            dynamicDeadband = 1.5f;      // Far — responsive
        else if (lastDistance > 50f)
            dynamicDeadband = 3f;         // Medium — slightly dampened
        else
            dynamicDeadband = 8f;         // Close — heavily dampened (GPS jitter)

        // Override with Inspector value if it's larger
        dynamicDeadband = Mathf.Max(dynamicDeadband, headingDeadband);

        // ── Phase 1: Phone-relative bearing for the on-screen direction arrow ──
        // Camera is top-down without mirroring: +Z is forward/up, +X is right.
        // Subtract heading so the arrow turns as the phone turns, always toward destination.
        float yaw = Mathf.Repeat((float)bearing - heading + arrowYawOffset, 360f);

        // Apply deadband to prevent jitter
        float currentYaw = targetRotation.eulerAngles.y;
        if (Mathf.Abs(Mathf.DeltaAngle(currentYaw, yaw)) > dynamicDeadband)
        {
            targetRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        sensorState = lastAccuracy <= 20f ? "GPS Ready" : "GPS Ready (Low Accuracy)";
    }

    // ── Phase 2 + 5: UI text with destination status ───────────────
    void UpdateUiText()
    {
        if (destinationUiText == null) return;

        string statusLine;
        string statusColor;

        if (!hasLocationFix)
        {
            // No GPS yet — show sensor state in red
            statusColor = "#FF6B6B";
            statusLine = sensorState;
        }
        else if (hasArrived)
        {
            statusColor = "#69DB7C";
            statusLine = "\u2705 Destination Reached!";
        }
        else
        {
            statusColor = "#FFD43B";
            statusLine = "\u23F3 Yet to reach destination";
        }

        // Build the full display text
        // When GPS fix exists, show sensorState below the status in subtle grey
        string sensorInfo = hasLocationFix ? string.Format("<color=#AAAAAA>{0}</color>\n", sensorState) : "";

        destinationUiText.text = string.Format(
            "<color=#74C0FC>\uD83D\uDCCD Navigating to: {0}</color>\n" +
            "<color={1}>{2}</color>\n" +
            "{3}\n" +
            "Lat: {4}, Lon: {5}\n" +
            "Accuracy: {6}m | Distance: {7:F0}m",
            destinationName,
            statusColor,
            statusLine,
            sensorInfo,
            hasLocationFix ? lastLatitude.ToString("F6") : "n/a",
            hasLocationFix ? lastLongitude.ToString("F6") : "n/a",
            hasLocationFix ? lastAccuracy.ToString("F0") : "n/a",
            lastDistance
        );
    }

    // ── Debug logging (console only) ───────────────────────────────
    void LogDebug()
    {
        if (!showDebugLogs || Time.unscaledTime < nextLogTime) return;
        nextLogTime = Time.unscaledTime + 1f;

        Debug.LogFormat(
            "GPS arrow: {0} | arrived={1} | lat={2}, lon={3}, acc={4}m, hdg={5:F1}, brg={6:F1}, dist={7:F0}m",
            sensorState,
            hasArrived,
            hasLocationFix ? lastLatitude.ToString("F6") : "n/a",
            hasLocationFix ? lastLongitude.ToString("F6") : "n/a",
            hasLocationFix ? lastAccuracy.ToString("F0") : "n/a",
            lastHeading, lastBearing, lastDistance
        );

        if (lastAccuracy > 50f && !warnedPoorAccuracy)
        {
            Debug.LogWarning("GPS accuracy is poor; direction may be unreliable.");
            warnedPoorAccuracy = true;
        }
    }

    // ── Haversine math (unchanged) ─────────────────────────────────
    static double Bearing(double lat1, double lon1, double lat2, double lon2)
    {
        lat1 *= Math.PI / 180d;
        lat2 *= Math.PI / 180d;
        double dLon = (lon2 - lon1) * Math.PI / 180d;
        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
        return (Math.Atan2(y, x) * 180d / Math.PI + 360d) % 360d;
    }

    static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        lat1 *= Math.PI / 180d;
        lat2 *= Math.PI / 180d;
        double dLat = lat2 - lat1;
        double dLon = (lon2 - lon1) * Math.PI / 180d;
        double a = Math.Sin(dLat / 2d) * Math.Sin(dLat / 2d) + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2d) * Math.Sin(dLon / 2d);
        a = Math.Max(0d, Math.Min(1d, a));
        return 6371000d * 2d * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1d - a));
    }
}
