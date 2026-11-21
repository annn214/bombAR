#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public static class SetupVirtualButtons
{
    [MenuItem("Tools/Setup Virtual Buttons")]
    private static void Setup()
    {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            EditorUtility.DisplayDialog(
                "Buka SampleScene",
                "Keluar dari Prefab Mode dan buka SampleScene terlebih dahulu.",
                "OK");
            return;
        }

        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
        {
            EditorUtility.DisplayDialog(
                "Scene tidak valid",
                "Buka SampleScene terlebih dahulu.",
                "OK");
            return;
        }

        ARTrackedImageManager imageManager = null;
        ARCameraBackground cameraBackground = null;
        BombManager bombManager = null;
        VirtualButtonDetector detector = null;

        foreach (GameObject go in scene.GetRootGameObjects())
        {
            if (imageManager == null)
                imageManager = go.GetComponentInChildren<ARTrackedImageManager>(true);

            if (cameraBackground == null)
                cameraBackground = go.GetComponentInChildren<ARCameraBackground>(true);

            if (bombManager == null)
                bombManager = go.GetComponentInChildren<BombManager>(true);

            if (detector == null)
                detector = go.GetComponentInChildren<VirtualButtonDetector>(true);
        }

        string missing = "";

        if (imageManager == null)
            missing += "\n- AR Tracked Image Manager";

        if (cameraBackground == null)
            missing += "\n- AR Camera Background";

        if (bombManager == null)
            missing += "\n- BombManager";

        if (missing != "")
        {
            EditorUtility.DisplayDialog(
                "Komponen belum lengkap",
                "Komponen berikut tidak ditemukan di SampleScene:" +
                missing, "OK");
            return;
        }

        if (detector == null)
        {
            GameObject go = new GameObject("VirtualButtons");
            Undo.RegisterCreatedObjectUndo(go, "Create VirtualButtons");
            detector = Undo.AddComponent<VirtualButtonDetector>(go);
        }

        Undo.RecordObject(detector, "Setup VirtualButtonDetector");

        detector.trackedImageManager = imageManager;
        detector.cameraBackground = cameraBackground;
        detector.arCamera = cameraBackground.GetComponent<Camera>();
        detector.bombManager = bombManager;

        EditorUtility.SetDirty(detector);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = detector.gameObject;

        EditorUtility.DisplayDialog(
            "Setup selesai",
            "Referensi telah diisi. Periksa empat nama tombol pada Inspector, lalu simpan SampleScene dengan Ctrl+S.",
            "OK");
    }
}
#endif