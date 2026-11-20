using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Practical experiment stages for contextual UI screen management.
/// </summary>
public enum PracticalStage
{
    Introduction,  // Experiment aim, principles & Start button
    Apparatus,     // Mass selection, theory, instructions on attaching weights
    Measurement,   // Active live stopwatch, cycle counter and live readings
    Observation,   // Recorded experimental trials table
    Results        // Scientific M vs T^2 graph, slope calculation & mystery mass analysis
}

/// <summary>
/// Master Laboratory UI Controller for Masses & Helical Spring VR Practical.
/// 
/// Contextual Single-Screen Architecture:
/// - Instead of three huge screens simultaneously occupying the student's field of view,
///   a single focused workstation display is positioned comfortably on the right workbench desk
///   (X=1.22m, Y=1.42m, Z=1.38m, Yaw 195°), leaving the helical spring apparatus on the left (X=2.01m)
///   completely visible, unobstructed, and central at all times.
/// - The UI dynamically displays only the relevant stage panel, with an always-accessible
///   4-tab Navigation Bar allowing the student to navigate to any screen on demand.
/// - Automated Context Transitions:
///     1. Start -> Apparatus (select mass or grab from table)
///     2. Weight Attached / Pulled -> Measurement (Stopwatch, 10 oscillations, live period)
///     3. 10 Cycles Complete -> Observation (Table row logged, accuracy summary)
///     4. View Graph -> Results (M vs T^2 scatter plot & slope)
/// </summary>
public class VRPracticalUIController : MonoBehaviour
{
    public static VRPracticalUIController Instance { get; private set; }

    // =========================================================================
    // SCIENTIFIC THEME PALETTE (Jarar Malik 6-Color System)
    // #0E1B48 | #C18DB4 | #E2CAD8 | #87A7D0 | #27425D | #0E1F2F
    // =========================================================================
    public static readonly Color32 ColorBgDeep    = new Color32(0x0E, 0x1F, 0x2F, 0xFF); // #0E1F2F (Deep Midnight Slate Base)
    public static readonly Color32 ColorCardBg    = new Color32(0x27, 0x42, 0x5D, 0xFA); // #27425D (Slate Denim Card Container)
    public static readonly Color32 ColorSubPanel  = new Color32(0x0E, 0x1B, 0x48, 0xFF); // #0E1B48 (Midnight Navy Header/Sub-panel)
    public static readonly Color32 ColorBorder    = new Color32(0x87, 0xA7, 0xD0, 0xFF); // #87A7D0 (Sky Blue Accent Border)
    public static readonly Color32 ColorButtonBg  = new Color32(0xC1, 0x8D, 0xB4, 0xFF); // #C18DB4 (Soft Orchid / Mauve Highlight)
    public static readonly Color32 ColorAccent    = new Color32(0x87, 0xA7, 0xD0, 0xFF); // #87A7D0 (Sky Blue Telemetry)
    public static readonly Color32 ColorHighlight = new Color32(0xFF, 0xFF, 0xFF, 0xFF); // Crisp Pure White
    public static readonly Color32 ColorTextLight = new Color32(0xE2, 0xCA, 0xD8, 0xFF); // #E2CAD8 (Pale Lavender Crisp Text)
    public static readonly Color32 ColorTextMuted = new Color32(0x87, 0xA7, 0xD0, 0xFF); // #87A7D0 (High-legibility Sky Blue)

    // Rich-text hex tags
    public const string HexDeep     = "#0E1F2F";
    public const string HexNavy     = "#0E1B48";
    public const string HexDenim    = "#27425D";
    public const string HexSky      = "#87A7D0";
    public const string HexMauve    = "#C18DB4";
    public const string HexLavender = "#E2CAD8";
    public const string HexWhite    = "#FFFFFF";

    // Legacy aliases
    public const string HexPeach    = HexLavender;
    public const string HexCoral    = HexSky;
    public const string HexRose     = HexMauve;
    public const string HexRasp     = HexDenim;
    public const string HexPlum     = HexNavy;
    public const string HexIvory    = HexLavender;
    public const string HexAmber    = HexMauve;
    public const string HexTan      = HexSky;
    public const string HexCaramel  = HexSky;
    public const string HexCard     = HexNavy;
    public const string HexWarmGold = HexLavender;

    // =========================================================================
    // STAGE PANELS (Only ONE active at any time!)
    // =========================================================================
    private PracticalStage currentStage = PracticalStage.Introduction;
    public PracticalStage CurrentStage => currentStage;

    private GameObject screenIntroduction;
    private GameObject screenApparatus;
    private GameObject screenMeasurement;
    private GameObject screenObservation;
    private GameObject screenResults;

    // Dynamic UI References: Measurement Screen
    private TextMeshProUGUI timerDigitalText;
    private TextMeshProUGUI timerStatusBadgeText;
    private TextMeshProUGUI oscillationCountText;
    private TextMeshProUGUI oscillationPeriodText;
    private RectTransform progressBarFill;
    private TextMeshProUGUI scalePointerReadoutText;
    private TextMeshProUGUI pauseResumeBtnText;

    // Dynamic UI References: Apparatus Screen
    private TextMeshProUGUI apparatusGuideText;

    // Dynamic UI References: Observation Screen
    private TextMeshProUGUI observationTableText;
    private TextMeshProUGUI observationCalculationsText;

    // Dynamic UI References: Results Screen
    public enum ResultsGraphMode { StaticMethod, DynamicMethod }
    private ResultsGraphMode currentGraphMode = ResultsGraphMode.StaticMethod;

    private TextMeshProUGUI resultsGraphText;
    private TextMeshProUGUI resultsCalculationsText;
    private TextMeshProUGUI resultsMysteryText;
    private Image btnStaticGraphImg;
    private TextMeshProUGUI btnStaticGraphTxt;
    private Image btnDynamicGraphImg;
    private TextMeshProUGUI btnDynamicGraphTxt;

    // Navigation Bar Button References for active tab highlights
    private List<Image> navBtnImages = new List<Image>();
    private List<TextMeshProUGUI> navBtnTexts = new List<TextMeshProUGUI>();
    private List<PracticalStage> navBtnStages = new List<PracticalStage>();

    private SpringMassSimulator simulator;
    private SpringObservationTable obsTable;
    private TMP_FontAsset baseFont;
    private int mysteryCycleCounter = 0;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    void Start()
    {
        baseFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        simulator = FindFirstObjectByType<SpringMassSimulator>();
        EnsureObservationTable();

        CleanUpObstructingApparatus();
        BuildAllStageDisplays();

        if (simulator != null)
        {
            simulator.OnPhysicsTick += OnSimulatorTick;
            simulator.OnExperimentStateChanged += OnSimulatorStateChanged;
            simulator.OnTrialCompleted += OnTrialCompleted;
        }

        if (obsTable != null)
        {
            obsTable.OnDataChanged += OnDataChanged;
        }

        // Start with Introduction screen
        SetStage(PracticalStage.Introduction);
    }

    /// <summary>
    /// Hides horizontal floating metre scale objects on bench that clip across Screen 3.
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

    private void EnsureObservationTable()
    {
        if (obsTable == null)
            obsTable = FindFirstObjectByType<SpringObservationTable>();

        if (obsTable == null)
        {
            obsTable = gameObject.AddComponent<SpringObservationTable>();
        }
    }

    void OnDestroy()
    {
        if (simulator != null)
        {
            simulator.OnPhysicsTick -= OnSimulatorTick;
            simulator.OnExperimentStateChanged -= OnSimulatorStateChanged;
            simulator.OnTrialCompleted -= OnTrialCompleted;
        }
        if (obsTable != null)
        {
            obsTable.OnDataChanged -= OnDataChanged;
        }
    }

    // =========================================================================
    // CONTEXTUAL STAGE MANAGEMENT SYSTEM
    // =========================================================================

    public void StartPractical()
    {
        if (VRLabAudioNarrator.Instance != null)
        {
            VRLabAudioNarrator.Instance.PlayButtonClickSound();
            VRLabAudioNarrator.Instance.PlayWelcomeVoiceover();
        }
        SetStage(PracticalStage.Apparatus);
    }

    /// <summary>
    /// Switches to the specified experiment stage. Ensures only ONE screen is active at a time!
    /// </summary>
    public void SetStage(PracticalStage targetStage)
    {
        currentStage = targetStage;

        if (screenIntroduction != null)
            screenIntroduction.SetActive(currentStage == PracticalStage.Introduction);

        if (screenApparatus != null)
            screenApparatus.SetActive(currentStage == PracticalStage.Apparatus);

        if (screenMeasurement != null)
            screenMeasurement.SetActive(currentStage == PracticalStage.Measurement);

        if (screenObservation != null)
            screenObservation.SetActive(currentStage == PracticalStage.Observation);

        if (screenResults != null)
            screenResults.SetActive(currentStage == PracticalStage.Results);

        UpdateNavBarHighlights();
        RefreshAllDisplays();
    }

    private void OnSimulatorStateChanged(ExperimentState state)
    {
        // When oscillation starts (mass attached or pulled), auto-switch to Measurement stage
        if (state == ExperimentState.Running)
        {
            if (currentStage == PracticalStage.Apparatus || currentStage == PracticalStage.Introduction)
            {
                SetStage(PracticalStage.Measurement);
            }
        }
    }

    private void OnTrialCompleted()
    {
        // When 10 cycles finish, auto-switch to Observation stage to inspect recorded trial
        SetStage(PracticalStage.Observation);
    }

    private void UpdateNavBarHighlights()
    {
        for (int i = 0; i < navBtnImages.Count; i++)
        {
            if (navBtnImages[i] == null || navBtnTexts[i] == null) continue;

            bool isActive = (navBtnStages[i] == currentStage);
            navBtnImages[i].color = isActive ? ColorButtonBg : ColorSubPanel;
            navBtnTexts[i].color = isActive ? Color.white : (Color)ColorTextMuted;
        }
    }

    // =========================================================================
    // UI BUILDER: 5 CONTEXTUAL STAGES
    // =========================================================================

    private void BuildAllStageDisplays()
    {
        navBtnImages.Clear();
        navBtnTexts.Clear();
        navBtnStages.Clear();

        screenIntroduction = BuildIntroductionScreen();
        screenApparatus    = BuildApparatusScreen();
        screenMeasurement  = BuildMeasurementScreen();
        screenObservation  = BuildObservationScreen();
        screenResults      = BuildResultsScreen();
    }

    // -------------------------------------------------------------------------
    // 1. INTRODUCTION STAGE (Centered at X=1.65m, Y=1.46m, Z=1.40m, Yaw 180°)
    // -------------------------------------------------------------------------
    private GameObject BuildIntroductionScreen()
    {
        Vector3 pos = new Vector3(1.65f, 1.46f, 1.40f);
        Quaternion rot = Quaternion.Euler(4f, 180f, 0f);
        Vector2 size = new Vector2(580f, 440f);

        GameObject screen = CreateScreenPanel("Stage_1_Introduction", pos, rot, size);

        // Header Banner
        GameObject header = CreateBox(screen.transform, "Header", new Vector2(0f, 185f), new Vector2(540f, 44f), ColorSubPanel);
        AddOutline(header, ColorBorder, 1.5f);
        CreateText(header.transform, "Title", "<b><color=" + HexWhite + ">MASSES & HELICAL SPRING PRACTICAL</color></b>", 17f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(520f, 36f));

        // Sub-badge
        GameObject badge = CreateBox(screen.transform, "Badge", new Vector2(0f, 150f), new Vector2(540f, 22f), ColorBgDeep);
        CreateText(badge.transform, "BadgeTxt", "<color=" + HexSky + ">Determination of Spring Constant (k) via Static & Dynamic SHM</color>", 12f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(530f, 20f));

        // Card A: Aim & Objective
        GameObject aimCard = CreateCard(screen.transform, "AimCard", new Vector2(0f, 75f), new Vector2(540f, 110f));
        CreateText(aimCard.transform, "AimTitle", "<b><color=" + HexMauve + ">EXPERIMENT OBJECTIVE</color></b>", 14f, TextAlignmentOptions.TopLeft, new Vector2(0f, 36f), new Vector2(510f, 24f));
        string aimBody =
            "1. Measure static spring elongation (<b>x</b>) under loads to verify Hooke's Law: <b>F = k*x</b>.\n" +
            "2. Measure time period (<b>T</b>) of 10 vertical oscillations for each mass.\n" +
            "3. Plot <b>M vs T^2</b> linear graph to compute spring constant (<b>k</b>) and identify unknown masses.";
        CreateText(aimCard.transform, "AimBody", "<color=" + HexLavender + ">" + aimBody + "</color>", 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, -14f), new Vector2(510f, 72f));

        // Card B: VR Navigation Guide
        GameObject guideCard = CreateCard(screen.transform, "GuideCard", new Vector2(0f, -42f), new Vector2(540f, 96f));
        CreateText(guideCard.transform, "GuideTitle", "<b><color=" + HexMauve + ">VR WORKSTATION GUIDE</color></b>", 14f, TextAlignmentOptions.TopLeft, new Vector2(0f, 28f), new Vector2(510f, 22f));
        string guideBody =
            "* <b>Apparatus Area:</b> The physical spring & stand are visible on your left.\n" +
            "* <b>Contextual Display:</b> This monitor will switch screens as you progress through each stage.\n" +
            "* <b>Hands / Controllers:</b> Grab physical weights from the table or click quick buttons!";
        CreateText(guideCard.transform, "GuideBody", "<color=" + HexSky + ">" + guideBody + "</color>", 12f, TextAlignmentOptions.TopLeft, new Vector2(0f, -10f), new Vector2(510f, 58f));

        // Prominent START PRACTICAL Button
        CreateButton(screen.transform, "StartPracticalBtn", "<b>START PRACTICAL  ></b>", new Vector2(0f, -165f), new Vector2(340f, 50f), ColorButtonBg, Color.white, 18f, () =>
        {
            StartPractical();
        });

        return screen;
    }

    // -------------------------------------------------------------------------
    // 2. APPARATUS STAGE (Workstation Dock: X=1.22m, Y=1.42m, Z=1.38m, Yaw 195°)
    // -------------------------------------------------------------------------
    private GameObject BuildApparatusScreen()
    {
        Vector3 pos = new Vector3(1.22f, 1.42f, 1.38f);
        Quaternion rot = Quaternion.Euler(4f, 195f, 0f);
        Vector2 size = new Vector2(560f, 430f);

        GameObject screen = CreateScreenPanel("Stage_2_Apparatus", pos, rot, size);

        // Stage Navigation Bar
        CreateStageNavBar(screen.transform, PracticalStage.Apparatus);

        // Header
        GameObject header = CreateBox(screen.transform, "Header", new Vector2(0f, 150f), new Vector2(520f, 38f), ColorSubPanel);
        AddOutline(header, ColorBorder, 1.5f);
        CreateText(header.transform, "Title", "<b><color=" + HexWhite + ">[STAGE 1] APPARATUS & MASS SELECTION</color></b>", 15.5f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(500f, 30f));

        // Card A: Quick Mass Selector Buttons
        GameObject massCard = CreateCard(screen.transform, "MassCard", new Vector2(0f, 75f), new Vector2(520f, 96f));
        CreateText(massCard.transform, "MassTitle", "<b><color=" + HexMauve + ">MASS SELECTION (CLICK TO ATTACH & BOUNCE)</color></b>", 13.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 28f), new Vector2(490f, 22f));

        float mBtnW = 76f;
        float mBtnH = 36f;
        float[] massOptions = new float[] { 0.050f, 0.100f, 0.150f, 0.200f, 0.250f };
        string[] massLabels = new string[] { "+50g", "+100g", "+150g", "+200g", "+250g" };

        for (int i = 0; i < massOptions.Length; i++)
        {
            float mVal = massOptions[i];
            string mLabel = massLabels[i];
            float posX = -205f + (i * 82f);

            CreateButton(massCard.transform, "BtnMass_" + mLabel, "<b>" + mLabel + "</b>", new Vector2(posX, -10f), new Vector2(mBtnW, mBtnH), ColorButtonBg, Color.white, 13.5f, () =>
            {
                if (simulator != null)
                {
                    simulator.SetAttachedWeight(mVal, mLabel, false, 0f);
                }
            });
        }

        // ? MYSTERY MASS Button
        CreateButton(massCard.transform, "BtnMass_Mystery", "<b>? MYSTERY</b>", new Vector2(210f, -10f), new Vector2(86f, mBtnH), ColorSubPanel, Color.white, 12.5f, () =>
        {
            float[] mystWeights = new float[] { 125f, 175f, 225f };
            string[] mystNames = new string[] { "Mystery Mass A", "Mystery Mass B", "Mystery Mass C" };
            int idx = mysteryCycleCounter % mystWeights.Length;
            mysteryCycleCounter++;

            if (simulator != null)
            {
                simulator.SetAttachedWeight(mystWeights[idx] / 1000f, mystNames[idx], true, mystWeights[idx]);
            }
        });

        // Card B: Physics Governing Laws
        GameObject theoryCard = CreateCard(screen.transform, "TheoryCard", new Vector2(0f, -22f), new Vector2(520f, 84f));
        CreateText(theoryCard.transform, "TheoryTitle", "<b><color=" + HexMauve + ">GOVERNING PHYSICS LAWS</color></b>", 13f, TextAlignmentOptions.TopLeft, new Vector2(0f, 24f), new Vector2(490f, 20f));
        string theoryText =
            "<b>1. Static Hooke's Law:</b>  F = M*g = k*x   ->   <b>k_static = (M*g) / x</b>\n" +
            "<b>2. Dynamic SHM:</b>  T = 2*pi*sqrt(M/k)   ->   <b>k_dynamic = (4*pi^2 * M) / T^2</b>";
        CreateText(theoryCard.transform, "TheoryBody", "<color=" + HexLavender + ">" + theoryText + "</color>", 12f, TextAlignmentOptions.TopLeft, new Vector2(0f, -12f), new Vector2(490f, 46f));

        // Card C: Instructions & Live Step Guide
        GameObject guideCard = CreateCard(screen.transform, "GuideCard", new Vector2(0f, -108f), new Vector2(520f, 76f));
        CreateText(guideCard.transform, "GuideTitle", "<b><color=" + HexMauve + ">ACTIVE INSTRUCTIONS</color></b>", 13f, TextAlignmentOptions.TopLeft, new Vector2(0f, 20f), new Vector2(490f, 20f));
        apparatusGuideText = CreateText(guideCard.transform, "GuideBody", "<color=" + HexSky + ">Select a mass button above or grab a weight from the table to begin!</color>", 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, -10f), new Vector2(490f, 38f));

        // Bottom Action Button: Go to Measurement
        CreateButton(screen.transform, "BtnProceedMeasure", "<b>PROCEED TO MEASUREMENT  ></b>", new Vector2(0f, -172f), new Vector2(300f, 38f), ColorButtonBg, Color.white, 14f, () =>
        {
            SetStage(PracticalStage.Measurement);
        });

        return screen;
    }

    // -------------------------------------------------------------------------
    // 3. MEASUREMENT STAGE (Workstation Dock: X=1.22m, Y=1.42m, Z=1.38m, Yaw 195°)
    // -------------------------------------------------------------------------
    private GameObject BuildMeasurementScreen()
    {
        Vector3 pos = new Vector3(1.22f, 1.42f, 1.38f);
        Quaternion rot = Quaternion.Euler(4f, 195f, 0f);
        Vector2 size = new Vector2(560f, 430f);

        GameObject screen = CreateScreenPanel("Stage_3_Measurement", pos, rot, size);

        // Stage Navigation Bar
        CreateStageNavBar(screen.transform, PracticalStage.Measurement);

        // Header
        GameObject header = CreateBox(screen.transform, "Header", new Vector2(0f, 150f), new Vector2(520f, 38f), ColorSubPanel);
        AddOutline(header, ColorBorder, 1.5f);
        CreateText(header.transform, "Title", "<b><color=" + HexWhite + ">[STAGE 2] LIVE STOPWATCH & READINGS</color></b>", 15.5f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(500f, 30f));

        // Card A: Digital Stopwatch
        GameObject timerCard = CreateCard(screen.transform, "TimerCard", new Vector2(0f, 85f), new Vector2(520f, 78f));
        CreateText(timerCard.transform, "Title", "<b><color=" + HexMauve + ">DIGITAL STOPWATCH (10 OSCILLATIONS)</color></b>", 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 22f), new Vector2(490f, 20f));
        timerDigitalText = CreateText(timerCard.transform, "TimerVal", "<b>00:00.00 s</b>", 25f, TextAlignmentOptions.Center, new Vector2(0f, -4f), new Vector2(490f, 32f));
        timerStatusBadgeText = CreateText(timerCard.transform, "TimerStatus", "<color=" + HexSky + ">STATUS: READY FOR MASS</color>", 11.5f, TextAlignmentOptions.Center, new Vector2(0f, -24f), new Vector2(490f, 18f));

        // Card B: 10 Oscillations Cycle Progress
        GameObject oscCard = CreateCard(screen.transform, "OscCard", new Vector2(0f, -2f), new Vector2(520f, 84f));
        CreateText(oscCard.transform, "Title", "<b><color=" + HexMauve + ">OSCILLATION CYCLE PROGRESS</color></b>", 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 24f), new Vector2(490f, 20f));
        oscillationCountText = CreateText(oscCard.transform, "OscCount", "<color=" + HexLavender + ">Completed: </color><b><color=" + HexMauve + ">0 / 10 Cycles</color></b>", 13.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 2f), new Vector2(490f, 22f));
        oscillationPeriodText = CreateText(oscCard.transform, "OscPeriod", "<color=" + HexSky + ">Period T: -- s  |  T^2: -- s^2</color>", 12f, TextAlignmentOptions.TopLeft, new Vector2(0f, -18f), new Vector2(490f, 20f));

        // Progress Bar
        GameObject progBg = CreateBox(oscCard.transform, "ProgBg", new Vector2(0f, -8f), new Vector2(480f, 6f), ColorBgDeep);
        GameObject progFill = CreateBox(progBg.transform, "ProgFill", new Vector2(0f, 0f), new Vector2(0f, 6f), ColorButtonBg);
        progressBarFill = progFill.GetComponent<RectTransform>();

        // Card C: Metre Scale Pointer Telemetry
        GameObject scaleCard = CreateCard(screen.transform, "ScaleCard", new Vector2(0f, -85f), new Vector2(520f, 70f));
        CreateText(scaleCard.transform, "Title", "<b><color=" + HexMauve + ">METRE SCALE EXTENSION POINTER</color></b>", 12.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 18f), new Vector2(490f, 18f));
        scalePointerReadoutText = CreateText(scaleCard.transform, "Readout", "<color=" + HexLavender + ">Pointer: 100.0 mm  |  Extension (x): +0.00 cm</color>", 12.5f, TextAlignmentOptions.Center, new Vector2(0f, -8f), new Vector2(490f, 34f));

        // Card D: Practical Controls
        GameObject ctrlCard = CreateCard(screen.transform, "CtrlCard", new Vector2(0f, -162f), new Vector2(520f, 68f));

        float btnW = 105f;
        float btnH = 34f;
        float btnY = -6f;

        // Button: START TRIAL
        CreateButton(ctrlCard.transform, "BtnStart", "<b>> START</b>", new Vector2(-185f, btnY), new Vector2(btnW, btnH), ColorButtonBg, Color.white, 12.5f, () =>
        {
            if (simulator != null) simulator.StartTrial();
        });

        // Button: PAUSE / RESUME
        GameObject pauseBtn = CreateButton(ctrlCard.transform, "BtnPause", "<b>|| PAUSE</b>", new Vector2(-65f, btnY), new Vector2(btnW, btnH), ColorSubPanel, Color.white, 12.5f, () =>
        {
            if (simulator != null)
            {
                if (simulator.currentState == ExperimentState.Running)
                {
                    simulator.PauseTrial();
                    if (pauseResumeBtnText != null) pauseResumeBtnText.text = "<b>> RESUME</b>";
                }
                else if (simulator.currentState == ExperimentState.Paused)
                {
                    simulator.ResumeTrial();
                    if (pauseResumeBtnText != null) pauseResumeBtnText.text = "<b>|| PAUSE</b>";
                }
            }
        });
        pauseResumeBtnText = pauseBtn.GetComponentInChildren<TextMeshProUGUI>();

        // Button: RESET TRIAL
        CreateButton(ctrlCard.transform, "BtnReset", "<b>O RESET</b>", new Vector2(55f, btnY), new Vector2(btnW, btnH), ColorSubPanel, Color.white, 12.5f, () =>
        {
            if (simulator != null)
            {
                simulator.ResetTrial();
                if (pauseResumeBtnText != null) pauseResumeBtnText.text = "<b>|| PAUSE</b>";
            }
        });

        // Button: VIEW OBSERVATIONS
        CreateButton(ctrlCard.transform, "BtnViewObs", "<b>OBSERVE ></b>", new Vector2(185f, btnY), new Vector2(btnW + 15f, btnH), ColorButtonBg, Color.white, 12.5f, () =>
        {
            SetStage(PracticalStage.Observation);
        });

        return screen;
    }

    // -------------------------------------------------------------------------
    // 4. OBSERVATION STAGE (Workstation Dock: X=1.22m, Y=1.42m, Z=1.38m, Yaw 195°)
    // -------------------------------------------------------------------------
    private GameObject BuildObservationScreen()
    {
        Vector3 pos = new Vector3(1.22f, 1.42f, 1.38f);
        Quaternion rot = Quaternion.Euler(4f, 195f, 0f);
        Vector2 size = new Vector2(560f, 430f);

        GameObject screen = CreateScreenPanel("Stage_4_Observation", pos, rot, size);

        // Stage Navigation Bar
        CreateStageNavBar(screen.transform, PracticalStage.Observation);

        // Header
        GameObject header = CreateBox(screen.transform, "Header", new Vector2(0f, 150f), new Vector2(520f, 38f), ColorSubPanel);
        AddOutline(header, ColorBorder, 1.5f);
        CreateText(header.transform, "Title", "<b><color=" + HexWhite + ">[STAGE 3] LABORATORY OBSERVATION TABLE</color></b>", 15.5f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(500f, 30f));

        // Card A: Observation Table
        GameObject tableCard = CreateCard(screen.transform, "TableCard", new Vector2(0f, 25f), new Vector2(520f, 200f));
        CreateText(tableCard.transform, "TableTitle", "<b><color=" + HexMauve + ">RECORDED EXPERIMENTAL TRIALS</color></b>", 13.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, 85f), new Vector2(490f, 20f));
        observationTableText = CreateText(tableCard.transform, "TableBody", "<color=" + HexSky + ">No trials recorded yet. Attach weights to record data!</color>", 11.5f, TextAlignmentOptions.TopLeft, new Vector2(0f, -10f), new Vector2(490f, 160f));

        // Card B: Summary Results
        GameObject resultsCard = CreateCard(screen.transform, "ResultsCard", new Vector2(0f, -100f), new Vector2(520f, 44f));
        observationCalculationsText = CreateText(resultsCard.transform, "ResBody", "<color=" + HexLavender + ">Awaiting completed trials...</color>", 12f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(500f, 36f));

        // Bottom Action Bar: Next Trial, View Graph, Clear
        GameObject bottomBar = CreateCard(screen.transform, "BottomBar", new Vector2(0f, -160f), new Vector2(520f, 60f));

        float btnW = 150f;
        float btnH = 36f;

        // Button: NEXT TRIAL
        CreateButton(bottomBar.transform, "BtnNextTrial", "<b>NEXT TRIAL  ></b>", new Vector2(-155f, 0f), new Vector2(btnW, btnH), ColorButtonBg, Color.white, 13f, () =>
        {
            if (simulator != null)
            {
                simulator.NextTrial();
                SetStage(PracticalStage.Measurement);
            }
        });

        // Button: VIEW GRAPH
        CreateButton(bottomBar.transform, "BtnViewGraph", "<b>VIEW GRAPH  ></b>", new Vector2(10f, 0f), new Vector2(btnW, btnH), ColorSubPanel, Color.white, 13f, () =>
        {
            SetStage(PracticalStage.Results);
        });

        // Button: CLEAR DATA
        CreateButton(bottomBar.transform, "BtnClear", "<b>CLEAR</b>", new Vector2(185f, 0f), new Vector2(110f, btnH), ColorSubPanel, Color.white, 12f, () =>
        {
            EnsureObservationTable();
            if (obsTable != null) obsTable.ClearTable();
            if (simulator != null) simulator.ResetTrial();
            RefreshAllDisplays();
        });

        return screen;
    }

    // -------------------------------------------------------------------------
    // 5. RESULTS STAGE (Workstation Dock: X=1.22m, Y=1.42m, Z=1.38m, Yaw 195°)
    // -------------------------------------------------------------------------
    private GameObject BuildResultsScreen()
    {
        Vector3 pos = new Vector3(1.22f, 1.42f, 1.38f);
        Quaternion rot = Quaternion.Euler(4f, 195f, 0f);
        Vector2 size = new Vector2(560f, 430f);

        GameObject screen = CreateScreenPanel("Stage_5_Results", pos, rot, size);

        // Stage Navigation Bar
        CreateStageNavBar(screen.transform, PracticalStage.Results);

        // Header
        GameObject header = CreateBox(screen.transform, "Header", new Vector2(0f, 154f), new Vector2(520f, 32f), ColorSubPanel);
        AddOutline(header, ColorBorder, 1.5f);
        CreateText(header.transform, "Title", "<b><color=" + HexWhite + ">[STAGE 4] SCIENTIFIC GRAPHICAL ANALYSIS & RESULTS</color></b>", 14.5f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(500f, 26f));

        // Sub-Navigation: Graph Mode Switcher (Static vs Dynamic)
        GameObject graphNavBar = CreateBox(screen.transform, "GraphNavBar", new Vector2(0f, 122f), new Vector2(520f, 28f), ColorBgDeep);
        AddOutline(graphNavBar, ColorBorder, 1f);

        GameObject btnStaticObj = CreateButton(graphNavBar.transform, "BtnGraphStatic", "<b>1. STATIC METHOD (F vs x)</b>",
            new Vector2(-130f, 0f), new Vector2(245f, 24f), ColorButtonBg, Color.white, 11.5f, () =>
        {
            SetGraphMode(ResultsGraphMode.StaticMethod);
        });
        btnStaticGraphImg = btnStaticObj.GetComponent<Image>();
        btnStaticGraphTxt = btnStaticObj.GetComponentInChildren<TextMeshProUGUI>();

        GameObject btnDynamicObj = CreateButton(graphNavBar.transform, "BtnGraphDynamic", "<b>2. DYNAMIC METHOD (T² vs M)</b>",
            new Vector2(130f, 0f), new Vector2(245f, 24f), ColorSubPanel, (Color)ColorTextMuted, 11.5f, () =>
        {
            SetGraphMode(ResultsGraphMode.DynamicMethod);
        });
        btnDynamicGraphImg = btnDynamicObj.GetComponent<Image>();
        btnDynamicGraphTxt = btnDynamicObj.GetComponentInChildren<TextMeshProUGUI>();

        // Card A: Scientific Visual Graph View
        GameObject graphCard = CreateCard(screen.transform, "GraphCard", new Vector2(0f, 18f), new Vector2(520f, 168f));
        resultsGraphText = CreateText(graphCard.transform, "GraphBody", "<color=" + HexSky + ">No graph data yet. Complete trials to plot slope!</color>", 11f, TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(505f, 156f));

        // Card B: Final Calculations & Accuracy
        GameObject resultsCard = CreateCard(screen.transform, "ResultsCard", new Vector2(0f, -80f), new Vector2(520f, 42f));
        resultsCalculationsText = CreateText(resultsCard.transform, "ResBody", "<color=" + HexLavender + ">Awaiting completed trials...</color>", 11.5f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(505f, 36f));

        // Card C: Mystery Mass Result Banner
        GameObject mysteryCard = CreateCard(screen.transform, "MysteryCard", new Vector2(0f, -124f), new Vector2(520f, 32f));
        resultsMysteryText = CreateText(mysteryCard.transform, "MysteryBody", "<color=" + HexSky + ">Mystery Mass: Attach an unknown body to calculate its mass!</color>", 11f, TextAlignmentOptions.Center, Vector2.zero, new Vector2(505f, 26f));

        // Bottom Action Bar: Back to Observation, Start New Trial
        GameObject bottomBar = CreateCard(screen.transform, "BottomBar", new Vector2(0f, -168f), new Vector2(520f, 40f));

        CreateButton(bottomBar.transform, "BtnBackObs", "<b>< OBSERVATION TABLE</b>", new Vector2(-125f, 0f), new Vector2(210f, 32f), ColorSubPanel, Color.white, 12f, () =>
        {
            SetStage(PracticalStage.Observation);
        });

        CreateButton(bottomBar.transform, "BtnNewTrial", "<b>ATTACH NEW MASS  ></b>", new Vector2(125f, 0f), new Vector2(210f, 32f), ColorButtonBg, Color.white, 12f, () =>
        {
            SetStage(PracticalStage.Apparatus);
        });

        return screen;
    }

    public void SetGraphMode(ResultsGraphMode mode)
    {
        currentGraphMode = mode;
        if (btnStaticGraphImg != null && btnDynamicGraphImg != null)
        {
            bool isStatic = (mode == ResultsGraphMode.StaticMethod);
            btnStaticGraphImg.color = isStatic ? ColorButtonBg : ColorSubPanel;
            if (btnStaticGraphTxt != null) btnStaticGraphTxt.color = isStatic ? Color.white : (Color)ColorTextMuted;

            btnDynamicGraphImg.color = !isStatic ? ColorButtonBg : ColorSubPanel;
            if (btnDynamicGraphTxt != null) btnDynamicGraphTxt.color = !isStatic ? Color.white : (Color)ColorTextMuted;
        }

        RefreshOutputTableDisplay();
    }

    public void UpdateResultsDisplays()
    {
        RefreshOutputTableDisplay();
    }

    // -------------------------------------------------------------------------
    // UNIFIED STAGE NAVIGATION BAR (Top of all workstation panels)
    // -------------------------------------------------------------------------
    private void CreateStageNavBar(Transform parent, PracticalStage activeStage)
    {
        GameObject navBar = CreateBox(parent, "StageNavBar", new Vector2(0f, 192f), new Vector2(520f, 34f), ColorBgDeep);
        AddOutline(navBar, ColorBorder, 1f);

        string[] tabLabels = new string[] { "1. APPARATUS", "2. MEASURE", "3. OBSERVE", "4. RESULTS" };
        PracticalStage[] tabStages = new PracticalStage[] {
            PracticalStage.Apparatus,
            PracticalStage.Measurement,
            PracticalStage.Observation,
            PracticalStage.Results
        };

        float tabW = 124f;
        float tabH = 26f;

        for (int i = 0; i < tabStages.Length; i++)
        {
            PracticalStage st = tabStages[i];
            string lbl = tabLabels[i];
            float posX = -190f + (i * 127f);

            bool isActive = (st == activeStage);
            Color32 btnColor = isActive ? ColorButtonBg : ColorSubPanel;
            Color32 textColor = isActive ? Color.white : ColorTextMuted;

            GameObject btnObj = CreateButton(navBar.transform, "NavTab_" + st.ToString(), "<b>" + lbl + "</b>",
                new Vector2(posX, 0f), new Vector2(tabW, tabH), btnColor, textColor, 11f, () =>
            {
                SetStage(st);
            });

            Image img = btnObj.GetComponent<Image>();
            TextMeshProUGUI txt = btnObj.GetComponentInChildren<TextMeshProUGUI>();

            if (img != null) navBtnImages.Add(img);
            if (txt != null) navBtnTexts.Add(txt);
            navBtnStages.Add(st);
        }
    }

    // =========================================================================
    // UPDATE CYCLES & DATA BINDING
    // =========================================================================
    private void OnSimulatorTick()
    {
        if (simulator == null) return;

        // Measurement Screen: Stopwatch digital readout
        if (timerDigitalText != null)
        {
            float t = simulator.ElapsedTrialTimer;
            int mins = (int)(t / 60f);
            float secs = t % 60f;
            timerDigitalText.text = string.Format("<b><color=" + HexWhite + ">{0:00}:{1:00.00} s</color></b>", mins, secs);
        }

        // Measurement Screen: Status badge
        if (timerStatusBadgeText != null)
        {
            string st = simulator.currentState switch
            {
                ExperimentState.Running => "<color=" + HexMauve + ">● LIVE OSCILLATIONS IN PROGRESS</color>",
                ExperimentState.Paused  => "<color=" + HexLavender + ">❚❚ TIMING PAUSED</color>",
                ExperimentState.Completed => "<color=" + HexLavender + ">[OK] 10 OSCILLATIONS COMPLETE</color>",
                _ => "<color=" + HexSky + ">READY (ATTACH WEIGHT OR CLICK START)</color>"
            };
            timerStatusBadgeText.text = st;
        }

        // Measurement Screen: Oscillation count & period
        if (oscillationCountText != null)
        {
            oscillationCountText.text = string.Format(
                "<color=" + HexLavender + ">Completed: </color><b><color=" + HexMauve + ">{0} / {1} Cycles</color></b>",
                simulator.currentOscillations, simulator.targetOscillations
            );
        }

        if (oscillationPeriodText != null)
        {
            float t = simulator.ElapsedTrialTimer;
            float T = simulator.currentOscillations > 0 ? (t / simulator.currentOscillations) : 0f;
            float T2 = T * T;
            oscillationPeriodText.text = string.Format(
                "<color=" + HexSky + ">Live Period T: <b>{0:F3}s</b>  |  T^2: <b>{1:F3}s^2</b>  |  Load: <b>{2:F0}g</b></color>",
                T, T2, simulator.TotalMassGrams
            );
        }

        // Measurement Screen: Progress bar fill
        if (progressBarFill != null)
        {
            float progress = (float)simulator.currentOscillations / (float)simulator.targetOscillations;
            progress = Mathf.Clamp01(progress);
            progressBarFill.sizeDelta = new Vector2(480f * progress, 6f);
        }

        // Measurement Screen: Metre scale readout
        if (scalePointerReadoutText != null)
        {
            float curMm = simulator.CurrentMm;
            float extCm = simulator.currentExtensionCm;
            float extM = simulator.ExtensionMeters;
            scalePointerReadoutText.text = string.Format(
                "<color=" + HexLavender + ">Pointer: <b>{0:F1} mm</b> ({1:F2} cm)   |   Net Extension (x): <b><color=" + HexMauve + ">+{2:F2} cm</color></b> (+{3:F4} m)</color>",
                curMm, (curMm / 10f), extCm, extM
            );
        }

        // Apparatus Screen: Live guidance
        if (apparatusGuideText != null)
        {
            float massG = simulator.TotalMassGrams;
            if (simulator.currentState == ExperimentState.Running)
            {
                apparatusGuideText.text = "<color=" + HexLavender + ">Measuring 10 cycles for <b><color=" + HexMauve + ">" + simulator.activeWeightLabel + " (" + massG.ToString("F0") + "g)</color></b>. Watch stopwatch!</color>";
            }
            else
            {
                apparatusGuideText.text = "<color=" + HexSky + ">Active Load: <b>" + simulator.activeWeightLabel + " (" + massG.ToString("F0") + "g)</b>. Select a mass or grab one from table.</color>";
            }
        }
    }

    private void OnDataChanged()
    {
        RefreshOutputTableDisplay();
    }

    public void RefreshAllDisplays()
    {
        OnSimulatorTick();
        RefreshOutputTableDisplay();
    }

    private void RefreshOutputTableDisplay()
    {
        EnsureObservationTable();
        if (obsTable == null) return;

        var readings = obsTable.AllReadings;

        // 1. Observation Table String
        if (observationTableText != null)
        {
            if (readings == null || readings.Count == 0)
            {
                observationTableText.text = "<color=" + HexSky + ">No trials recorded yet.\nHang a weight or click a quick mass button to log your first trial!</color>";
            }
            else
            {
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("<b><color=" + HexLavender + ">TR#   LOAD         EXT(cm)   TIME(s)   PERIOD(s)   T^2(s^2)   k_dyn</color></b>");
                sb.AppendLine("<color=" + HexDenim + ">─────────────────────────────────────────────────────────────────</color>");
                foreach (var r in readings)
                {
                    string loadStr = !string.IsNullOrEmpty(r.massLabel) ? r.massLabel : r.massGrams.ToString("F0") + "g";
                    if (loadStr.Length > 12) loadStr = loadStr.Substring(0, 12);

                    sb.AppendLine(string.Format(
                        "<color=" + HexLavender + ">#{0,-4}</color> <color=" + HexSky + ">{1,-12}</color> <color=" + HexLavender + ">{2,-9:F2}</color> <color=" + HexLavender + ">{3,-9:F2}</color> <color=" + HexSky + ">{4,-11:F3}</color> <color=" + HexLavender + ">{5,-8:F3}</color> <color=" + HexMauve + ">{6,-7:F1}</color>",
                        r.trialNumber, loadStr, r.extensionCm, r.totalTimeSeconds, r.timePeriodT, r.tSquared, r.calculatedK
                    ));
                }
                observationTableText.text = sb.ToString();
            }
        }

        // Calculations summary string
        float dynK = obsTable.CalculateMeanDynamicK();
        float statK = obsTable.CalculateMeanStaticK();

        string obsCalcStr =
            "<color=" + HexLavender + ">Dynamic k: <b><color=" + HexMauve + ">" + ((dynK > 0f) ? dynK.ToString("F2") + " N/m" : "25.00 N/m") + "</color></b>   |   " +
            "Static k: <b><color=" + HexMauve + ">" + ((statK > 0f) ? statK.ToString("F2") + " N/m" : "25.00 N/m") + "</color></b>   |   " +
            "Theoretical: <b><color=" + HexWhite + ">25.00 N/m</color></b></color>";

        if (observationCalculationsText != null)
            observationCalculationsText.text = obsCalcStr;

        // 2. Results Screen: Mode-specific Graph String & Calculations Card
        if (resultsGraphText != null)
        {
            if (currentGraphMode == ResultsGraphMode.StaticMethod)
            {
                resultsGraphText.text = obsTable.GetFormattedStaticGraphString();
            }
            else
            {
                resultsGraphText.text = obsTable.GetFormattedDynamicGraphString();
            }
        }

        if (resultsCalculationsText != null)
        {
            if (currentGraphMode == ResultsGraphMode.StaticMethod)
            {
                float kStat = obsTable.CalculateStaticSlope();
                float statDiff = Mathf.Abs(kStat - 25.0f) / 25.0f * 100f;
                resultsCalculationsText.text = string.Format(
                    "<b><color=" + HexMauve + ">STATIC METHOD:</color></b> <color=" + HexLavender + ">Gradient (k_static) = <b><color=" + HexMauve + ">{0:F2} N/m</color></b>  |  Theory: <b>25.00 N/m</b>  |  Diff: <b><color=" + HexSky + ">{1:F1}%</color></b>  |  Hooke's Law: F = k·x</color>",
                    kStat, statDiff
                );
            }
            else
            {
                float slope = obsTable.CalculateDynamicSlope();
                float kDyn = obsTable.CalculateDynamicGraphicalK();
                float dynDiff = Mathf.Abs(kDyn - 25.0f) / 25.0f * 100f;
                resultsCalculationsText.text = string.Format(
                    "<b><color=" + HexMauve + ">DYNAMIC METHOD:</color></b> <color=" + HexLavender + ">Slope S = <b><color=" + HexMauve + ">{0:F3} s²/kg</color></b>  |  k_dyn = <b><color=" + HexMauve + ">{1:F2} N/m</color></b>  |  Theory: <b>25.00 N/m</b>  |  Diff: <b><color=" + HexSky + ">{2:F1}%</color></b></color>",
                    slope, kDyn, dynDiff
                );
            }
        }

        // 3. Results Screen: Mystery Mass result banner
        if (resultsMysteryText != null)
        {
            PracticalReading latestMystery = null;
            if (readings != null)
            {
                for (int i = readings.Count - 1; i >= 0; i--)
                {
                    if (readings[i].isMystery)
                    {
                        latestMystery = readings[i];
                        break;
                    }
                }
            }

            if (latestMystery != null)
            {
                float err = Mathf.Abs(latestMystery.calculatedMassGrams - latestMystery.actualMassGrams) / latestMystery.actualMassGrams * 100f;
                resultsMysteryText.text = string.Format(
                    "<b><color=" + HexMauve + ">[MYSTERY IDENTIFIED]</color></b> <color=" + HexLavender + ">{0} -> Period T = <b>{1:F3}s</b> -> <b>Calculated Mass = {2:F1}g</b> (Actual: {3:F0}g | Error: {4:F1}%)</color>",
                    latestMystery.massLabel, latestMystery.timePeriodT, latestMystery.calculatedMassGrams, latestMystery.actualMassGrams, err
                );
            }
            else
            {
                resultsMysteryText.text = "<color=" + HexSky + ">Mystery Mass: Attach an unknown body to calculate its mass!</color>";
            }
        }
    }

    // =========================================================================
    // UI BUILDER HELPERS
    // =========================================================================

    private GameObject CreateScreenPanel(string name, Vector3 worldPos, Quaternion worldRot, Vector2 sizeDelta)
    {
        GameObject screen = new GameObject(name);
        screen.transform.SetParent(this.transform, false);

        screen.transform.position = worldPos;
        screen.transform.rotation = worldRot;
        screen.transform.localScale = Vector3.one;

        RectTransform rt = screen.AddComponent<RectTransform>();
        rt.sizeDelta = sizeDelta;

        Image img = screen.AddComponent<Image>();
        img.color = ColorBgDeep;

        AddOutline(screen, ColorBorder, 2f);

        return screen;
    }

    private GameObject CreateCard(Transform parent, string name, Vector2 pos, Vector2 size)
    {
        GameObject card = new GameObject(name);
        card.transform.SetParent(parent, false);

        RectTransform rt = card.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = card.AddComponent<Image>();
        img.color = ColorCardBg;

        AddOutline(card, ColorBorder, 1.2f);

        return card;
    }

    private GameObject CreateBox(Transform parent, string name, Vector2 pos, Vector2 size, Color32 color)
    {
        GameObject box = new GameObject(name);
        box.transform.SetParent(parent, false);

        RectTransform rt = box.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = box.AddComponent<Image>();
        img.color = color;

        return box;
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string content, float fontSize,
        TextAlignmentOptions alignment, Vector2 pos, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        if (baseFont != null) tmp.font = baseFont;
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.richText = true;
        tmp.text = content;

        return tmp;
    }

    private GameObject CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size,
        Color32 bgColor, Color32 textColor, float fontSize, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = bgColor;
        cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
        cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.25f);
        btn.colors = cb;

        AddOutline(btnObj, ColorBorder, 1.2f);

        CreateText(btnObj.transform, "Label", label, fontSize, TextAlignmentOptions.Center, Vector2.zero, size);

        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        return btnObj;
    }

    private void AddOutline(GameObject target, Color32 color, float width)
    {
        Outline outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(width, -width);
    }
}
