using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Shows location and points the compass art toward a destination.
public class GPSLocationDisplay : MonoBehaviour
{
    private const double TestLatitude = 12.9716;
    private const double TestLongitude = 77.5946;
    private const double TestDestinationLatitude = 12.9750;
    private const double TestDestinationLongitude = 77.6050;

    private Text statusText;
    private Transform dialTransform;
    private bool locationStarted;
    private float simulatedHeading;

    private IEnumerator Start()
    {
        statusText = GetComponent<Text>();
        Transform canvas = transform.parent;
        Transform compass = canvas != null ? canvas.Find("CompassPanel") : null;
        Transform dial = compass != null ? compass.Find("DialRing") : null;
        dialTransform = dial;

        if (statusText == null || dialTransform == null)
        {
            Debug.LogError("Compass setup needs this Text and a CompassPanel/DialRing object.");
            yield break;
        }
        statusText.color = Color.white;

#if UNITY_EDITOR
        while (true)
        {
            simulatedHeading = Mathf.Repeat(simulatedHeading + 30f * Time.deltaTime, 360f);
            float bearing = Bearing(TestLatitude, TestLongitude, TestDestinationLatitude, TestDestinationLongitude);
            PointDial(bearing, simulatedHeading);
            statusText.text = string.Format(
                "Latitude & Longitude: {0:F5}, {1:F5}\nRotation: {2:0}°",
                TestLatitude, TestLongitude, simulatedHeading);
            yield return null;
        }
#else
        if (!SystemInfo.supportsLocationService)
        {
            statusText.text = "Latitude & Longitude: unavailable\nRotation: unavailable";
            yield break;
        }

        Input.compass.enabled = true;
        statusText.text = "Latitude & Longitude: waiting...\nRotation: waiting...";
        Input.location.Start(10f, 10f);
        locationStarted = true;

        int secondsLeft = 30;
        while (Input.location.status == LocationServiceStatus.Initializing && secondsLeft > 0)
        {
            statusText.text = "Latitude & Longitude: waiting...\nRotation: waiting...";
            yield return new WaitForSeconds(1f);
            secondsLeft--;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            Input.location.Stop();
            locationStarted = false;
            statusText.text = "Latitude & Longitude: unavailable\nRotation: unavailable";
            Debug.LogWarning("GPS did not start. Check device permission and Location setting.");
            yield break;
        }

        while (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo location = Input.location.lastData;
            float heading = Input.compass.trueHeading;
            if (heading < 0f)
                heading = Input.compass.magneticHeading;

            float bearing = Bearing(location.latitude, location.longitude,
                TestDestinationLatitude, TestDestinationLongitude);
            PointDial(bearing, heading);
            statusText.text = string.Format(
                "Latitude & Longitude: {0:F5}, {1:F5}\nRotation: {2:0}°",
                location.latitude, location.longitude, heading);
            yield return new WaitForSeconds(0.2f);
        }

        Input.location.Stop();
        locationStarted = false;
        statusText.text = "Latitude & Longitude: unavailable\nRotation: unavailable";
#endif
    }

    private void PointDial(float bearing, float heading)
    {
        // The compass needle is part of the dial picture, so rotate that picture toward the target.
        float angle = Mathf.DeltaAngle(heading, bearing);
        dialTransform.localRotation = Quaternion.Euler(0f, 0f, -angle);
    }

    private static float Bearing(double fromLatitude, double fromLongitude,
        double toLatitude, double toLongitude)
    {
        double fromLat = fromLatitude * Mathf.Deg2Rad;
        double toLat = toLatitude * Mathf.Deg2Rad;
        double deltaLongitude = (toLongitude - fromLongitude) * Mathf.Deg2Rad;
        double y = System.Math.Sin(deltaLongitude) * System.Math.Cos(toLat);
        double x = System.Math.Cos(fromLat) * System.Math.Sin(toLat)
            - System.Math.Sin(fromLat) * System.Math.Cos(toLat) * System.Math.Cos(deltaLongitude);
        return Mathf.Repeat((float)(System.Math.Atan2(y, x) * Mathf.Rad2Deg), 360f);
    }

    private void OnDisable()
    {
        if (locationStarted)
        {
            Input.location.Stop();
            locationStarted = false;
        }
        Input.compass.enabled = false;
    }
}
