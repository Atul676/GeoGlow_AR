using UnityEngine;
using UnityEngine.Android;

public class NavArrow : MonoBehaviour
{
    const float goalLat = 12.7531f, goalLon = 80.1965f;
    float smooth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create() { new GameObject("NavArrow").AddComponent<NavArrow>(); }

    void Start()
    {
        Transform pivot = new GameObject("ArrowPivot").transform;
        pivot.SetParent(Camera.main.transform, false);
        pivot.localPosition = new Vector3(0, -0.45f, 2.2f);
        pivot.localRotation = Quaternion.Euler(40, 0, 0);
        transform.SetParent(pivot, false);

        Cube(new Vector3(0, 0, -0.1f), 0, new Vector3(0.14f, 0.14f, 0.6f));
        Cube(new Vector3(-0.129f, 0, 0.047f), 40, new Vector3(0.14f, 0.14f, 0.4f));
        Cube(new Vector3(0.129f, 0, 0.047f), -40, new Vector3(0.14f, 0.14f, 0.4f));

        Permission.RequestUserPermission(Permission.FineLocation);
        Input.compass.enabled = true;
    }

    void Cube(Vector3 pos, float rotY, Vector3 scale)
    {
        GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cube);
        c.transform.SetParent(transform, false);
        c.transform.localPosition = pos;
        c.transform.localRotation = Quaternion.Euler(0, rotY, 0);
        c.transform.localScale = scale;
        Destroy(c.GetComponent<Collider>());
        c.GetComponent<Renderer>().material.color = new Color32(30, 144, 255, 255);
    }

    void Update()
    {
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation)) return;
        if (Input.location.status == LocationServiceStatus.Stopped) Input.location.Start();
        if (Input.location.status != LocationServiceStatus.Running) return;

        LocationInfo p = Input.location.lastData;
        float bearing = Mathf.Atan2((goalLon - p.longitude) * Mathf.Cos(p.latitude * Mathf.Deg2Rad), goalLat - p.latitude) * Mathf.Rad2Deg;
        smooth = Mathf.LerpAngle(smooth, bearing - Input.compass.magneticHeading, 10f * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0, smooth, 0);
    }
}