using UnityEngine;
using UnityEngine.UI;

// Points the arrow from the phone's location toward a fixed destination.
public class NavArrow : MonoBehaviour
{
    public double targetLat = 12.7531;
    public double targetLon = 80.1965;

    // These values let us test the arrow in the Unity Editor.
    public float testLat = 12.9716f;
    public float testLon = 77.5946f;
    public float testHeading = 0f;

    public Text locationText;
    public Text rotationText;

    private bool permissionRequested;
    private bool sensorsStarted;

    private void Start()
    {
        if (Application.isEditor) return;

#if UNITY_ANDROID
        UnityEngine.Android.Permission.RequestUserPermission(
            UnityEngine.Android.Permission.FineLocation);
        permissionRequested = true;
#endif
    }

    private void Update()
    {
        if (Application.isEditor)
        {
            UpdateArrow(testLat, testLon, testHeading);
            return;
        }

#if UNITY_ANDROID
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.FineLocation))
        {
            if (!permissionRequested)
            {
                UnityEngine.Android.Permission.RequestUserPermission(
                    UnityEngine.Android.Permission.FineLocation);
                permissionRequested = true;
            }
            return;
        }

        try
        {
            if (!sensorsStarted)
            {
                Input.location.Start();
                Input.compass.enabled = true;
                sensorsStarted = true;
            }

            if (Input.location.status == LocationServiceStatus.Running)
            {
                LocationInfo location = Input.location.lastData;
                UpdateArrow(location.latitude, location.longitude,
                    Input.compass.trueHeading);
            }
        }
        catch (System.Exception error)
        {
            Debug.LogError("GPS/compass input failed: " + error.Message);
        }
#endif
    }

    private void UpdateArrow(double latitude, double longitude, float heading)
    {
        float bearing = FindBearing(latitude, longitude, targetLat, targetLon);
        transform.localRotation = Quaternion.Euler(0f, bearing - heading, 0f);

        if (locationText == null) return;

        if (rotationText != null)
        {
            locationText.text = string.Format(
                "Latitude & Longitude: {0:F5}, {1:F5}", latitude, longitude);
            rotationText.text = string.Format("Rotation: {0:0}°", heading);
        }
        else
        {
            locationText.text = string.Format(
                "Latitude & Longitude: {0:F5}, {1:F5}\nRotation: {2:0}°",
                latitude, longitude, heading);
        }
    }

    private static float FindBearing(double fromLat, double fromLon,
        double toLat, double toLon)
    {
        double startLat = fromLat * Mathf.Deg2Rad;
        double endLat = toLat * Mathf.Deg2Rad;
        double longitudeDifference = (toLon - fromLon) * Mathf.Deg2Rad;

        double y = System.Math.Sin(longitudeDifference) * System.Math.Cos(endLat);
        double x = System.Math.Cos(startLat) * System.Math.Sin(endLat)
            - System.Math.Sin(startLat) * System.Math.Cos(endLat)
            * System.Math.Cos(longitudeDifference);

        double degrees = System.Math.Atan2(y, x) * Mathf.Rad2Deg;
        return Mathf.Repeat((float)degrees, 360f);
    }

    private void OnDisable()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (sensorsStarted)
        {
            Input.location.Stop();
            Input.compass.enabled = false;
            sensorsStarted = false;
        }
#endif
    }
}
