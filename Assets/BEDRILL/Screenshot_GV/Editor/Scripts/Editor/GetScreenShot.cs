using ScreenShot.ResizeTools;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

public class GetScreenShot : EditorWindow
{
    [System.Serializable]
    public class ResolutionsClass
    {
        public string NameScreenShot;
        public Vector2 Resolution;
    }

    public List<ResolutionsClass> Resolutions = new List<ResolutionsClass>();

    private string nameFile = "Screenshot";
    private static string SaveFolder = "";

    private string Suffix;
    private string Preffix;

    private bool needAddToNameResolutions = true;
    private bool needAddToNameSuffix = false;
    private bool needAddToNamePreffix = false;

    private bool togglePngOrJPEG = true;
    private bool moreResolution = false;

    // 🔥 NEW
    private bool useTransparent = false;
    private bool useUIOnly = false;

    private static ResizeTool activeTool;
    private ReorderableList itemList = null;
    static SerializedObject serializedObject = null;

    [MenuItem("Tools/Screenshot_GV")]
    public static void ShowWindow()
    {
        if (string.IsNullOrEmpty(SaveFolder))
            SaveFolder = Application.dataPath + "/../Screenshots";

        var window = GetWindow<GetScreenShot>("Screenshot_GV");
        window.minSize = Vector2.one * 200;

        activeTool = new ResizeTool("", 10, 10);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        // FORMAT
        using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
        {
            EditorGUILayout.LabelField("Format:");
            togglePngOrJPEG = GUILayout.Toggle(togglePngOrJPEG, "PNG", EditorStyles.miniButtonLeft, GUILayout.Width(50));
            togglePngOrJPEG = !GUILayout.Toggle(!togglePngOrJPEG, "JPG", EditorStyles.miniButtonRight, GUILayout.Width(50));
        }

        // 🔥 MODE
        useTransparent = EditorGUILayout.Toggle("Transparent Background", useTransparent);
        useUIOnly = EditorGUILayout.Toggle("UI Only", useUIOnly);

        // MULTI RES
        using (new EditorGUILayout.HorizontalScope(EditorStyles.textArea))
        {
            EditorGUILayout.LabelField("More Resolutions:");
            moreResolution = GUILayout.Toggle(moreResolution, "On", EditorStyles.miniButtonLeft, GUILayout.Width(50));
            moreResolution = !GUILayout.Toggle(!moreResolution, "Off", EditorStyles.miniButtonRight, GUILayout.Width(50));
        }

        if (moreResolution)
        {
            if (itemList == null)
            {
                serializedObject = new SerializedObject(this);
                itemList = new ReorderableList(serializedObject, serializedObject.FindProperty("Resolutions"));
            }

            itemList.drawHeaderCallback = rect =>
                EditorGUI.LabelField(rect, "Resolutions");

            itemList.drawElementCallback = (rect, index, isActive, isFocus) =>
            {
                SerializedProperty element = itemList.serializedProperty.GetArrayElementAtIndex(index);

                EditorGUI.PropertyField(
                    new Rect(rect.x, rect.y, 150, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("NameScreenShot"),
                    GUIContent.none);

                EditorGUI.PropertyField(
                    new Rect(rect.x + 160, rect.y, 150, EditorGUIUtility.singleLineHeight),
                    element.FindPropertyRelative("Resolution"),
                    GUIContent.none);
            };

            itemList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }
        else
        {
            nameFile = EditorGUILayout.TextField("Name:", nameFile);
        }

        // PREFIX / SUFFIX
        needAddToNamePreffix = EditorGUILayout.Toggle("Use Prefix", needAddToNamePreffix);
        if (needAddToNamePreffix)
            Preffix = EditorGUILayout.TextField("Prefix:", Preffix);

        needAddToNameSuffix = EditorGUILayout.Toggle("Use Suffix", needAddToNameSuffix);
        if (needAddToNameSuffix)
            Suffix = EditorGUILayout.TextField("Suffix:", Suffix);

        needAddToNameResolutions = EditorGUILayout.Toggle("Add Resolution To Name", needAddToNameResolutions);

        // SAVE PATH
        SaveFolder = EditorGUILayout.TextField("Save To:", SaveFolder);
        if (GUILayout.Button("Browse"))
            SaveFolder = EditorUtility.SaveFolderPanel("Select Folder", SaveFolder, "");

        GUILayout.Space(10);

        // 🔥 BUTTONS
        if (GUILayout.Button("GET SCREENSHOT", GUILayout.Height(30)))
        {
            GetScreenshots(useTransparent, useUIOnly);
        }

        // ⭐ BUTTON MỚI
        if (GUILayout.Button("🔥 UI + TRANSPARENT (1 CLICK)", GUILayout.Height(30)))
        {
            GetScreenshots(true, true);
        }

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Save Path: " + SaveFolder);

        EditorGUILayout.EndVertical();
    }

    void GetScreenshots(bool transparent, bool uiOnly)
    {
        if (moreResolution)
            CoroutineForResizeGameWindow(transparent, uiOnly);
        else
        {
            string path = GetSaveFolderPath(Handles.GetMainGameViewSize(), nameFile);
            CaptureInEditor(path, transparent, uiOnly);
        }
    }

    void CoroutineForResizeGameWindow(bool transparent, bool uiOnly)
    {
        Vector2 currentRes = Handles.GetMainGameViewSize();
        int index = 0;

        EditorApplication.update += Step;

        void Step()
        {
            if (index >= Resolutions.Count)
            {
                activeTool.Resize((int)currentRes.x, (int)currentRes.y);
                EditorApplication.update -= Step;
                return;
            }

            var res = Resolutions[index];

            activeTool.Resize((int)res.Resolution.x, (int)res.Resolution.y);

            EditorApplication.delayCall += () =>
            {
                string path = GetSaveFolderPath(res.Resolution, res.NameScreenShot);
                CaptureInEditor(path, transparent, uiOnly);
                index++;
            };
        }
    }

    void CaptureInEditor(string path, bool transparent, bool uiOnly)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("No Camera found!");
            cam = FindFirstObjectByType<Camera>();
        }

        int width = (int)Handles.GetMainGameViewSize().x;
        int height = (int)Handles.GetMainGameViewSize().y;

        var oldClear = cam.clearFlags;
        var oldBg = cam.backgroundColor;
        var oldMask = cam.cullingMask;

        if (uiOnly)
        {
            int uiLayer = LayerMask.NameToLayer("UI");
            cam.cullingMask = 1 << uiLayer;
        }

        if (transparent)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0, 0, 0, 0);
        }

        RenderTexture rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        cam.targetTexture = rt;

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);

        cam.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        cam.targetTexture = null;
        RenderTexture.active = null;
        DestroyImmediate(rt);

        cam.clearFlags = oldClear;
        cam.backgroundColor = oldBg;
        cam.cullingMask = oldMask;

        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        Debug.Log("Saved: " + path);
    }

    string GetSaveFolderPath(Vector2 res, string name)
    {
        string folder = SaveFolder + "/" + (int)res.x + "x" + (int)res.y;

        if (!Directory.Exists(folder))
            Directory.CreateDirectory(folder);

        string file =
            (needAddToNamePreffix ? Preffix : "") +
            name +
            (needAddToNameSuffix ? Suffix : "") +
            (needAddToNameResolutions ? $"_{(int)res.x}x{(int)res.y}" : "") +
            ".png";

        return folder + "/" + file;
    }
}