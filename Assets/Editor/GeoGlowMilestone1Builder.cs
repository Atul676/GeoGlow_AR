using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GeoGlow
{
    public static class Milestone1Builder
    {
        private const string ScenePath = "Assets/Scenes/GeoGlowMain.unity";
        private const string CirclePath = "Assets/Sprites/Circle.png";
        private const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        [MenuItem("GeoGlow/Build Milestone 1")]
        public static void Build()
        {
            EnsureFolder("Assets/Scripts");
            EnsureFolder("Assets/Sprites");
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Materials");

            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (font == null)
                throw new InvalidOperationException("TMP Essentials are missing. Import them from Window > TextMeshPro > Import TMP Essential Resources.");

            string samplePath = "Assets/Scenes/SampleScene.unity";
            if (File.Exists(samplePath) && File.Exists(ScenePath))
                throw new InvalidOperationException("Both SampleScene.unity and GeoGlowMain.unity exist; refusing to overwrite either scene.");
            if (File.Exists(samplePath))
            {
                string error = AssetDatabase.RenameAsset(samplePath, "GeoGlowMain.unity");
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }

            if (!File.Exists(ScenePath))
                throw new FileNotFoundException("Could not find the scene to rename.", ScenePath);

            CreateCircleSprite();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            PlayerSettings.productName = "GeoGlow AR";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject mainCamera = null;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "Main Camera") mainCamera = root;
            if (mainCamera == null)
            {
                mainCamera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                mainCamera.tag = "MainCamera";
            }
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root != mainCamera) UnityEngine.Object.DestroyImmediate(root);

            Sprite circle = AssetDatabase.LoadAssetAtPath<Sprite>(CirclePath);
            if (circle == null) throw new InvalidOperationException("Circle.png did not import as a Sprite.");

            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject panelObject = new GameObject("CompassPanel", typeof(RectTransform));
            RectTransform panel = panelObject.GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = new Vector2(1, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = new Vector2(-252, 0);
            panel.sizeDelta = new Vector2(360, 360);

            AddImage(panel, "DialRing", circle, 360, new Color32(0, 200, 255, 255));
            AddImage(panel, "DialFace", circle, 330, new Color32(15, 25, 45, 230));
            AddImage(panel, "CenterDot", circle, 24, Color.white);
            AddLabel(panel, "Label_N", "N", new Vector2(0, 120), new Color32(255, 80, 80, 255), font);
            AddLabel(panel, "Label_E", "E", new Vector2(120, 0), Color.white, font);
            AddLabel(panel, "Label_S", "S", new Vector2(0, -120), Color.white, font);
            AddLabel(panel, "Label_W", "W", new Vector2(-120, 0), Color.white, font);

            GameObject eventObject = new GameObject("EventSystem", typeof(EventSystem));
            Type inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModule != null) eventObject.AddComponent(inputModule);
            else eventObject.AddComponent<StandaloneInputModule>();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("GeoGlow Milestone 1 scene built and saved.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void CreateCircleSprite()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.name = "Circle";
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int inside = 0;
                for (int sy = 0; sy < 4; sy++)
                for (int sx = 0; sx < 4; sx++)
                {
                    float dx = x + (sx + 0.5f) / 4f - size / 2f;
                    float dy = y + (sy + 0.5f) / 4f - size / 2f;
                    if (dx * dx + dy * dy <= 126f * 126f) inside++;
                }
                byte alpha = (byte)(inside * 255 / 16);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(CirclePath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(CirclePath, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(CirclePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void AddImage(RectTransform parent, string name, Sprite sprite, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = color;
        }

        private static void AddLabel(RectTransform parent, string name, string text, Vector2 position, Color color, TMP_FontAsset font)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(80, 80);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.font = font;
            label.fontSize = 56;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.color = color;
        }
    }
}
