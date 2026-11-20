using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
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

    private const float JoystickRadius = 70f;
    private Vector2 moveInput;
    private Vector2 moveTouchStart;
    private Vector2 lastLookPosition;
    private float pitch;
    private float verticalVelocity;
    private float nextBeepTime;
    private int moveTouchId = -1;
    private int lookTouchId = -1;
    private bool enabledEnhancedTouch;
    private bool hasPliers;
    private AudioClip beepClip;
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

        if (!EnhancedTouchSupport.enabled)
        {
            EnhancedTouchSupport.Enable();
            enabledEnhancedTouch = true;
        }

        if (bombAudio != null)
        {
            beepClip = CreateBeepClip();
            bombAudio.spatialBlend = 1f;
            bombAudio.rolloffMode = AudioRolloffMode.Logarithmic;
            bombAudio.minDistance = 0.75f;
            bombAudio.maxDistance = 15f;
            bombAudio.dopplerLevel = 0f;
        }

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
    }

    private void OnDestroy()
    {
        if (enabledEnhancedTouch && EnhancedTouchSupport.enabled)
            EnhancedTouchSupport.Disable();

        if (beepClip != null)
            Destroy(beepClip);
    }

    private void Update()
    {
        ReadTouchInput();
        ReadEditorInput();
        MovePlayer();
        PlayBeep();
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

    private void PlayBeep()
    {
        if (bombAudio == null || beepClip == null || Time.time < nextBeepTime)
            return;

        bombAudio.PlayOneShot(beepClip, 0.85f);
        nextBeepTime = Time.time + 0.9f;
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
        if (!hasPliers)
        {
            bool canPickup = Vector3.Distance(characterController.transform.position,
                pliers.position) <= pickupDistance;
            pickupButton.SetActive(canPickup);
            if (canPickup && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame)
                TakePliers();

            SetHighlightedCable(null);
            return;
        }

        Ray aimRay = new Ray(playerCamera.transform.position,
            playerCamera.transform.forward);
        if (Physics.Raycast(aimRay, out RaycastHit hit, cableAimDistance,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
            hit.transform.IsChildOf(cableRoot))
        {
            SetHighlightedCable(hit.collider.GetComponent<Renderer>());
            return;
        }

        SetHighlightedCable(null);
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

    private static AudioClip CreateBeepClip()
    {
        const int sampleRate = 44100;
        const float duration = 0.11f;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float envelope = 1f - i / (float)sampleCount;
            samples[i] = Mathf.Sin(2f * Mathf.PI * 920f * i / sampleRate) *
                         envelope * 0.35f;
        }

        AudioClip clip = AudioClip.Create(
            "BombBeep",
            sampleCount,
            1,
            sampleRate,
            false);
        clip.SetData(samples, 0);
        return clip;
    }
}
