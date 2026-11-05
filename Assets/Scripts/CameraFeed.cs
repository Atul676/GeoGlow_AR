using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

public class CameraFeed : MonoBehaviour
{
    WebCamTexture cam;
    RawImage img;

    void Start()
    {
        img = GetComponent<RawImage>();
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            Permission.RequestUserPermission(Permission.Camera);
    }

    void Update()
    {
        if (cam == null)
        {
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera)) return;
            foreach (WebCamDevice d in WebCamTexture.devices)
                if (!d.isFrontFacing) cam = new WebCamTexture(d.name);
            if (cam == null) return;
            img.texture = cam;
            cam.Play();
        }

        if (cam.width < 100) return;

        RectTransform rt = img.rectTransform;
        RectTransform canvas = (RectTransform)rt.parent;
        bool turned = cam.videoRotationAngle % 180 != 0;
        float w = turned ? cam.height : cam.width;
        float h = turned ? cam.width : cam.height;
        float s = Mathf.Max(canvas.rect.width / w, canvas.rect.height / h);

        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(cam.width * s, cam.height * s);
        rt.localEulerAngles = new Vector3(0, 0, -cam.videoRotationAngle);
        img.uvRect = cam.videoVerticallyMirrored ? new Rect(0, 1, 1, -1) : new Rect(0, 0, 1, 1);
    }
}