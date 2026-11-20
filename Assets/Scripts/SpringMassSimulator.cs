using UnityEngine;
using TMPro;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Practical state machine for controlling trial runs.
/// </summary>
public enum ExperimentState
{
    Idle,
    Running,
    Paused,
    Completed
}

/// <summary>
/// Spring-Mass Physics Simulator (Hooke's Law & Simple Harmonic Motion).
/// Ensures dynamic oscillation bouncing immediately when any weight is attached,
/// and produces unique, scientifically accurate readings for every weight load.
/// Palette: Jarar Malik Theme (#0E1F2F, #0E1B48, #27425D, #87A7D0, #C18DB4, #E2CAD8).
/// </summary>
public class SpringMassSimulator : MonoBehaviour
{
    [Header("1. Apparatus 3D References")]
    public Transform helicalSpringMesh;
    public Transform massHanger;
    public Transform redPointerNeedle;

    [Header("2. UI Station References")]
    public LabStopwatch labStopwatch;
    public SpringObservationTable observationTable;
    public TextMeshProUGUI oscillationStatusText;
    public TextMeshProUGUI metreScaleHUDText;

    [Header("3. Physics Parameters")]
    public float springConstantK = 25f;
    public float dampingB = 0.012f; // Smooth, realistic damping for 10 oscillations
    public float hangerBaseMassKg = 0.05f; // 50g mass hanger
    public float gravity = 9.81f;

    [Header("4. Live Output")]
    public float attachedMassKg = 0.050f;
    public string activeWeightLabel = "50g Weight";
    public bool isMysteryMass = false;
    public float mysteryActualGrams = 0f;
    public float currentExtensionCm = 0f;
    public float timePeriodSeconds = 0f;

    [Header("5. Automated Oscillation System")]
    public int targetOscillations = 10;
    public int currentOscillations = 0;
    public bool isMeasuringOscillations = false;
    public ExperimentState currentState = ExperimentState.Idle;

    [Header("6. Manual Pull Interaction")]
    public bool isBeingManuallyPulled = false;

    // Palette hex codes (Jarar Malik)
    public const string HexDeep     = "#0E1F2F";
    public const string HexNavy     = "#0E1B48";
    public const string HexDenim    = "#27425D";
    public const string HexSky      = "#87A7D0";
    public const string HexMauve    = "#C18DB4";
    public const string HexLavender = "#E2CAD8";

    // Telemetry getters
    public float TotalMassKg => attachedMassKg > 0.002f ? attachedMassKg : hangerBaseMassKg;
    public float TotalMassGrams => TotalMassKg * 1000f;
    public float BaselineMm => 100f; // Scale baseline (10.0 cm)
    public float CurrentMm => BaselineMm + (currentExtensionCm * 10f);
    public float NaturalSpringLengthM => naturalSpringLength;
    public float NaturalSpringLengthCm => naturalSpringLength * 100f;
    public float CurrentSpringLengthM => naturalSpringLength + (currentExtensionCm / 100f);
    public float CurrentSpringLengthCm => (naturalSpringLength * 100f) + currentExtensionCm;
    public float ExtensionMeters => currentExtensionCm / 100f;
    public float AppliedForceN => TotalMassKg * gravity;
    public float RestoringForceN => TotalMassKg * gravity;
    public float StaticCalculatedK => (currentExtensionCm > 0.05f) ? (AppliedForceN / ExtensionMeters) : springConstantK;
    public float ElapsedTrialTimer => trialTimer;
    public float EquilibriumDisplacementY => equilibriumDisplacementY;
    public float CurrentDisplacementY => displacementY;

    public System.Action OnPhysicsTick;
    public System.Action<ExperimentState> OnExperimentStateChanged;
    public System.Action OnTrialCompleted;

    private XRSocketInteractor socket;
    private Vector3 springStartWorldPos;
    private Vector3 springStartScale;
    private Vector3 hangerStartWorldPos;
    private Vector3 pointerStartWorldPos;
    private float naturalSpringLength = 0.38f;
    private float displacementY = 0f;
    private float velocityY = 0f;

    private float equilibriumDisplacementY = 0f;
    private float prevDisplacementRelative = 0f;
    private float trialTimer = 0f;
    private bool crossedUpward = false;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        if (socket == null && massHanger != null)
            socket = massHanger.GetComponentInChildren<XRSocketInteractor>();

        if (massHanger == null)
            massHanger = this.transform;

        if (labStopwatch == null)
            labStopwatch = FindFirstObjectByType<LabStopwatch>();

        EnsureObservationTable();
    }

    private void EnsureObservationTable()
    {
        if (observationTable == null)
            observationTable = FindFirstObjectByType<SpringObservationTable>();

        if (observationTable == null)
        {
            observationTable = gameObject.AddComponent<SpringObservationTable>();
        }
    }

    void OnEnable()
    {
        if (socket != null)
        {
            socket.selectEntered.AddListener(OnWeightAttached);
            socket.selectExited.AddListener(OnWeightDetached);
        }
    }

    void OnDisable()
    {
        if (socket != null)
        {
            socket.selectEntered.RemoveListener(OnWeightAttached);
            socket.selectExited.RemoveListener(OnWeightDetached);
        }
    }

    public void RegisterSocket(XRSocketInteractor newSocket)
    {
        if (socket != null)
        {
            socket.selectEntered.RemoveListener(OnWeightAttached);
            socket.selectExited.RemoveListener(OnWeightDetached);
        }
        socket = newSocket;
        if (socket != null)
        {
            socket.selectEntered.AddListener(OnWeightAttached);
            socket.selectExited.AddListener(OnWeightDetached);
        }
    }

    void Start()
    {
        EnsureObservationTable();

        if (helicalSpringMesh != null)
        {
            springStartWorldPos = helicalSpringMesh.position;
            springStartScale = helicalSpringMesh.localScale;
        }

        if (massHanger != null)
            hangerStartWorldPos = massHanger.position;

        if (redPointerNeedle != null)
            pointerStartWorldPos = redPointerNeedle.position;

        if (socket != null)
        {
            SphereCollider sc = socket.GetComponent<SphereCollider>();
            if (sc != null) sc.radius = 0.12f;
        }

        // Initialize at 50g mass
        attachedMassKg = 0.050f;
        activeWeightLabel = "50g Weight";
        isMysteryMass = false;
        UpdateEquilibriumDisplacement();
        displacementY = equilibriumDisplacementY;
        RecordStaticReading();
        UpdateOscillationStatusUI();
        UpdateMetreScaleHUD();
    }

    public void UpdateEquilibriumDisplacement()
    {
        float mass = TotalMassKg;
        equilibriumDisplacementY = (mass * gravity) / springConstantK;
    }

    void FixedUpdate()
    {
        float mass = TotalMassKg;

        if (isBeingManuallyPulled)
        {
            currentExtensionCm = displacementY * 100f;
            timePeriodSeconds = 2f * Mathf.PI * Mathf.Sqrt(mass / springConstantK);

            UpdatePhysicalApparatusVisuals();
            UpdateMetreScaleHUD();
            OnPhysicsTick?.Invoke();
            return;
        }

        if (currentState == ExperimentState.Paused)
        {
            OnPhysicsTick?.Invoke();
            return;
        }

        // Physics integration (SHM with viscous damping)
        float weightForce = mass * gravity;
        float restoringForce = -springConstantK * displacementY;
        float dampingForce = -dampingB * velocityY;

        float netForce = weightForce + restoringForce + dampingForce;
        float acceleration = netForce / mass;

        velocityY += acceleration * Time.fixedDeltaTime;
        displacementY += velocityY * Time.fixedDeltaTime;

        if (displacementY < -0.05f)
        {
            displacementY = -0.05f;
            velocityY = 0f;
        }

        currentExtensionCm = displacementY * 100f;
        timePeriodSeconds = 2f * Mathf.PI * Mathf.Sqrt(mass / springConstantK);

        // Dynamic cycle detection & live timer
        if (isMeasuringOscillations && currentState == ExperimentState.Running)
        {
            trialTimer += Time.fixedDeltaTime;
            if (labStopwatch != null)
            {
                labStopwatch.SetTime(trialTimer);
            }

            float relPos = displacementY - equilibriumDisplacementY;

            if (prevDisplacementRelative < 0f && relPos >= 0f)
            {
                crossedUpward = true;
            }
            else if (crossedUpward && prevDisplacementRelative > 0f && relPos <= 0f)
            {
                crossedUpward = false;
                currentOscillations++;
                if (VRLabAudioNarrator.Instance != null)
                {
                    VRLabAudioNarrator.Instance.PlayOscillationBeep(currentOscillations, targetOscillations);
                }
                UpdateOscillationStatusUI();

                if (currentOscillations >= targetOscillations)
                {
                    FinishOscillationTrial();
                }
            }
            prevDisplacementRelative = relPos;
        }

        UpdatePhysicalApparatusVisuals();
        UpdateMetreScaleHUD();
        OnPhysicsTick?.Invoke();
    }

    private void UpdatePhysicalApparatusVisuals()
    {
        // 1. Helical Spring stretch (Local Z is length in Blender models rotated X=-90°)
        if (helicalSpringMesh != null)
        {
            float stretchRatio = (naturalSpringLength + displacementY) / naturalSpringLength;
            stretchRatio = Mathf.Max(0.2f, stretchRatio);
            helicalSpringMesh.localScale = new Vector3(
                springStartScale.x,
                springStartScale.y,
                springStartScale.z * stretchRatio
            );
        }

        // 2. Mass Hanger vertical position
        if (massHanger != null)
        {
            massHanger.position = hangerStartWorldPos + new Vector3(0f, -displacementY, 0f);
        }

        // 3. Pointer Needle tracking hanger tip
        if (redPointerNeedle != null)
        {
            redPointerNeedle.position = pointerStartWorldPos + new Vector3(0f, -displacementY, 0f);
        }
    }

    private void UpdateMetreScaleHUD()
    {
        if (metreScaleHUDText == null) return;

        float curMm = CurrentMm;
        float extCm = currentExtensionCm;
        float extM = ExtensionMeters;
        float totalMassG = TotalMassGrams;

        metreScaleHUDText.text = string.Format(
            "<color=" + HexLavender + "><b>METRE SCALE TELEMETRY</b></color>\n" +
            "Active Load: <b><color=" + HexMauve + ">{0:F0} g</color></b> ({1:F3} kg)\n" +
            "Pointer Reading: <b><color=" + HexLavender + ">{2:F1} mm</color></b> ({3:F2} cm)\n" +
            "Net Extension (x): <b><color=" + HexMauve + ">+{4:F2} cm</color></b> (+{5:F4} m)\n" +
            "Restoring Force: <b><color=" + HexSky + ">{6:F2} N</color></b>   |   k_stat: <b><color=" + HexMauve + ">{7:F1} N/m</color></b>",
            totalMassG, (totalMassG / 1000f), curMm, (curMm / 10f), extCm, extM, RestoringForceN, StaticCalculatedK
        );
    }

    // =========================================================================
    // MANUAL SPRING PULL GESTURE
    // =========================================================================

    public void OnManualPullStart()
    {
        isBeingManuallyPulled = true;
        velocityY = 0f;
        currentState = ExperimentState.Idle;
    }

    public void OnManualPullUpdate(float deltaWorldY)
    {
        if (!isBeingManuallyPulled) return;

        displacementY -= deltaWorldY;
        displacementY = Mathf.Clamp(displacementY, -0.05f, 0.40f);
    }

    public void SetManualPullDisplacement(float newDisplacement)
    {
        displacementY = newDisplacement;
        velocityY = 0f;
    }

    public void ReleaseManualPull()
    {
        OnManualPullRelease();
    }

    public void OnManualPullRelease()
    {
        isBeingManuallyPulled = false;
        velocityY = 0f;

        float deltaFromEq = Mathf.Abs(displacementY - equilibriumDisplacementY);
        if (deltaFromEq > 0.012f)
        {
            StartTrialFromCurrentDisplacement();
        }
        else
        {
            UpdateOscillationStatusUI();
        }
    }

    public void StartTrialFromCurrentDisplacement()
    {
        UpdateEquilibriumDisplacement();
        prevDisplacementRelative = displacementY - equilibriumDisplacementY;
        crossedUpward = false;
        currentOscillations = 0;
        trialTimer = 0f;
        isMeasuringOscillations = true;
        currentState = ExperimentState.Running;

        if (labStopwatch != null)
        {
            labStopwatch.ResetTimer();
            labStopwatch.StartTimer();
        }

        RecordStaticReading();
        UpdateOscillationStatusUI();
    }

    // =========================================================================
    // ATTACH WEIGHT & EXPERIMENT CONTROLS
    // =========================================================================

    /// <summary>
    /// Sets attached weight mass, updates equilibrium, and triggers immediate dynamic bouncing & live stopwatch!
    /// </summary>
    public void SetAttachedWeight(float massKg, string weightName = "", bool isMystery = false, float mysteryActual = 0f)
    {
        attachedMassKg = Mathf.Max(0.010f, massKg);
        activeWeightLabel = !string.IsNullOrEmpty(weightName) ? weightName : (attachedMassKg * 1000f).ToString("F0") + "g";
        isMysteryMass = isMystery;
        mysteryActualGrams = mysteryActual > 0.001f ? mysteryActual : (attachedMassKg * 1000f);

        UpdateEquilibriumDisplacement();
        RecordStaticReading();
        UpdateOscillationStatusUI();

        if (VRLabAudioNarrator.Instance != null)
        {
            VRLabAudioNarrator.Instance.PlayWeightSnapSound();
        }

        // Weight lagate hi spring up down move kare aur timer shuru ho!
        StartTrial();
    }

    /// <summary>
    /// Control: Start Trial (Displaces spring beyond equilibrium and bounces)
    /// </summary>
    public void StartTrial()
    {
        if (currentState == ExperimentState.Paused)
        {
            ResumeTrial();
            return;
        }

        UpdateEquilibriumDisplacement();

        // Displace spring by 4.5 cm to induce clear, visible up-down harmonic motion
        displacementY = equilibriumDisplacementY + 0.045f;
        velocityY = 0f;
        prevDisplacementRelative = 0.045f;
        crossedUpward = false;

        currentOscillations = 0;
        trialTimer = 0f;
        isMeasuringOscillations = true;
        currentState = ExperimentState.Running;

        if (labStopwatch != null)
        {
            labStopwatch.ResetTimer();
            labStopwatch.StartTimer();
        }

        RecordStaticReading();
        UpdateOscillationStatusUI();
        OnExperimentStateChanged?.Invoke(currentState);
    }

    public void PauseTrial()
    {
        if (currentState == ExperimentState.Running)
        {
            currentState = ExperimentState.Paused;
            if (labStopwatch != null) labStopwatch.StopTimer();
            UpdateOscillationStatusUI();
            OnExperimentStateChanged?.Invoke(currentState);
        }
    }

    public void ResumeTrial()
    {
        if (currentState == ExperimentState.Paused)
        {
            currentState = ExperimentState.Running;
            if (labStopwatch != null) labStopwatch.StartTimer();
            UpdateOscillationStatusUI();
            OnExperimentStateChanged?.Invoke(currentState);
        }
    }

    public void ResetTrial()
    {
        currentState = ExperimentState.Idle;
        isMeasuringOscillations = false;
        currentOscillations = 0;
        trialTimer = 0f;
        velocityY = 0f;

        UpdateEquilibriumDisplacement();
        displacementY = equilibriumDisplacementY;

        if (labStopwatch != null)
        {
            labStopwatch.ResetTimer();
        }

        UpdatePhysicalApparatusVisuals();
        UpdateOscillationStatusUI();
        UpdateMetreScaleHUD();
        OnExperimentStateChanged?.Invoke(currentState);
    }

    public void NextTrial()
    {
        EnsureObservationTable();
        if (observationTable != null)
        {
            observationTable.AdvanceTrialNumber();
        }

        // Increment to next weight step (50g -> 100g -> 150g -> 200g -> 250g)
        float nextMass = attachedMassKg + 0.050f;
        if (nextMass > 0.255f) nextMass = 0.050f;

        SetAttachedWeight(nextMass, (nextMass * 1000f).ToString("F0") + "g Weight", false, 0f);
    }

    public void RecordStaticReading()
    {
        EnsureObservationTable();
        if (observationTable == null) return;

        UpdateEquilibriumDisplacement();
        int trialNum = Mathf.Max(1, observationTable.CurrentTrial);
        float totalMassG = TotalMassGrams;
        float hangerG = hangerBaseMassKg * 1000f;
        float slottedG = attachedMassKg * 1000f;
        float l0Cm = NaturalSpringLengthCm;
        float extCm = equilibriumDisplacementY * 100f;
        float lFinalCm = l0Cm + extCm;
        float forceN = AppliedForceN;
        float kStat = (extCm > 0.001f) ? (forceN / (extCm / 100f)) : springConstantK;

        observationTable.RecordStaticReading(trialNum, totalMassG, hangerG, slottedG, l0Cm, lFinalCm, extCm, forceN, kStat, activeWeightLabel, isMysteryMass);
    }

    private void FinishOscillationTrial()
    {
        isMeasuringOscillations = false;
        currentState = ExperimentState.Completed;

        // Use actual physically simulated elapsed time for the 10 oscillations
        if (trialTimer < 0.5f)
        {
            // Fallback safety only if timer was corrupted or uninitialized
            float mass = TotalMassKg;
            trialTimer = 20f * Mathf.PI * Mathf.Sqrt(mass / springConstantK);
        }

        if (labStopwatch != null)
        {
            labStopwatch.StopTimer();
            labStopwatch.SetTime(trialTimer);
        }

        EnsureObservationTable();
        if (observationTable != null)
        {
            float extCm = equilibriumDisplacementY * 100f;
            observationTable.RecordReading(
                TotalMassGrams,
                extCm,
                trialTimer,
                targetOscillations,
                springConstantK,
                activeWeightLabel,
                isMysteryMass,
                mysteryActualGrams
            );
        }

        if (VRLabAudioNarrator.Instance != null)
        {
            VRLabAudioNarrator.Instance.PlaySuccessFanfare();
        }

        UpdateOscillationStatusUI();
        OnExperimentStateChanged?.Invoke(currentState);
        OnTrialCompleted?.Invoke();
    }

    public void UpdateOscillationStatusUI()
    {
        if (oscillationStatusText == null) return;

        float totalMassG = TotalMassGrams;
        EnsureObservationTable();
        int trialNum = (observationTable != null) ? Mathf.Max(1, observationTable.CurrentTrial) : 1;

        if (isBeingManuallyPulled)
        {
            oscillationStatusText.text = string.Format(
                "<color=" + HexMauve + "><b>[MANUAL PULL DETECTED]</b></color>\n" +
                "Elongation: <b><color=" + HexLavender + ">+{0:F2} cm</color></b>   |   Restoring Force: <b><color=" + HexMauve + ">{1:F2} N</color></b>\n" +
                "<color=" + HexSky + "><i>Release grip to trigger 10 oscillation cycles!</i></color>",
                currentExtensionCm, AppliedForceN
            );
        }
        else if (currentState == ExperimentState.Running)
        {
            string labelInfo = isMysteryMass ? "<b>[MYSTERY MASS (?)]</b>" : ("Active Load: <b>" + totalMassG.ToString("F0") + "g</b>");
            oscillationStatusText.text = string.Format(
                "<color=" + HexMauve + "><b>● LIVE OSCILLATIONS: TRIAL #{0}</b></color>\n" +
                "Cycle Progress: <b><color=" + HexLavender + ">{1} / {2}</color></b>   |   Elapsed Time: <b><color=" + HexLavender + ">{3:F2}s</color></b>   |   {4}",
                trialNum, currentOscillations, targetOscillations, trialTimer, labelInfo
            );
        }
        else if (currentState == ExperimentState.Paused)
        {
            oscillationStatusText.text = string.Format(
                "<color=" + HexLavender + "><b>❚❚ TRIAL #{0} PAUSED</b></color>\n" +
                "Cycles: <b><color=" + HexMauve + ">{1} / {2}</color></b>   |   Time Frozen: <b><color=" + HexLavender + ">{3:F2}s</color></b>   |   Click <b>RESUME</b> to continue.",
                trialNum, currentOscillations, targetOscillations, trialTimer
            );
        }
        else if (currentState == ExperimentState.Completed || currentOscillations >= targetOscillations)
        {
            int completedTrial = (observationTable != null && observationTable.RecordedCount > 0)
                ? observationTable.RecordedCount
                : Mathf.Max(1, trialNum);
            float tSingle = targetOscillations > 0 ? (trialTimer / targetOscillations) : 0f;

            if (isMysteryMass)
            {
                float calcKg = (springConstantK * tSingle * tSingle) / (4f * Mathf.PI * Mathf.PI);
                float calcG = calcKg * 1000f;
                float err = Mathf.Abs(calcG - mysteryActualGrams) / mysteryActualGrams * 100f;
                oscillationStatusText.text = string.Format(
                    "<color=" + HexMauve + "><b>[OK] MYSTERY TRIAL #{0} COMPLETE!</b></color>\n" +
                    "<color=" + HexLavender + ">Measured Period T = <b>{1:F3}s</b>   ->   <b>Calculated Mass = {2:F1}g</b> (Actual: {3:F0}g, Error: {4:F1}%)</color>",
                    completedTrial, tSingle, calcG, mysteryActualGrams, err
                );
            }
            else
            {
                oscillationStatusText.text = string.Format(
                    "<color=" + HexMauve + "><b>[OK] TRIAL #{0} COMPLETE!</b></color>\n" +
                    "<color=" + HexLavender + ">Timed <b>{1} oscillations</b> in <b>{2:F2}s</b>  (Period T = <b>{3:F3}s</b>) for <b>{4:F0}g</b> load.</color>",
                    completedTrial, targetOscillations, trialTimer, tSingle, totalMassG
                );
            }
        }
        else
        {
            oscillationStatusText.text = string.Format(
                "<color=" + HexMauve + "><b>[STATUS: READY]</b></color>   |   Active Load: <b><color=" + HexLavender + ">{0:F0}g</color></b> ({1:F3}kg)   |   Extension: <b><color=" + HexMauve + ">{2:F2} cm</color></b>\n" +
                "<color=" + HexSky + "><i>Attach a weight or select below to begin Trial #{3}.</i></color>",
                totalMassG, (totalMassG / 1000f), currentExtensionCm, trialNum
            );
        }
    }

    public float GetMassFromObject(GameObject go, out string weightLabel, out bool isMystery, out float mysteryActual)
    {
        weightLabel = "Weight";
        isMystery = false;
        mysteryActual = 0f;

        if (go == null) return 0.050f;

        LabWeight lw = go.GetComponentInParent<LabWeight>();
        if (lw == null) lw = go.GetComponentInChildren<LabWeight>();
        if (lw == null) lw = go.GetComponent<LabWeight>();
        if (lw != null && lw.massInGrams > 0.1f)
        {
            weightLabel = !string.IsNullOrEmpty(lw.weightLabel) ? lw.weightLabel : go.name;
            isMystery = lw.isMystery;
            mysteryActual = lw.massInGrams;
            return lw.MassInKg;
        }

        string n = go.name.ToLower();
        if (n.Contains("mystery_mass_a"))
        {
            weightLabel = "Mystery Mass A";
            isMystery = true;
            mysteryActual = 125f;
            return 0.125f;
        }
        if (n.Contains("mystery_mass_b"))
        {
            weightLabel = "Mystery Mass B";
            isMystery = true;
            mysteryActual = 175f;
            return 0.175f;
        }
        if (n.Contains("mystery_mass_c"))
        {
            weightLabel = "Mystery Mass C";
            isMystery = true;
            mysteryActual = 225f;
            return 0.225f;
        }
        if (n.Contains("mystery"))
        {
            weightLabel = "Mystery Mass";
            isMystery = true;
            mysteryActual = 150f;
            return 0.150f;
        }
        if (n.Contains("250g")) { weightLabel = "250g Weight"; return 0.250f; }
        if (n.Contains("200g")) { weightLabel = "200g Weight"; return 0.200f; }
        if (n.Contains("150g")) { weightLabel = "150g Weight"; return 0.150f; }
        if (n.Contains("100g")) { weightLabel = "100g Weight"; return 0.100f; }
        if (n.Contains("50g"))  { weightLabel = "50g Weight"; return 0.050f; }
        if (n.Contains("25g"))  { weightLabel = "25g Weight"; return 0.025f; }
        if (n.Contains("20g"))  { weightLabel = "20g Weight"; return 0.020f; }
        if (n.Contains("10g"))  { weightLabel = "10g Weight"; return 0.010f; }

        Rigidbody rb = go.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = go.GetComponent<Rigidbody>();
        if (rb != null && rb.mass > 0.005f && rb.mass < 5f)
        {
            weightLabel = (rb.mass * 1000f).ToString("F0") + "g Weight";
            return rb.mass;
        }

        return 0.050f;
    }

    public void OnWeightAttached(SelectEnterEventArgs args)
    {
        GameObject go = args.interactableObject != null ? args.interactableObject.transform.gameObject : null;
        string wLabel;
        bool isMyst;
        float mystActual;
        float massKg = GetMassFromObject(go, out wLabel, out isMyst, out mystActual);

        SetAttachedWeight(massKg, wLabel, isMyst, mystActual);
    }

    public void OnWeightDetached(SelectExitEventArgs args)
    {
        attachedMassKg = 0.050f;
        activeWeightLabel = "Empty Hanger";
        isMysteryMass = false;
        mysteryActualGrams = 0f;

        UpdateEquilibriumDisplacement();
        RecordStaticReading();
        UpdateOscillationStatusUI();
        ResetTrial();
    }
}
