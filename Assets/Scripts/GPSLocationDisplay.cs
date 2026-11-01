using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// This script shows location and turns the compass dial toward a destination.
public class GPSLocationDisplay : MonoBehaviour
{
    // Change these in the Inspector to choose a destination.
    public double destinationLatitude = 12.9750;
    public double destinationLongitude = 77.6050;

    // These sample coordinates are used while testing in the Unity Editor.
    public double testLatitude = 12.9716;
    public double testLongitude = 77.5946;

    private Text gpsText;
    private Transform compassDial;
    private bool gpsIsRunning;

    private IEnumerator Start()
    {
        // Find the text this script is attached to and the compass dial in the scene.
        gpsText = GetComponent<Text>();
        Transform compassPanel = null;

        if (transform.parent != null)
        {
            compassPanel = transform.parent.Find("CompassPanel");
        }

        if (compassPanel != null)
        {
            compassDial = compassPanel.Find("DialRing");
        }

        if (gpsText == null || compassDial == null)
        {
            Debug.LogError("This script needs a UI Text and a CompassPanel with a DialRing.");
            yield break;
        }

#if UNITY_EDITOR
        // In the Editor, spin a sample heading so the compass can be seen working.
        float testHeading = 0f;

        while (true)
        {
            testHeading = testHeading + 30f * Time.deltaTime;
            if (testHeading >= 360f) testHeading -= 360f;

            UpdateCompass(testLatitude, testLongitude, testHeading);
            yield return null;
        }
#else
        // On a phone, read the real GPS and compass sensors.
        if (!SystemInfo.supportsLocationService)
        {
            ShowUnavailable();
            yield break;
        }

        Input.compass.enabled = true;
        Input.location.Start(10f, 10f);
        gpsIsRunning = true;

        // Wait up to 30 seconds for GPS to start.
        int secondsWaited = 0;
        while (Input.location.status == LocationServiceStatus.Initializing && secondsWaited < 30)
        {
            yield return new WaitForSeconds(1f);
            secondsWaited++;
        }

        if (Input.location.status != LocationServiceStatus.Running)
        {
            StopSensors();
            ShowUnavailable();
            yield break;
        }

        // Keep updating the display while GPS is running.
        while (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo phoneLocation = Input.location.lastData;
            float phoneHeading = Input.compass.trueHeading;

            // Use magnetic heading if true heading is not available.
            if (phoneHeading < 0f) phoneHeading = Input.compass.magneticHeading;

            UpdateCompass(phoneLocation.latitude, phoneLocation.longitude, phoneHeading);
            yield return new WaitForSeconds(0.2f);
        }

        StopSensors();
        ShowUnavailable();
#endif
    }

    // Update the numbers and point the dial toward the destination.
    private void UpdateCompass(double latitude, double longitude, float heading)
    {
        float targetDirection = FindBearing(latitude, longitude,
            destinationLatitude, destinationLongitude);

        float turnToTarget = Mathf.DeltaAngle(heading, targetDirection);
        compassDial.localRotation = Quaternion.Euler(0f, 0f, -turnToTarget);

        gpsText.text = string.Format(
            "Latitude & Longitude: {0:F5}, {1:F5}\nRotation: {2:0}°",
            latitude, longitude, heading);
    }

    // Show this when the phone cannot provide a GPS location.
    private void ShowUnavailable()
    {
        gpsText.text = "Latitude & Longitude: unavailable\nRotation: unavailable";
    }

    // Work out the compass direction from the current location to the destination.
    private static float FindBearing(double fromLatitude, double fromLongitude,
        double toLatitude, double toLongitude)
    {
        double startLatitude = fromLatitude * Mathf.Deg2Rad;
        double endLatitude = toLatitude * Mathf.Deg2Rad;
        double longitudeDifference = (toLongitude - fromLongitude) * Mathf.Deg2Rad;

        double y = System.Math.Sin(longitudeDifference) * System.Math.Cos(endLatitude);
        double x = System.Math.Cos(startLatitude) * System.Math.Sin(endLatitude)
            - System.Math.Sin(startLatitude) * System.Math.Cos(endLatitude)
            * System.Math.Cos(longitudeDifference);

        double angle = System.Math.Atan2(y, x) * Mathf.Rad2Deg;
        return Mathf.Repeat((float)angle, 360f);
    }

    // Stop the phone sensors when this object is turned off.
    private void OnDisable()
    {
        StopSensors();
    }

    private void StopSensors()
    {
        if (gpsIsRunning)
        {
            Input.location.Stop();
            gpsIsRunning = false;
        }

        Input.compass.enabled = false;
    }
}
