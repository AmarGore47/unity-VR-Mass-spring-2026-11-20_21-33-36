using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Master Unified Interaction Manager for both Hand Tracking and VR Controllers.
/// Provides:
/// 1. Natural hand grabbing & pinch gesture support for all weights.
/// 2. Controller ray interaction and direct grab for all weights.
/// 3. Dynamic XRSocketInteractor on Mass_Hanger with attachment logic.
/// 4. Interactive Spring Pull Handle for physical spring pulling and releasing.
/// 5. Visual interaction feedback (selectable hover highlight, valid attachment point indicator,
///    successful attachment chime/flash, and invalid release recovery).
/// 6. UI ray and poke reachability across all contextual screens.
/// </summary>
public class VRHandInteractionManager : MonoBehaviour
{
    public static VRHandInteractionManager Instance { get; private set; }

    [Header("Colors (Jarar Malik Palette)")]
    public static readonly Color32 ColorMint = new Color32(0xC1, 0x8D, 0xB4, 0xFF);        // #C18DB4 (Soft Orchid)
    public static readonly Color32 ColorParchment = new Color32(0xE2, 0xCA, 0xD8, 0xFF);   // #E2CAD8 (Pale Lavender)
    public static readonly Color32 ColorBorder = new Color32(0x87, 0xA7, 0xD0, 0xFF);      // #87A7D0 (Sky Blue)
    public static readonly Color32 ColorCard = new Color32(0x0E, 0x1B, 0x48, 0xFF);        // #0E1B48 (Midnight Navy)
    public static readonly Color32 ColorWarning = new Color32(0x27, 0x42, 0x5D, 0xFF);     // #27425D (Slate Denim)

    private SpringMassSimulator simulator;
    private XRSocketInteractor hangerSocket;
    private GameObject socketIndicatorRing;
    private MeshRenderer socketIndicatorRenderer;
    private Material socketIndicatorMat;
    private AudioSource audioSource;
    private AudioClip snapChimeClip;

    private int activeHeldWeightsCount = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);

        simulator = FindFirstObjectByType<SpringMassSimulator>();
        SetupAudioFeedback();
    }

    private GameObject simulatorUIObject;

    void Start()
    {
        if (simulator == null)
            simulator = FindFirstObjectByType<SpringMassSimulator>();

        CleanUpObstructingApparatus();
        HideAffordanceCalloutsAndSimulatorOverlay();
        SetupApparatusLightingAndLabEnvironment();
        SetupMassesForInteraction();
        SetupHangerSocketAndPullInteraction();
        SetupCanvasesAndInteractors();
    }

    void Update()
    {
        // Developer shortcut: Press F1 to toggle XR Device Simulator overlay UI when needed (Input System compliant)
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame && simulatorUIObject != null)
        {
            simulatorUIObject.SetActive(!simulatorUIObject.activeSelf);
        }
    }

    /// <summary>
    /// Hides floating affordance callout badges ("Move", "Grab", "UI Press", "Blink", "Turn") and simulator overlays.
    /// Keeps controller models and simulator input functionality fully active.
    /// </summary>
    private void HideAffordanceCalloutsAndSimulatorOverlay()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string n = go.name.ToLower();
            if (n.Contains("affordance callouts") || n.Contains("affordance callout") || n.Contains("affordancecallout"))
            {
                go.SetActive(false);
            }
            if (n.Contains("devicesimulatorui") || n.Contains("xrdevicesimulatorui"))
            {
                simulatorUIObject = go;
                go.SetActive(false); // Clean view for recording and students
            }
        }
    }

    /// <summary>
    /// Illuminates apparatus with focused key spotlight and applies scientific lab aesthetic.
    /// </summary>
    private void SetupApparatusLightingAndLabEnvironment()
    {
        // 1. Dedicated Key Spotlight on the Helical Spring and Suspended Mass
        GameObject existingSpot = GameObject.Find("Apparatus_Focus_Spotlight");
        if (existingSpot == null)
        {
            GameObject spotGo = new GameObject("Apparatus_Focus_Spotlight");
            spotGo.transform.position = new Vector3(2.01f, 2.35f, 1.52f);
            spotGo.transform.rotation = Quaternion.Euler(75f, 0f, 0f);

            Light spotLight = spotGo.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.range = 3.5f;
            spotLight.spotAngle = 48f;
            spotLight.innerSpotAngle = 32f;
            spotLight.intensity = 2.4f;
            spotLight.color = new Color(0.97f, 0.98f, 1.0f); // Clean laboratory daylight
            spotLight.shadows = LightShadows.Soft;
        }

        // 2. High-contrast Red Pointer Needle
        if (simulator != null && simulator.redPointerNeedle != null)
        {
            MeshRenderer ptrRend = simulator.redPointerNeedle.GetComponent<MeshRenderer>();
            if (ptrRend != null && ptrRend.material != null)
            {
                ptrRend.material.color = new Color(1f, 0.15f, 0.15f, 1f); // Vibrant scientific red
            }
        }

        // 3. Laboratory Wall and Bench Surface Aesthetic (#263746 & #34495E)
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string n = go.name.ToLower();
            MeshRenderer rend = go.GetComponent<MeshRenderer>();
            if (rend == null || rend.material == null) continue;

            if (n.Contains("wall") || n.Contains("room_wall") || n.Contains("laboratory_wall"))
            {
                rend.material.color = new Color32(0x26, 0x37, 0x46, 0xFF); // #263746
            }
            else if (n.Contains("table") || n.Contains("bench") || n.Contains("workbench") || n.Contains("lab_table"))
            {
                rend.material.color = new Color32(0x34, 0x49, 0x5E, 0xFF); // #34495E
            }
        }
    }

    /// <summary>
    /// Cleans up floating horizontal metre scale meshes on the bench so they never clip or block Screen 3.
    /// </summary>
    private void CleanUpObstructingApparatus()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string n = go.name;
            if (n == "Metre_Scale_Body" || n.StartsWith("Scale_Tick_") || n.StartsWith("Scale_Label_"))
            {
                go.SetActive(false);
            }
        }
    }

    // =========================================================================
    // 1. MASS SETUP (Hands & Controllers Grab, Hover Highlight, Safe Return)
    // =========================================================================

    public void SetupMassesForInteraction()
    {
        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        foreach (var go in allObjects)
        {
            if (go == null) continue;
            string lowerName = go.name.ToLower();

            // Match slotted masses, hooked weights, mystery masses and spare masses on bench
            bool isWeight = lowerName.Contains("slotted_mass")
                         || lowerName.Contains("hooked_weight")
                         || lowerName.Contains("mystery_mass")
                         || lowerName.Contains("storage_spare_mass")
                         || go.GetComponent<LabWeight>() != null;

            // Skip hanging mass assembly parts or station arm components
            if (lowerName.Contains("mass_hanger") || lowerName.Contains("station_"))
            {
                isWeight = false;
            }

            if (isWeight)
            {
                float massG = 100f;
                string label = "100g Weight";
                bool isMystery = false;

                if (lowerName.Contains("mystery_mass_a"))
                {
                    massG = 125f;
                    label = "Mystery Mass A";
                    isMystery = true;
                }
                else if (lowerName.Contains("mystery_mass_b"))
                {
                    massG = 175f;
                    label = "Mystery Mass B";
                    isMystery = true;
                }
                else if (lowerName.Contains("mystery_mass_c"))
                {
                    massG = 225f;
                    label = "Mystery Mass C";
                    isMystery = true;
                }
                else if (lowerName.Contains("mystery"))
                {
                    massG = 150f;
                    label = "Mystery Mass";
                    isMystery = true;
                }
                else if (lowerName.Contains("10g")) { massG = 10f; label = "10g Weight"; }
                else if (lowerName.Contains("20g")) { massG = 20f; label = "20g Weight"; }
                else if (lowerName.Contains("50g")) { massG = 50f; label = "50g Weight"; }
                else if (lowerName.Contains("100g")) { massG = 100f; label = "100g Weight"; }
                else if (lowerName.Contains("150g")) { massG = 150f; label = "150g Weight"; }
                else if (lowerName.Contains("200g")) { massG = 200f; label = "200g Weight"; }
                else if (lowerName.Contains("250g")) { massG = 250f; label = "250g Weight"; }
                else if (lowerName.Contains("storage_spare_mass")) { massG = 100f; label = "Spare Mass"; }

                LabWeight lw = go.GetComponent<LabWeight>();
                if (lw == null) lw = go.AddComponent<LabWeight>();
                lw.massInGrams = massG;
                lw.weightLabel = label;
                lw.isMystery = isMystery;

                // Ensure convex collider
                Collider col = go.GetComponent<Collider>();
                if (col == null)
                {
                    MeshFilter mf = go.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null)
                    {
                        MeshCollider mc = go.AddComponent<MeshCollider>();
                        mc.convex = true;
                    }
                    else
                    {
                        BoxCollider bc = go.AddComponent<BoxCollider>();
                        bc.size = new Vector3(0.08f, 0.03f, 0.08f);
                    }
                }
                else if (col is MeshCollider mc && !mc.convex)
                {
                    mc.convex = true;
                }

                // Ensure Rigidbody
                Rigidbody rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.mass = massG / 1000f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

                // Ensure XRGrabInteractable for hands & controllers
                XRGrabInteractable grab = go.GetComponent<XRGrabInteractable>();
                if (grab == null) grab = go.AddComponent<XRGrabInteractable>();
                grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
                grab.throwOnDetach = false; // Prevents accidental flying across room

                // Attach feedback & return component
                WeightFeedbackAndReturn feedback = go.GetComponent<WeightFeedbackAndReturn>();
                if (feedback == null) feedback = go.AddComponent<WeightFeedbackAndReturn>();
                feedback.Initialize(this, grab, go.transform.position, go.transform.rotation);
            }
        }
    }

    // =========================================================================
    // 2. HANGER SOCKET & SPRING PULL HANDLE
    // =========================================================================

    public void SetupHangerSocketAndPullInteraction()
    {
        if (simulator == null) return;
        Transform hanger = simulator.massHanger;
        if (hanger == null) return;

        // 1. Create / Configure XRSocketInteractor
        hangerSocket = hanger.GetComponent<XRSocketInteractor>();
        if (hangerSocket == null)
            hangerSocket = hanger.gameObject.AddComponent<XRSocketInteractor>();

        // Attach point at base plate of hanger
        Transform attachPoint = hanger.Find("SocketAttachPoint");
        if (attachPoint == null)
        {
            GameObject ap = new GameObject("SocketAttachPoint");
            ap.transform.SetParent(hanger, false);
            ap.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            attachPoint = ap.transform;
        }
        hangerSocket.attachTransform = attachPoint;
        hangerSocket.socketActive = true;
        hangerSocket.recycleDelayTime = 0.25f;

        // Socket trigger collider
        SphereCollider socketTrigger = hanger.GetComponent<SphereCollider>();
        if (socketTrigger == null)
        {
            socketTrigger = hanger.gameObject.AddComponent<SphereCollider>();
            socketTrigger.isTrigger = true;
            socketTrigger.radius = 0.12f;
            socketTrigger.center = new Vector3(0f, -0.05f, 0f);
        }

        // Register with SpringMassSimulator
        simulator.RegisterSocket(hangerSocket);

        hangerSocket.selectEntered.AddListener(OnSocketAttachSuccess);
        hangerSocket.selectExited.AddListener(OnSocketDetach);

        // 2. Create Valid Attachment Point Visual Indicator (Glowing Disc/Halo)
        CreateSocketIndicatorRing(hanger, attachPoint.localPosition);

        // 3. Create Spring Pull Handle (Allows grabbing hanger to pull downward)
        CreateSpringPullHandle(hanger);
    }

    private void CreateSocketIndicatorRing(Transform parent, Vector3 localPos)
    {
        Transform existing = parent.Find("SocketHaloIndicator");
        if (existing != null)
        {
            socketIndicatorRing = existing.gameObject;
        }
        else
        {
            socketIndicatorRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketIndicatorRing.name = "SocketHaloIndicator";
            socketIndicatorRing.transform.SetParent(parent, false);
            socketIndicatorRing.transform.localPosition = localPos + new Vector3(0f, 0.005f, 0f);
            socketIndicatorRing.transform.localScale = new Vector3(0.09f, 0.002f, 0.09f);

            // Remove primitive collider so it does not block interactions
            Collider c = socketIndicatorRing.GetComponent<Collider>();
            if (c != null) Destroy(c);
        }

        socketIndicatorRenderer = socketIndicatorRing.GetComponent<MeshRenderer>();
        if (socketIndicatorRenderer != null)
        {
            Shader standardShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            socketIndicatorMat = new Material(standardShader);
            socketIndicatorMat.color = new Color32(0x3A, 0x5A, 0x40, 0x66); // Subtle idle
            socketIndicatorRenderer.material = socketIndicatorMat;
        }

        UpdateSocketIndicatorVisual(false);
    }

    private void CreateSpringPullHandle(Transform hanger)
    {
        Transform existing = hanger.Find("SpringPullHandle");
        if (existing != null) return;

        GameObject handle = new GameObject("SpringPullHandle");
        handle.transform.SetParent(hanger, false);
        handle.transform.localPosition = new Vector3(0f, -0.10f, 0f);

        // Grabbable collider
        SphereCollider sc = handle.AddComponent<SphereCollider>();
        sc.isTrigger = true;
        sc.radius = 0.08f;

        // Pull Interactable Component
        SpringPullInteractable pull = handle.AddComponent<SpringPullInteractable>();
        pull.Initialize(simulator);
    }

    // =========================================================================
    // 3. INTERACTION FEEDBACK & INDICATORS
    // =========================================================================

    public void NotifyWeightGrabbed(WeightFeedbackAndReturn weight)
    {
        activeHeldWeightsCount++;
        UpdateSocketIndicatorVisual(true);
    }

    public void NotifyWeightReleased(WeightFeedbackAndReturn weight)
    {
        activeHeldWeightsCount = Mathf.Max(0, activeHeldWeightsCount - 1);
        if (activeHeldWeightsCount == 0)
        {
            UpdateSocketIndicatorVisual(false);
        }
    }

    private void UpdateSocketIndicatorVisual(bool isHoldingWeight)
    {
        if (socketIndicatorRing == null || socketIndicatorMat == null) return;

        if (isHoldingWeight)
        {
            // Valid attachment point glowing mint ring
            socketIndicatorMat.color = new Color32(0x3A, 0xB1, 0x8A, 0xEE); // Bright Mint
            socketIndicatorRing.transform.localScale = new Vector3(0.12f, 0.003f, 0.12f);
        }
        else
        {
            // Idle subtle border
            socketIndicatorMat.color = new Color32(0x3A, 0x5A, 0x40, 0x44); // Dim Border
            socketIndicatorRing.transform.localScale = new Vector3(0.09f, 0.002f, 0.09f);
        }
    }

    private void OnSocketAttachSuccess(SelectEnterEventArgs args)
    {
        PlaySnapChime();
        StartCoroutine(FlashSocketSuccess());
    }

    private void OnSocketDetach(SelectExitEventArgs args)
    {
        UpdateSocketIndicatorVisual(activeHeldWeightsCount > 0);
    }

    private IEnumerator FlashSocketSuccess()
    {
        if (socketIndicatorMat != null)
        {
            socketIndicatorMat.color = ColorParchment;
            yield return new WaitForSeconds(0.15f);
            socketIndicatorMat.color = ColorMint;
            yield return new WaitForSeconds(0.25f);
            UpdateSocketIndicatorVisual(activeHeldWeightsCount > 0);
        }
    }

    // =========================================================================
    // 4. CANVASES & RAY INTERACTOR DISTANCE
    // =========================================================================

    public void SetupCanvasesAndInteractors()
    {
        // 1. Ensure TrackedDeviceGraphicRaycaster on all UI Canvases
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases)
        {
            var raycaster = c.GetComponent<TrackedDeviceGraphicRaycaster>();
            if (raycaster == null)
            {
                c.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            }
        }

        // 2. Ensure Controller & Hand Rays have sufficient reach (10m) and non-intrusive sleek visuals
        XRRayInteractor[] rays = FindObjectsByType<XRRayInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ray in rays)
        {
            if (ray != null)
            {
                ray.maxRaycastDistance = Mathf.Max(ray.maxRaycastDistance, 10f);

                // Configure sleek, non-intrusive ray visual line
                XRInteractorLineVisual line = ray.GetComponent<XRInteractorLineVisual>();
                if (line == null) line = ray.GetComponentInChildren<XRInteractorLineVisual>();
                if (line != null)
                {
                    line.lineWidth = 0.0025f; // Thin subtle ray instead of thick beam

                    Gradient grad = new Gradient();
                    grad.SetKeys(
                        new GradientColorKey[] {
                            new GradientColorKey(new Color(0.53f, 0.65f, 0.82f), 0.0f),
                            new GradientColorKey(new Color(0.76f, 0.55f, 0.71f), 1.0f)
                        },
                        new GradientAlphaKey[] {
                            new GradientAlphaKey(0.55f, 0.0f),
                            new GradientAlphaKey(0.0f, 1.0f)
                        }
                    );
                    line.validColorGradient = grad;
                    line.invalidColorGradient = grad;
                }
            }
        }
    }

    // =========================================================================
    // 5. AUDIO SYNTHESIS FOR ATTACHMENT FEEDBACK
    // =========================================================================

    private void SetupAudioFeedback()
    {
        audioSource = gameObject.GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0.75f;

        snapChimeClip = CreateProceduralChimeClip();
    }

    public void PlaySnapChime()
    {
        if (audioSource != null && snapChimeClip != null)
        {
            audioSource.PlayOneShot(snapChimeClip, 0.85f);
        }
    }

    private AudioClip CreateProceduralChimeClip()
    {
        int sampleRate = 44100;
        float duration = 0.22f;
        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 14f); // Quick bell decay
            float f1 = Mathf.Sin(2f * Mathf.PI * 659.25f * t); // E5 note
            float f2 = Mathf.Sin(2f * Mathf.PI * 1318.5f * t) * 0.5f; // E6 harmonic
            samples[i] = (f1 + f2) * envelope * 0.5f;
        }

        AudioClip clip = AudioClip.Create("SnapChime", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}

/// <summary>
/// Attached to every slotted weight to provide:
/// - Selectable hover highlight (Mint #3AB18A outline)
/// - Grab notification to activate socket indicator
/// - Safe auto-return if dropped far from table (invalid attachment feedback)
/// </summary>
public class WeightFeedbackAndReturn : MonoBehaviour
{
    private VRHandInteractionManager manager;
    private XRGrabInteractable grab;
    private Vector3 originalBenchPos;
    private Quaternion originalBenchRot;
    private MeshRenderer meshRenderer;
    private Color originalColor;
    private bool isGrabbed = false;

    public void Initialize(VRHandInteractionManager mgr, XRGrabInteractable g, Vector3 originPos, Quaternion originRot)
    {
        manager = mgr;
        grab = g;
        originalBenchPos = originPos;
        originalBenchRot = originRot;

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && meshRenderer.material != null)
        {
            originalColor = meshRenderer.material.color;
        }

        if (grab != null)
        {
            grab.hoverEntered.AddListener(OnHoverEnter);
            grab.hoverExited.AddListener(OnHoverExit);
            grab.selectEntered.AddListener(OnSelectEnter);
            grab.selectExited.AddListener(OnSelectExit);
        }
    }

    private void OnHoverEnter(HoverEnterEventArgs args)
    {
        if (!isGrabbed && meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = Color.Lerp(originalColor, VRHandInteractionManager.ColorMint, 0.45f);
        }
    }

    private void OnHoverExit(HoverExitEventArgs args)
    {
        if (!isGrabbed && meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = originalColor;
        }
    }

    private void OnSelectEnter(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = VRHandInteractionManager.ColorMint;
        }
        if (manager != null) manager.NotifyWeightGrabbed(this);
    }

    private void OnSelectExit(SelectExitEventArgs args)
    {
        isGrabbed = false;
        if (meshRenderer != null && meshRenderer.material != null)
        {
            meshRenderer.material.color = originalColor;
        }
        if (manager != null) manager.NotifyWeightReleased(this);

        // Check for invalid release (dropped onto floor or far off table)
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(CheckSafePlacementRoutine());
        }
    }

    private IEnumerator CheckSafePlacementRoutine()
    {
        yield return new WaitForSeconds(1.2f);

        // If not attached to socket and fallen below floor or strayed far away
        if (!isGrabbed)
        {
            XRGrabInteractable g = GetComponent<XRGrabInteractable>();
            if (g != null && !g.isSelected && (transform.position.y < 0.45f || Vector3.Distance(transform.position, originalBenchPos) > 3.0f))
            {
                // Invalid attachment feedback: smoothly return to bench rack
                Rigidbody rb = GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
                transform.position = originalBenchPos;
                transform.rotation = originalBenchRot;
            }
        }
    }
}

/// <summary>
/// Allows students to grab and pull the spring-mass system downward with either
/// physical Hand Tracking (pinch/grab gesture) or VR Controllers (grip/trigger).
/// Releasing initiates SHM oscillation and begins trial timing.
/// </summary>
public class SpringPullInteractable : XRBaseInteractable
{
    private SpringMassSimulator simulator;
    private bool isPulling = false;
    private Vector3 initialInteractingPos;
    private float initialDisplacementY;
    private IXRInteractor pullingInteractor;

    public void Initialize(SpringMassSimulator sim)
    {
        simulator = sim;
    }

    protected override void OnSelectEntered(SelectEnterEventArgs args)
    {
        base.OnSelectEntered(args);
        if (simulator == null) return;

        pullingInteractor = args.interactorObject;
        if (pullingInteractor != null)
        {
            initialInteractingPos = pullingInteractor.transform.position;
            initialDisplacementY = simulator.CurrentDisplacementY;
            isPulling = true;
            simulator.isBeingManuallyPulled = true;
        }
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);
        if (isPulling && simulator != null)
        {
            isPulling = false;
            pullingInteractor = null;
            simulator.ReleaseManualPull();
        }
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        base.ProcessInteractable(updatePhase);

        if (updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic && isPulling && pullingInteractor != null && simulator != null)
        {
            // Vertical displacement: pulling down in world space increases spring extension
            float deltaY = initialInteractingPos.y - pullingInteractor.transform.position.y;
            float newDisplacement = Mathf.Clamp(initialDisplacementY + deltaY, 0.01f, 0.22f);

            simulator.SetManualPullDisplacement(newDisplacement);
        }
    }
}
