using UnityEngine;
using UnityEngine.UI;

public class GPSLocationDisplay : MonoBehaviour
{
    public Text gpsText;
    public Transform compassDial;
    [Range(1f, 25f)] public float smoothingSpeed = 10f;
    
    // Define 'smoothHeading' here so it exists in context
    float smoothHeading;

    void Start()
    {
        Input.compass.enabled = true;
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("Location service is not enabled on this device.");
        }
        else if (Input.location.status == LocationServiceStatus.Stopped)
        {
            Input.location.Start(5f, 1f);
        }
    }

    void Update()
    {
        // Smooth compass rotation (using -smoothHeading to fix East/West reversal)
        float heading = Input.compass.trueHeading >= 0 ? Input.compass.trueHeading : Input.compass.magneticHeading;
        smoothHeading = Mathf.LerpAngle(smoothHeading, heading, smoothingSpeed * Time.deltaTime);
        
        if (compassDial != null)
        {
            compassDial.localRotation = Quaternion.Euler(0, 0, smoothHeading);
        }

        // Location text display
        string place = "Waiting for GPS...";
        if (Input.location.status == LocationServiceStatus.Running)
        {
            LocationInfo location = Input.location.lastData;
            place = $"{location.latitude:F6}, {location.longitude:F6} (±{location.horizontalAccuracy:F0}m)";
        }
        else
        {
            place = $"GPS Status: {Input.location.status}";
        }

        if (gpsText != null)
        {
            gpsText.text = $"Location: {place}\nHeading: {(int)heading}°";
        }
    }
}