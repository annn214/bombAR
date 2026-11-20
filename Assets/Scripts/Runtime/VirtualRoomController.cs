using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using UnityEngine.Video;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class VirtualRoomController : MonoBehaviour
{
    [Header("Fase 5 - Gerak")]
    [SerializeField] private CharacterController characterController;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private RectTransform joystickHandle;
    [SerializeField] private float moveSpeed = 2.5f;
    [SerializeField] private float lookSpeed = 0.12f;

    [Header("Fase 6 - Spatial Audio")]
    [SerializeField] private AudioSource bombAudio;
    [SerializeField] private AudioClip bombCountdownClip;
    [SerializeField, Min(0f)] private float bombAudioStartSeconds = 5f;

    [Header("Fase 7 - Mengambil Tang")]
    [SerializeField] private Transform holdPoint;
    [SerializeField] private Transform pliers;
    [SerializeField] private GameObject pickupButton;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private float pickupDistance = 2.2f;

    [Header("Fase 8 - Memilih Kabel")]
    [SerializeField] private Transform cableRoot;
    [SerializeField] private Renderer[] cableRenderers;
    [SerializeField] private float cableAimDistance = 3.5f;

    [Header("Fase 9 - Memotong Kabel")]
    [SerializeField] private GameObject cutButton;

    [Header("Fase 10 - Hasil")]
    [SerializeField] private AudioSource explosionAudio;
    [SerializeField] private AudioClip explosionClip;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private CanvasGroup deathTransitionOverlay;
    [SerializeField] private RawImage deathTransitionImage;
    [SerializeField] private VideoPlayer deathVideoPlayer;
    [SerializeField] private GameObject modeSelectionPanel;
    [SerializeField] private TMP_Text modeResultText;

    private const float JoystickRadius = 70f;
    private Vector2 moveInput;
    private Vector2 moveTouchStart;
    private Vector2 lastLookPosition;
    private float pitch;
    private float verticalVelocity;
    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private bool enabledEnhancedTouch;
    private bool hasPliers;
    private bool gameEnded;
    private string correctCableColor;
    private Color[] cableBaseColors;
    private Renderer highlightedCable;
    private MaterialPropertyBlock cablePropertyBlock;

    private void Awake()
    {
        Debug.Assert(characterController != null, "CharacterController belum dihubungkan.", this);
        Debug.Assert(playerCamera != null, "Player Camera belum dihubungkan.", this);
        Debug.Assert(joystickHandle != null, "Joystick Handle belum dihubungkan.", this);
        Debug.Assert(bombAudio != null, "Bomb Audio belum dihubungkan.", this);
        Debug.Assert(holdPoint != null, "Hold Point belum dihubungkan.", this);
        Debug.Assert(pliers != null, "Tang belum dihubungkan.", this);
        Debug.Assert(pickupButton != null, "Tombol Ambil belum dihubungkan.", this);
        Debug.Assert(crosshair != null, "Crosshair belum dihubungkan.", this);
        Debug.Assert(cableRoot != null, "Cable Root belum dihubungkan.", this);
        Debug.Assert(cableRenderers != null && cableRenderers.Length == 4,
            "Empat renderer kabel harus dihubungkan.", this);
        Debug.Assert(cutButton != null, "Tombol Potong belum dihubungkan.", this);
        Debug.Assert(explosionAudio != null, "Explosion Audio belum dihubungkan.", this);
        Debug.Assert(resultPanel != null && resultText != null,
            "Panel hasil belum dihubungkan.", this);
        Debug.Assert(deathTransitionOverlay != null && deathTransitionImage != null,
            "Overlay transisi kematian belum dihubungkan.", this);
        Debug.Assert(deathVideoPlayer != null,
            "Video Player transisi belum dihubungkan.", this);
        Debug.Assert(modeSelectionPanel != null,
            "Panel pilihan mode belum dihubungkan.", this);
        Debug.Assert(modeResultText != null,
            "Teks hasil akhir belum dihubungkan.", this);

        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            enabledEnhancedTouch = true;
        }

        if (bombAudio != null)
        {
            bombAudio.spatialBlend = 1f;
            bombAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            bombAudio.minDistance = 0.75f;
            bombAudio.maxDistance = 15f;
            bombAudio.dopplerLevel = 0f;

            if (bombCountdownClip != null)
            {
                bombAudio.clip = bombCountdownClip;
                bombAudio.loop = false;
                bombAudio.time = Mathf.Min(
                    bombAudioStartSeconds,
                    Mathf.Max(0f, bombCountdownClip.length - 0.01f));
                bombAudio.Play();
            }
        }

        correctCableColor = PlayerPrefs.GetString("BombCorrectColor", "Merah");

        cablePropertyBlock = new MaterialPropertyBlock();
        cableBaseColors = new Color[cableRenderers.Length];
        for (int i = 0; i < cableRenderers.Length; i++)
        {
            Debug.Assert(cableRenderers[i] != null,
                "Renderer kabel indeks " + i + " belum dihubungkan.", this);
            cableBaseColors[i] = cableRenderers[i].sharedMaterial.GetColor("_BaseColor");
        }

        pickupButton.SetActive(false);
        crosshair.SetActive(false);
        cutButton.SetActive(false);
        resultPanel.SetActive(false);
        deathTransitionOverlay.gameObject.SetActive(false);
        modeSelectionPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (enabledEnhancedTouch && EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Disable();

    }

    private void Update()
    {
        if (gameEnded)
            return;

        if (bombAudio != null && bombAudio.clip != null && bombAudio.isPlaying &&
            bombAudio.time >= bombAudio.clip.length - 3f)
        {
            StartCoroutine(FailureSequence("Waktu habis!", false));
            return;
        }

        ReadTouchInput();
        ReadEditorInput();
        MovePlayer();
        UpdateInteraction();
    }

    private void ReadTouchInput()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            int id = touch.touchId;
            Vector2 position = touch.screenPosition;

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                bool movementArea = position.x < Screen.width * 0.45f &&
                                    position.y < Screen.height * 0.55f;

                if (movementArea && moveTouchId < 0)
                {
                    moveTouchId = id;
                    moveTouchStart = position;
                }
                else if (lookTouchId < 0 && !PointerOverUi(id))
                {
                    lookTouchId = id;
                    lastLookPosition = position;
                }
            }

            if (id == moveTouchId)
            {
                Vector2 delta = Vector2.ClampMagnitude(
                    position - moveTouchStart,
                    JoystickRadius);
                moveInput = delta / JoystickRadius;
                joystickHandle.anchoredPosition = delta;
            }
            else if (id == lookTouchId)
            {
                Vector2 delta = position - lastLookPosition;
                RotateView(delta * lookSpeed);
                lastLookPosition = position;
            }

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended ||
                touch.phase == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                if (id == moveTouchId)
                {
                    moveTouchId = -1;
                    moveInput = Vector2.zero;
                    joystickHandle.anchoredPosition = Vector2.zero;
                }

                if (id == lookTouchId)
                    lookTouchId = -1;
            }
        }
    }

    private void ReadEditorInput()
    {
        if (Keyboard.current != null)
        {
            Vector2 keyboard = Vector2.zero;
            if (Keyboard.current.aKey.isPressed) keyboard.x -= 1f;
            if (Keyboard.current.dKey.isPressed) keyboard.x += 1f;
            if (Keyboard.current.sKey.isPressed) keyboard.y -= 1f;
            if (Keyboard.current.wKey.isPressed) keyboard.y += 1f;

            if (moveTouchId < 0)
                moveInput = Vector2.ClampMagnitude(keyboard, 1f);
        }

        if (Mouse.current != null && Mouse.current.rightButton.isPressed)
            RotateView(Mouse.current.delta.ReadValue() * lookSpeed);
    }

    private void MovePlayer()
    {
        Vector3 forward = playerCamera.transform.forward;
        Vector3 right = playerCamera.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        if (characterController.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -1f;
        else
            verticalVelocity += Physics.gravity.y * Time.deltaTime;

        Vector3 movement = (right * moveInput.x + forward * moveInput.y) * moveSpeed;
        movement.y = verticalVelocity;
        characterController.Move(movement * Time.deltaTime);
    }

    private void RotateView(Vector2 delta)
    {
        characterController.transform.Rotate(0f, delta.x, 0f);
        pitch = Mathf.Clamp(pitch - delta.y, -70f, 70f);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public void TakePliers()
    {
        if (hasPliers || Vector3.Distance(characterController.transform.position,
                pliers.position) > pickupDistance)
            return;

        hasPliers = true;
        pickupButton.SetActive(false);
        crosshair.SetActive(true);

        foreach (Collider pliersCollider in pliers.GetComponentsInChildren<Collider>())
            pliersCollider.enabled = false;

        pliers.SetParent(holdPoint, false);
        pliers.localPosition = Vector3.zero;
        pliers.localRotation = Quaternion.Euler(8f, 0f, 0f);
        pliers.localScale = Vector3.one * 0.6f;
    }

    private void UpdateInteraction()
    {
        if (gameEnded)
        {
            cutButton.SetActive(false);
            SetHighlightedCable(null);
            return;
        }

        if (!hasPliers)
        {
            bool canPickup = Vector3.Distance(characterController.transform.position,
                pliers.position) <= pickupDistance;
            pickupButton.SetActive(canPickup);
            if (canPickup && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
                TakePliers();

            SetHighlightedCable(null);
            cutButton.SetActive(false);
            return;
        }

        Ray aimRay = new Ray(playerCamera.transform.position,
            playerCamera.transform.forward);
        if (Physics.Raycast(aimRay, out RaycastHit hit, cableAimDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.transform.IsChildOf(cableRoot))
        {
            SetHighlightedCable(hit.collider.GetComponent<Renderer>());
            cutButton.SetActive(highlightedCable != null);

            if (highlightedCable != null && Keyboard.current != null &&
                Keyboard.current.fKey.wasPressedThisFrame)
                CutSelectedCable();

            return;
        }

        SetHighlightedCable(null);
        cutButton.SetActive(false);
    }

    public void CutSelectedCable()
    {
        if (gameEnded || !hasPliers || highlightedCable == null)
            return;

        Renderer selectedCable = highlightedCable;
        string selectedColor = GetCableColor(selectedCable.name);
        SetHighlightedCable(null);
        cutButton.SetActive(false);
        ReleaseCable(selectedCable);

        if (selectedColor == correctCableColor)
            StartCoroutine(SuccessSequence());
        else
            StartCoroutine(FailureSequence(
                "Kabel " + selectedColor + " salah!", true));
    }

    private void ReleaseCable(Renderer cable)
    {
        Rigidbody cableBody = cable.GetComponent<Rigidbody>();
        if (cableBody == null)
            return;

        cable.transform.SetParent(null, true);
        cableBody.isKinematic = false;
        cableBody.AddForce(
            (Vector3.down + playerCamera.transform.right * 0.25f) * 0.45f,
            ForceMode.Impulse);
        cableBody.AddTorque(playerCamera.transform.forward * 0.12f,
            ForceMode.Impulse);
    }

    private IEnumerator SuccessSequence()
    {
        gameEnded = true;
        StopBombAudio();
        ShowResult("BERHASIL\nBom berhasil dijinakkan!", Color.green);
        yield return new WaitForSeconds(1.75f);
        resultPanel.SetActive(false);
        ShowModeSelection("BOMB DEFUSED", Color.green);
    }

    private IEnumerator FailureSequence(string message, bool playExplosion)
    {
        gameEnded = true;

        if (playExplosion)
        {
            StopBombAudio();
            if (explosionClip != null)
            {
                explosionAudio.clip = explosionClip;
                explosionAudio.time = Mathf.Max(0f, explosionClip.length - 3f);
                explosionAudio.Play();
            }
        }

        ShowResult("GAGAL\n" + message, Color.red);

        characterController.enabled = false;
        Vector3 startPosition = characterController.transform.position;
        Quaternion startRotation = playerCamera.transform.localRotation;
        Quaternion fallenRotation = Quaternion.Euler(-78f, 0f, 88f);

        const float fallDuration = 1.15f;
        float elapsed = 0f;
        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.SmoothStep(
                0f, 1f, Mathf.Clamp01(elapsed / fallDuration));
            characterController.transform.position = Vector3.Lerp(
                startPosition,
                startPosition + Vector3.down * 1.15f,
                progress);
            playerCamera.transform.localRotation = Quaternion.Slerp(
                startRotation,
                fallenRotation,
                progress);
            yield return null;
        }

        yield return PlayDeathTransition();
        resultPanel.SetActive(false);
        ShowModeSelection("YOU DIED", new Color(0.85f, 0.02f, 0.02f));
    }

    private IEnumerator PlayDeathTransition()
    {
        deathTransitionOverlay.gameObject.SetActive(true);
        deathTransitionOverlay.alpha = 0f;

        if (deathVideoPlayer.clip != null)
        {
            deathTransitionImage.color = Color.white;
            deathVideoPlayer.Play();
        }
        else
        {
            deathTransitionImage.texture = null;
            deathTransitionImage.color = Color.black;
        }

        float elapsed = 0f;
        while (elapsed < 0.35f)
        {
            elapsed += Time.deltaTime;
            deathTransitionOverlay.alpha = Mathf.Clamp01(elapsed / 0.35f);
            yield return null;
        }

        if (deathVideoPlayer.clip != null)
        {
            while (deathVideoPlayer.isPlaying)
                yield return null;
        }
        else
        {
            yield return new WaitForSeconds(1.25f);
        }

        deathTransitionOverlay.gameObject.SetActive(false);
    }

    private void StopBombAudio()
    {
        if (bombAudio != null)
            bombAudio.Stop();
    }

    private void ShowResult(string message, Color color)
    {
        resultText.text = message;
        resultText.color = color;
        resultPanel.SetActive(true);
    }

    private void ShowModeSelection(string message, Color color)
    {
        modeResultText.text = message;
        modeResultText.color = color;
        modeSelectionPanel.SetActive(true);
    }

    private static string GetCableColor(string objectName)
    {
        if (objectName.Contains("Red")) return "Merah";
        if (objectName.Contains("Blue")) return "Biru";
        if (objectName.Contains("Green")) return "Hijau";
        if (objectName.Contains("Yellow")) return "Kuning";
        return objectName;
    }

    private void SetHighlightedCable(Renderer cable)
    {
        if (highlightedCable == cable)
            return;

        if (highlightedCable != null)
            ApplyCableColor(highlightedCable,
                cableBaseColors[FindCableIndex(highlightedCable)]);

        highlightedCable = cable;
        if (highlightedCable != null)
        {
            int cableIndex = FindCableIndex(highlightedCable);
            ApplyCableColor(highlightedCable,
                Color.Lerp(cableBaseColors[cableIndex], Color.white, 0.7f));
        }
    }

    private int FindCableIndex(Renderer cable)
    {
        for (int i = 0; i < cableRenderers.Length; i++)
        {
            if (cableRenderers[i] == cable)
                return i;
        }

        return 0;
    }

    private void ApplyCableColor(Renderer cable, Color color)
    {
        cable.GetPropertyBlock(cablePropertyBlock);
        cablePropertyBlock.SetColor("_BaseColor", color);
        cable.SetPropertyBlock(cablePropertyBlock);
    }

    private bool PointerOverUi(int pointerId)
    {
        return EventSystem.current != null &&
               EventSystem.current.IsPointerOverGameObject(pointerId);
    }

}
