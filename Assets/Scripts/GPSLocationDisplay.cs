using UnityEngine;
using UnityEngine.UI;

public class GPSLocationDisplay : MonoBehaviour
{
    public Text gpsText;
    public Transform compassDial;
    float smooth;

    void Start()
    {
        Input.compass.enabled = true;
        Input.location.Start();
    }

    void Update()
    {
        float heading = Input.compass.magneticHeading;
        smooth = Mathf.LerpAngle(smooth, heading, 10f * Time.deltaTime);
        compassDial.localRotation = Quaternion.Euler(0, 0, smooth);

        string place = "waiting for GPS";
        if (Input.location.status == LocationServiceStatus.Running)
            place = Input.location.lastData.latitude + ", " + Input.location.lastData.longitude;

        gpsText.text = "Latitude & Longitude: " + place + "\nRotation: " + (int)heading + "°";
    }
}
