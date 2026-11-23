using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class VirtualButtonDetector : MonoBehaviour
{
    [Serializable]
    public class ButtonDef
    {
        public string name = "Tombol";

        [Header("Posisi alternatif relatif marker")]
        public Vector2 center = new Vector2(0.5f, 0.5f);
        public Vector2 size = new Vector2(0.2f, 0.2f);

        [Header("Nama objek tombol 3D")]
        public string childName = "";

        [Header("Area deteksi (meter)")]
        public Vector2 sizeMeters = new Vector2(0.05f, 0.05f);

        public UnityEvent onPressed;
        public UnityEvent onReleased;

        [NonSerialized] public Transform cachedChild;
        [NonSerialized] public Transform cachedRoot;
        [NonSerialized] public Color baseline;
        [NonSerialized] public float diff;
        [NonSerialized] public bool pressed;
        [NonSerialized] public int overCount;
        [NonSerialized] public int underCount;
        [NonSerialized] public float lastPressTime = -99f;
        [NonSerialized] public float nextLookupTime;
        [NonSerialized] public bool warnedMissingChild;
    }

    [Header("Referensi AR Foundation")]
    public ARTrackedImageManager trackedImageManager;
    public ARCameraBackground cameraBackground;
    public Camera arCamera;

    [Header("Game")]
    public BombManager bombManager;

    [Header("Tombol Kabel")]
    public ButtonDef[] buttons =
    {
        new ButtonDef
        {
            name = "Merah",
            childName = "Tombol_Merah"
        },
        new ButtonDef
        {
            name = "Biru",
            childName = "Tombol_Biru"
        },
        new ButtonDef
        {
            name = "Hijau",
            childName = "Tombol_Hijau"
        },
        new ButtonDef
        {
            name = "Kuning",
            childName = "Tombol_Kuning"
        }
    };

    [Header("Sensitivitas")]
    [Range(0.02f, 0.5f)]
    public float pressThreshold = 0.075f;

    [Range(0.01f, 0.5f)]
    public float releaseThreshold = 0.035f;

    [Min(1)]
    public int framesToPress = 5;

    [Min(1)]
    public int framesToRelease = 2;

    [Min(0.03f)]
    public float checkInterval = 0.06f;

    [Tooltip("Jeda setelah tombol apa pun ditekan, sebelum tombol lain boleh terdeteksi.")]
    [Min(0f)]
    public float cooldown = 1.5f;

    [Min(3)]
    public int calibrationSamples = 10;

    [Tooltip("Aktifkan jika sampel vertikal gambar kamera terbalik.")]
    public bool flipY = false;

    [Header("Toleransi tracking")]
    [Tooltip("Berapa detik marker yang tertutup jari tetap dianggap ada.")]
    [Min(0f)]
    public float lostTrackingGrace = 1.5f;

    [Header("Debug")]
    public bool showDebug = true;

    private const int TextureWidth = 320;
    private const int Grid = 5;

    private RenderTexture renderTexture;
    private Texture2D cameraTexture;

    private Color[] calibrationSums;
    private Color[] currentColors;

    private float elapsed;
    private int calibrationCount;
    private bool calibrated;

    private string status = "Arahkan kamera ke marker";

    // Marker terakhir yang terlihat jelas
    private ARTrackedImage lastTrackedImage;
    private float lastTrackedTime = -99f;

    // BARU: cooldown untuk semua tombol dan info debug
    private float lastAnyPressTime = -99f;
    private string lastPressedName = "-";

    private void Start()
    {
        if (trackedImageManager == null ||
            cameraBackground == null ||
            arCamera == null ||
            bombManager == null)
        {
            status = "ERROR: Referensi Inspector belum lengkap";

            Debug.LogError(
                "VirtualButtonDetector: Hubungkan ARTrackedImageManager, " +
                "ARCameraBackground, AR Camera, dan BombManager.",
                this
            );

            enabled = false;
            return;
        }

        if (buttons == null || buttons.Length == 0)
        {
            Debug.LogError(
                "VirtualButtonDetector: Daftar tombol kosong.",
                this
            );

            enabled = false;
            return;
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null)
            {
                Debug.LogError(
                    "VirtualButtonDetector: Data tombol ke-" + i + " kosong.",
                    this
                );

                enabled = false;
                return;
            }
        }

        int height = Mathf.Max(
            16,
            Mathf.RoundToInt(
                TextureWidth * (float)Screen.height / Screen.width
            )
        );

        renderTexture = new RenderTexture(
            TextureWidth,
            height,
            0,
            RenderTextureFormat.ARGB32
        );

        renderTexture.Create();

        cameraTexture = new Texture2D(
            TextureWidth,
            height,
            TextureFormat.RGB24,
            false
        );

        calibrationSums = new Color[buttons.Length];
        currentColors = new Color[buttons.Length];

        for (int i = 0; i < buttons.Length; i++)
            calibrationSums[i] = Color.black;

        status = "Arahkan kamera ke marker";

        Debug.Log(
            "VirtualButtonDetector siap.",
            this
        );
    }

    private void OnDestroy()
    {
        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
        }

        if (cameraTexture != null)
            Destroy(cameraTexture);
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (elapsed < checkInterval)
            return;

        elapsed = 0f;

        ARTrackedImage trackedImage = GetTrackedImage();

        if (trackedImage == null)
        {
            ResetCalibration();
            status = "Arahkan kamera ke marker" + InfoTracking();
            return;
        }

        if (cameraBackground == null ||
            cameraBackground.material == null ||
            arCamera == null)
        {
            status = "Referensi kamera tidak lengkap";
            return;
        }

        bool trackingKuat =
            trackedImage.trackingState == TrackingState.Tracking;

        // Kalibrasi hanya saat marker terlihat jelas.
        if (!calibrated && !trackingKuat)
        {
            status = "Tahan kamera, marker belum stabil" + InfoTracking();
            return;
        }

        CaptureCamera();

        // Ambil sampel pada posisi layar masing-masing tombol.
        for (int i = 0; i < buttons.Length; i++)
        {
            if (!SampleButton(
                trackedImage,
                buttons[i],
                out currentColors[i]))
            {
                status =
                    "Tombol di luar kamera atau posisi tidak ditemukan";
                return;
            }
        }

        // Kalibrasi gambar awal tanpa jari menutupi marker.
        if (!calibrated)
        {
            for (int i = 0; i < buttons.Length; i++)
                calibrationSums[i] += currentColors[i];

            calibrationCount++;

            status =
                "Kalibrasi: jangan sentuh (" +
                calibrationCount + "/" + calibrationSamples + ")";

            if (calibrationCount >= calibrationSamples)
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].baseline =
                        calibrationSums[i] / calibrationCount;
                }

                calibrated = true;
                status = "Kalibrasi selesai. Tutupi satu tombol.";

                Debug.Log(
                    "Kalibrasi Virtual Button selesai.",
                    this
                );
            }

            return;
        }

        int changedButtons = 0;
        bool anyPressed = false;

        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].diff = ColorDiff(
                currentColors[i],
                buttons[i].baseline
            );

            if (buttons[i].diff > pressThreshold)
                changedButtons++;

            if (buttons[i].pressed)
                anyPressed = true;
        }

        // Perubahan pada semua tombol sering menandakan perubahan cahaya.
        if (buttons.Length > 1 &&
            changedButtons == buttons.Length &&
            !anyPressed)
        {
            ResetCalibration();
            status = "Cahaya berubah. Kalibrasi ulang.";
            return;
        }

        status = trackingKuat
            ? "Tutupi satu area tombol dengan jari"
            : "Tutupi satu area tombol dengan jari (tracking lemah)";

        // Periksa pelepasan tombol terlebih dahulu.
        for (int i = 0; i < buttons.Length; i++)
        {
            ButtonDef button = buttons[i];

            if (!button.pressed)
                continue;

            if (button.diff < releaseThreshold)
                button.underCount++;
            else
                button.underCount = 0;

            if (button.underCount >= framesToRelease)
            {
                button.pressed = false;
                button.underCount = 0;
                button.overCount = 0;

                button.onReleased?.Invoke();

                Debug.Log(
                    "Virtual Button dilepas: " + button.name,
                    this
                );
            }
        }

        // Cari perubahan warna terkuat. Hanya satu tombol yang
        // boleh menjadi kandidat tekanan dalam satu pemeriksaan.
        int bestIndex = -1;
        float strongestDiff = pressThreshold;

        for (int i = 0; i < buttons.Length; i++)
        {
            ButtonDef button = buttons[i];

            if (button.pressed)
                continue;

            if (button.diff > strongestDiff)
            {
                strongestDiff = button.diff;
                bestIndex = i;
            }
        }

        // Tambah hitungan hanya untuk kandidat terkuat.
        for (int i = 0; i < buttons.Length; i++)
        {
            ButtonDef button = buttons[i];

            if (button.pressed)
                continue;

            if (i == bestIndex)
            {
                button.overCount++;
            }
            else
            {
                button.overCount = 0;

                // Baseline hanya menyesuaikan bila area benar-benar tenang.
                if (button.diff < releaseThreshold)
                {
                    button.baseline = Color.Lerp(
                        button.baseline,
                        currentColors[i],
                        0.02f
                    );
                }
            }
        }

        if (bestIndex < 0)
            return;

        ButtonDef selected = buttons[bestIndex];

        if (selected.overCount < framesToPress)
            return;

        // DIUBAH: cooldown berlaku untuk semua tombol sekaligus.
        if (Time.time - lastAnyPressTime < cooldown)
            return;

        selected.pressed = true;
        selected.underCount = 0;
        selected.overCount = 0;
        selected.lastPressTime = Time.time;

        lastAnyPressTime = Time.time;
        lastPressedName = selected.name;

        status = "Kabel terdeteksi: " + selected.name;

        Debug.Log(
            "VIRTUAL BUTTON DITEKAN: " + selected.name +
            " | Diff: " + selected.diff.ToString("F3"),
            this
        );

        if (bombManager != null)
            bombManager.SelectCable(selected.name);

        selected.onPressed?.Invoke();
    }

    // Beri toleransi saat marker tertutup jari.
    private ARTrackedImage GetTrackedImage()
    {
        if (trackedImageManager == null)
            return null;

        foreach (ARTrackedImage trackedImage
                 in trackedImageManager.trackables)
        {
            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                lastTrackedImage = trackedImage;
                lastTrackedTime = Time.time;
                return trackedImage;
            }
        }

        // Saat jari menutupi marker, tracking sering melemah sebentar.
        // Pakai posisi marker terakhir selama masa toleransi.
        if (lastTrackedImage != null &&
            Time.time - lastTrackedTime <= lostTrackingGrace)
        {
            return lastTrackedImage;
        }

        lastTrackedImage = null;
        return null;
    }

    // BARU: menampilkan status tracking tiap marker di layar.
    private string InfoTracking()
    {
        string info = "";

        if (trackedImageManager == null)
            return info;

        foreach (ARTrackedImage img in trackedImageManager.trackables)
        {
            info += "\nMarker " + img.referenceImage.name +
                    ": " + img.trackingState;
        }

        return info;
    }

    private void ResetCalibration()
    {
        calibrated = false;
        calibrationCount = 0;

        if (calibrationSums != null)
        {
            for (int i = 0; i < calibrationSums.Length; i++)
                calibrationSums[i] = Color.black;
        }

        if (buttons == null)
            return;

        foreach (ButtonDef button in buttons)
        {
            button.pressed = false;
            button.overCount = 0;
            button.underCount = 0;
        }
    }

    private void CaptureCamera()
    {
        Graphics.Blit(
            null,
            renderTexture,
            cameraBackground.material
        );

        RenderTexture previous = RenderTexture.active;

        RenderTexture.active = renderTexture;

        cameraTexture.ReadPixels(
            new Rect(
                0,
                0,
                renderTexture.width,
                renderTexture.height
            ),
            0,
            0
        );

        cameraTexture.Apply(false);

        RenderTexture.active = previous;
    }

    private bool SampleButton(
        ARTrackedImage trackedImage,
        ButtonDef button,
        out Color average)
    {
        average = Color.black;

        if (trackedImage == null || arCamera == null)
            return false;

        Vector3 centerWorld;
        Vector3 rightWorld;
        Vector3 upWorld;
        float widthMeters;
        float heightMeters;

        // Pada AR Foundation, marker berada di bidang XZ
        // dan sumbu Y (up) adalah arah tegak lurus kertas.
        Transform marker = trackedImage.transform;

        if (!string.IsNullOrEmpty(button.childName))
        {
            Transform child = ResolveButtonTransform(
                trackedImage,
                button
            );

            if (child == null)
            {
                status = "Objek tombol tidak ditemukan: " +
                         button.childName;
                return false;
            }

            // Proyeksikan posisi tombol ke permukaan kertas,
            // supaya yang diperiksa adalah area kertas tepat di bawah tombol.
            Vector3 normal = marker.up;
            Vector3 p = child.position;

            centerWorld =
                p - normal * Vector3.Dot(p - marker.position, normal);

            // Pakai arah permukaan marker, bukan arah model tombol.
            rightWorld = marker.right;
            upWorld = marker.forward;

            widthMeters = button.sizeMeters.x;
            heightMeters = button.sizeMeters.y;
        }
        else
        {
            // Cadangan untuk tombol dengan posisi relatif marker.
            Vector2 imageSize = trackedImage.size;

            if (imageSize.x <= 0f || imageSize.y <= 0f)
                return false;

            Vector3 localCenter = new Vector3(
                (button.center.x - 0.5f) * imageSize.x,
                0f,
                (0.5f - button.center.y) * imageSize.y
            );

            centerWorld = marker.TransformPoint(localCenter);

            rightWorld = marker.right;
            upWorld = marker.forward;

            widthMeters = button.size.x * imageSize.x;
            heightMeters = button.size.y * imageSize.y;
        }

        Color sum = Color.black;
        int samples = 0;

        for (int gx = 0; gx < Grid; gx++)
        {
            for (int gy = 0; gy < Grid; gy++)
            {
                float offsetX =
                    ((gx + 0.5f) / Grid - 0.5f) *
                    widthMeters * 0.8f;

                float offsetY =
                    ((gy + 0.5f) / Grid - 0.5f) *
                    heightMeters * 0.8f;

                Vector3 sampleWorld =
                    centerWorld +
                    rightWorld * offsetX +
                    upWorld * offsetY;

                Vector3 screenPoint =
                    arCamera.WorldToScreenPoint(sampleWorld);

                if (screenPoint.z <= 0f)
                    continue;

                float nx = screenPoint.x / Screen.width;
                float ny = screenPoint.y / Screen.height;

                if (flipY)
                    ny = 1f - ny;

                if (nx < 0f || nx > 1f ||
                    ny < 0f || ny > 1f)
                {
                    continue;
                }

                int tx = Mathf.Clamp(
                    Mathf.FloorToInt(nx * cameraTexture.width),
                    0,
                    cameraTexture.width - 1
                );

                int ty = Mathf.Clamp(
                    Mathf.FloorToInt(ny * cameraTexture.height),
                    0,
                    cameraTexture.height - 1
                );

                sum += cameraTexture.GetPixel(tx, ty);
                samples++;
            }
        }

        if (samples == 0)
            return false;

        average = sum / samples;
        return true;
    }

    private Transform ResolveButtonTransform(
        ARTrackedImage trackedImage,
        ButtonDef button)
    {
        // Jika marker berubah, cari kembali objek untuk marker baru.
        if (button.cachedRoot != trackedImage.transform)
        {
            button.cachedRoot = trackedImage.transform;
            button.cachedChild = FindDeep(
                trackedImage.transform,
                button.childName
            );

            button.nextLookupTime = 0f;
        }

        // Bila belum ditemukan, coba ulang secara berkala.
        if (button.cachedChild == null &&
            Time.time >= button.nextLookupTime)
        {
            button.cachedChild = FindDeep(
                trackedImage.transform,
                button.childName
            );

            if (button.cachedChild == null)
            {
                button.cachedChild = FindNearestNamedTransform(
                    button.childName,
                    trackedImage.transform.position,
                    trackedImage.gameObject.scene
                );
            }

            button.nextLookupTime = Time.time + 0.5f;

            if (button.cachedChild == null &&
                !button.warnedMissingChild)
            {
                Debug.LogWarning(
                    "VirtualButtonDetector: Tidak menemukan objek '" +
                    button.childName + "' di scene.",
                    this
                );

                button.warnedMissingChild = true;
            }

            if (button.cachedChild != null)
                button.warnedMissingChild = false;
        }

        return button.cachedChild;
    }

    private static Transform FindDeep(
        Transform root,
        string objectName)
    {
        if (root == null)
            return null;

        foreach (Transform child in
                 root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName &&
                child.gameObject.activeInHierarchy)
            {
                return child;
            }
        }

        return null;
    }

    private static Transform FindNearestNamedTransform(
        string objectName,
        Vector3 referencePosition,
        Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
            return null;

        Transform nearest = null;
        float nearestDistance = float.MaxValue;

        GameObject[] roots = scene.GetRootGameObjects();

        foreach (GameObject root in roots)
        {
            Transform[] transforms =
                root.GetComponentsInChildren<Transform>(true);

            foreach (Transform candidate in transforms)
            {
                if (candidate.name != objectName ||
                    !candidate.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float distance = (
                    candidate.position - referencePosition
                ).sqrMagnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = candidate;
                }
            }
        }

        return nearest;
    }

    private static float ColorDiff(Color a, Color b)
    {
        return (
            Mathf.Abs(a.r - b.r) +
            Mathf.Abs(a.g - b.g) +
            Mathf.Abs(a.b - b.b)
        ) / 3f;
    }

    private void OnGUI()
    {
        if (!showDebug)
            return;

        GUI.matrix = Matrix4x4.Scale(
            Vector3.one *
            (Screen.dpi > 0f ? Screen.dpi / 160f : 1f)
        );

        string debugText = status;

        if (calibrated && buttons != null)
        {
            foreach (ButtonDef button in buttons)
            {
                debugText += "\n" + button.name +
                    ": diff " + button.diff.ToString("F3") +
                    (button.pressed ? " [DITEKAN]" : "");
            }
        }

        debugText += "\nTerakhir ditekan: " + lastPressedName;

        GUI.Label(new Rect(8, 8, 460, 300), debugText);
    }
}
