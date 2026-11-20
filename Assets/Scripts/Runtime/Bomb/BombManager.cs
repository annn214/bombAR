using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class BombManager : MonoBehaviour
{
    [Header("Referensi Bom (opsional)")]
    [Tooltip("Isi dengan instance bom di Scene jika tersedia. Jangan isi dengan aset prefab.")]
    [SerializeField] private Transform bombContainer;

    [Header("Pengaturan Game")]
    [SerializeField, Min(1f)] private float timeLimit = 45f;
    [SerializeField, Min(1)] private int questionsToWin = 5;
    [SerializeField] private GameObject explosionEffect;

    // BARU: hanya boleh ada satu BombManager yang aktif
    private static BombManager activeInstance;

    private TMP_Text instructionText;
    private TMP_Text timerText;

    private float timeRemaining;
    private string correctColor;
    private bool isGameOver;
    private bool isInitialized;
    private bool warnedMissingTexts;
    private int answered;

    // True selama jeda menuju soal berikutnya
    private bool waitingNextQuestion;

    private readonly HashSet<string> usedQuestions =
        new HashSet<string>();

    private readonly Dictionary<string, string> puzzleLibrary =
        new Dictionary<string, string>
        {
            { "Apa hasil pencampuran warna Kuning + Biru?", "Hijau" },
            { "Pilih kabel yang warnanya seperti darah!", "Merah" },
            { "Pilih kabel yang warnanya seperti langit cerah!", "Biru" },
            { "Pilih kabel yang warnanya seperti pisang matang!", "Kuning" },
            { "Jinakkan bom! Potong kabel warna Hijau!", "Hijau" }
        };

    // BARU: matikan BombManager tambahan supaya soal
    // di layar dan penilaian selalu dari sumber yang sama.
    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            Debug.LogWarning(
                "BombManager tambahan ditemukan di '" + name +
                "' dan dinonaktifkan. Hapus komponen ini.",
                this
            );

            enabled = false;
            return;
        }

        activeInstance = this;
    }

    private void OnDestroy()
    {
        if (activeInstance == this)
            activeInstance = null;
    }

    private void Start()
    {
        if (explosionEffect != null)
            explosionEffect.SetActive(false);

        Debug.Log(
            "BombManager aktif di '" + name + "'. Menunggu Canvas bom...",
            this
        );

        TryInitializeGame();
    }

    private void Update()
    {
        if (!isInitialized)
        {
            TryInitializeGame();
            return;
        }

        if (isGameOver)
            return;

        timeRemaining = Mathf.Max(
            0f,
            timeRemaining - Time.deltaTime
        );

        timerText.text =
            "Waktu: " + Mathf.CeilToInt(timeRemaining);

        if (timeRemaining <= 0f)
            EndGame(false, "Waktu habis!");
    }

    private void TryInitializeGame()
    {
        if (isInitialized)
            return;

        instructionText = null;
        timerText = null;

        if (bombContainer != null)
        {
            FindTextsInTransform(bombContainer);
        }

        if (instructionText == null || timerText == null)
        {
            instructionText = null;
            timerText = null;

            Canvas[] canvases =
                FindObjectsByType<Canvas>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (Canvas canvas in canvases)
            {
                TMP_Text foundInstruction =
                    FindTextInChildren(
                        canvas.transform,
                        "InstructionText3D"
                    );

                TMP_Text foundTimer =
                    FindTextInChildren(
                        canvas.transform,
                        "TimerText3D"
                    );

                if (foundInstruction != null &&
                    foundTimer != null)
                {
                    instructionText = foundInstruction;
                    timerText = foundTimer;
                    break;
                }
            }
        }

        if (instructionText == null || timerText == null)
        {
            if (!warnedMissingTexts)
            {
                Debug.LogWarning(
                    "BombManager: menunggu Canvas yang memiliki " +
                    "InstructionText3D dan TimerText3D. " +
                    "Pastikan nama objek dan komponen TMP benar.",
                    this
                );

                warnedMissingTexts = true;
            }

            return;
        }

        isInitialized = true;

        RestartGame();

        Debug.Log(
            "BombManager berhasil dimulai. " +
            "Instruksi dan timer sudah terhubung.",
            this
        );
    }

    private void FindTextsInTransform(Transform root)
    {
        if (root == null)
            return;

        instructionText = FindTextInChildren(
            root, "InstructionText3D");

        timerText = FindTextInChildren(
            root, "TimerText3D");
    }

    private TMP_Text FindTextInChildren(
        Transform root,
        string targetName)
    {
        TMP_Text[] texts =
            root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text item in texts)
        {
            if (item.name == targetName)
                return item;
        }

        return null;
    }

    public void SelectCable(string color)
    {
        // Kalau tombol terhubung ke BombManager tambahan,
        // teruskan ke BombManager yang aktif.
        if (activeInstance != null && activeInstance != this)
        {
            activeInstance.SelectCable(color);
            return;
        }

        if (!isInitialized || isGameOver || waitingNextQuestion)
            return;

        Debug.Log(
            "Kabel dipilih: " + color + " | Jawaban: " + correctColor,
            this
        );

        if (color != correctColor)
        {
            EndGame(
                false,
                "Salah potong kabel!\nDipilih: " + color +
                ", jawaban: " + correctColor
            );
            return;
        }

        answered++;

        if (answered >= questionsToWin)
        {
            EndGame(true, "BOM BERHASIL DIJINAKKAN!");
            return;
        }

        waitingNextQuestion = true;

        instructionText.color = Color.green;
        instructionText.text =
            "Benar! Lanjut ke soal berikutnya...";

        CancelInvoke(nameof(NextQuestion));
        Invoke(nameof(NextQuestion), 1.5f);
    }

    private void NextQuestion()
    {
        waitingNextQuestion = false;

        if (!isGameOver)
            GenerateRandomPuzzle();
    }

    private void GenerateRandomPuzzle()
    {
        List<string> availableQuestions =
            new List<string>();

        foreach (string questionItem in puzzleLibrary.Keys)
        {
            if (!usedQuestions.Contains(questionItem))
                availableQuestions.Add(questionItem);
        }

        if (availableQuestions.Count == 0)
        {
            usedQuestions.Clear();
            availableQuestions.AddRange(puzzleLibrary.Keys);
        }

        string selectedQuestion = availableQuestions[
            Random.Range(0, availableQuestions.Count)
        ];

        usedQuestions.Add(selectedQuestion);
        correctColor = puzzleLibrary[selectedQuestion];
        PlayerPrefs.SetString("BombCorrectColor", correctColor);
        PlayerPrefs.Save();

        instructionText.color = Color.white;
        instructionText.text = selectedQuestion;

        Debug.Log(
            "Soal: " + selectedQuestion +
            " | Jawaban: " + correctColor,
            this
        );
    }

    private void EndGame(bool won, string message)
    {
        if (isGameOver)
            return;

        isGameOver = true;
        waitingNextQuestion = false;
        CancelInvoke(nameof(NextQuestion));

        instructionText.text = won
            ? message
            : message + "\nPermainan selesai.";

        instructionText.color =
            won ? Color.green : Color.red;

        timerText.text = won ? "AMAN" : "GAGAL";
        timerText.color =
            won ? Color.green : Color.red;

        if (!won && explosionEffect != null)
            explosionEffect.SetActive(true);

        Debug.Log(
            won ? "HASIL: MENANG" : "HASIL: KALAH",
            this
        );
    }

    public void RestartGame()
    {
        if (!isInitialized)
            return;

        CancelInvoke(nameof(NextQuestion));

        if (explosionEffect != null)
            explosionEffect.SetActive(false);

        timeRemaining = timeLimit;
        isGameOver = false;
        answered = 0;
        waitingNextQuestion = false;
        usedQuestions.Clear();

        timerText.color = Color.white;
        timerText.text =
            "Waktu: " + Mathf.CeilToInt(timeRemaining);

        GenerateRandomPuzzle();
    }
}
