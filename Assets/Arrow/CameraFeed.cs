using UnityEngine;
using UnityEngine.UI;

// Shows the phone's rear camera behind the compass UI.
public class CameraFeed : MonoBehaviour
{
    public RawImage display;
    private WebCamTexture cameraTexture;

    private void Start()
    {
        if (display == null) display = GetComponent<RawImage>();

#if UNITY_EDITOR
        // Keep the desktop Game view available for testing without opening its camera.
        if (display != null) display.enabled = false;
#else
#if UNITY_ANDROID
        if (display == null) return;
        UnityEngine.Android.Permission.RequestUserPermission(
            UnityEngine.Android.Permission.Camera);
#endif
#endif
    }

    private void Update()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (cameraTexture == null &&
            UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Camera))
        {
            StartBackCamera();
        }

        if (cameraTexture != null && cameraTexture.didUpdateThisFrame)
        {
            FitToPortraitScreen();
        }
#endif
    }

    private void StartBackCamera()
    {
        WebCamDevice[] cameras = WebCamTexture.devices;
        for (int i = 0; i < cameras.Length; i++)
        {
            if (!cameras[i].isFrontFacing)
            {
                cameraTexture = new WebCamTexture(cameras[i].name);
                display.texture = cameraTexture;
                cameraTexture.Play();
                return;
            }
        }

        Debug.LogWarning("No rear-facing camera was found.");
    }

    private void FitToPortraitScreen()
    {
        int angle = cameraTexture.videoRotationAngle;
        bool turnSideways = angle == 90 || angle == 270;
        RectTransform image = display.rectTransform;
        RectTransform parent = image.parent as RectTransform;
        if (parent == null || cameraTexture.width <= 16 || cameraTexture.height <= 16) return;

        float screenWidth = parent.rect.width;
        float screenHeight = parent.rect.height;
        float boxWidth = turnSideways ? screenHeight : screenWidth;
        float boxHeight = turnSideways ? screenWidth : screenHeight;
        float cameraAspect = (float)cameraTexture.width / cameraTexture.height;

        // Enlarge the image to cover the screen without stretching it.
        if (cameraAspect > boxWidth / boxHeight)
            boxWidth = boxHeight * cameraAspect;
        else
            boxHeight = boxWidth / cameraAspect;

        image.anchorMin = image.anchorMax = new Vector2(0.5f, 0.5f);
        image.anchoredPosition = Vector2.zero;
        image.sizeDelta = new Vector2(boxWidth, boxHeight);
        image.localEulerAngles = new Vector3(0f, 0f, -angle);
    }

    private void OnDisable()
    {
        if (cameraTexture != null && cameraTexture.isPlaying)
            cameraTexture.Stop();
    }
}
