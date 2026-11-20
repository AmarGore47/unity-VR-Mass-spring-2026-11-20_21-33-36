using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[System.Serializable]
public class PracticalReading
{
    public int trialNumber;
    public string massLabel = "";
    public bool isMystery = false;
    public float actualMassGrams = 0f;
    public float calculatedMassGrams = 0f;
    
    // Mass properties (SI: kg, Lab: g)
    public float massGrams;          // Total mass in grams (e.g. 50g, 100g, 150g, 200g, 250g)
    public float massKg;             // Total mass in kg (0.050, 0.100, 0.150, 0.200, 0.250)
    public float hangerMassGrams;    // Base hanger
    public float slottedMassGrams;   // Attached slotted weights
    
    // Static Hooke's Law properties
    public float initialLengthCm;    // L0 (cm)
    public float finalLengthCm;      // L (cm)
    public float extensionCm;        // x = L - L0 (cm)
    public float extensionMeters;    // x in meters (SI)
    public float appliedForceN;      // F = M * g in Newtons (SI)
    public float staticK;            // k = F / x in N/m (SI)
    public bool hasStaticData;
    
    // Dynamic SHM properties
    public int oscillationCount;     // 10 complete cycles
    public float totalTimeSeconds;   // t10 (s)
    public float timePeriodT;        // T = t / 10 (s)
    public float tSquared;           // T^2 (s^2)
    public float calculatedK;        // k = (4 * pi^2 * M) / T^2 (N/m)
    public bool hasDynamicData;
}

/// <summary>
/// Stores experimental observations across static and dynamic stages with SI validation.
/// Theme: Jarar Malik 6-Color Palette:
/// #0E1F2F (Deep Charcoal Navy) | #0E1B48 (Midnight Navy) | #27425D (Slate Denim)
/// #87A7D0 (Sky Blue)           | #C18DB4 (Soft Orchid)   | #E2CAD8 (Pale Lavender)
/// </summary>
public class SpringObservationTable : MonoBehaviour
{
    public static SpringObservationTable Instance { get; private set; }

    [Header("UI References")]
    public TextMeshProUGUI tableDisplayText;
    public TextMeshProUGUI kResultDisplayText;
    public TextMeshProUGUI stepGuideDisplayText;

    public const float THEORETICAL_K = 25.0f;
    public const float GRAVITY = 9.81f;

    // Palette hex codes
    public const string HexDeep     = "#0E1F2F";
    public const string HexNavy     = "#0E1B48";
    public const string HexDenim    = "#27425D";
    public const string HexSky      = "#87A7D0";
    public const string HexMauve    = "#C18DB4";
    public const string HexLavender = "#E2CAD8";

    private readonly List<PracticalReading> readings = new List<PracticalReading>();
    private int activeTrial = 1;

    public int CurrentTrial => Mathf.Max(1, activeTrial);
    public int RecordedCount => readings.Count;
    public List<PracticalReading> AllReadings => readings;
    public List<PracticalReading> GetReadings() => readings;

    public event Action OnDataChanged;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(this);
    }

    void Start()
    {
        UpdateAllDisplays();
    }

    /// <summary>
    /// Records static elongation measurement for the active trial.
    /// </summary>
    public void RecordStaticReading(int trialNum, float totalMassGrams, float hangerGrams, float slottedGrams,
        float l0Cm, float lFinalCm, float extCm, float forceN, float kStatic, string label = "", bool isMystery = false)
    {
        int validTrial = Mathf.Max(1, trialNum);
        PracticalReading r = GetOrCreateReading(validTrial, totalMassGrams);
        r.massGrams = totalMassGrams;
        r.massKg = totalMassGrams / 1000f;
        r.massLabel = !string.IsNullOrEmpty(label) ? label : totalMassGrams.ToString("F0") + "g";
        r.isMystery = isMystery;
        r.hangerMassGrams = hangerGrams;
        r.slottedMassGrams = slottedGrams;
        r.initialLengthCm = l0Cm;
        r.finalLengthCm = lFinalCm;
        r.extensionCm = extCm;
        r.extensionMeters = extCm / 100f;
        r.appliedForceN = forceN;
        r.staticK = kStatic;
        r.hasStaticData = true;

        UpdateAllDisplays();
        OnDataChanged?.Invoke();
    }

    /// <summary>
    /// Records dynamic oscillation measurement for the active trial.
    /// </summary>
    public void RecordReading(float massGrams, float extensionCm, float totalTime, int oscCount, float kTheoretical,
        string label = "", bool isMystery = false, float mysteryActualGrams = 0f)
    {
        int validTrial = Mathf.Max(1, activeTrial);
        PracticalReading r = GetOrCreateReading(validTrial, massGrams);

        float mKg = massGrams / 1000f;
        float t = oscCount > 0 ? (totalTime / oscCount) : 0f;
        float t2 = t * t;
        float kDyn = (t2 > 0.0001f) ? (4f * Mathf.PI * Mathf.PI * mKg / t2) : kTheoretical;

        r.massGrams = massGrams;
        r.massKg = mKg;
        r.massLabel = !string.IsNullOrEmpty(label) ? label : massGrams.ToString("F0") + "g";
        r.isMystery = isMystery;
        r.actualMassGrams = mysteryActualGrams > 0.001f ? mysteryActualGrams : massGrams;

        // Calculate unknown mass from T and k: M = (k * T^2) / (4 * pi^2)
        if (isMystery)
        {
            float calcKg = (kTheoretical * t2) / (4f * Mathf.PI * Mathf.PI);
            r.calculatedMassGrams = calcKg * 1000f;
        }

        r.extensionCm = extensionCm;
        r.extensionMeters = extensionCm / 100f;
        r.oscillationCount = oscCount;
        r.totalTimeSeconds = totalTime;
        r.timePeriodT = t;
        r.tSquared = t2;
        r.calculatedK = kDyn;
        r.hasDynamicData = true;

        if (!r.hasStaticData && extensionCm > 0.001f)
        {
            r.initialLengthCm = 38.0f;
            r.finalLengthCm = 38.0f + extensionCm;
            r.appliedForceN = mKg * GRAVITY;
            r.staticK = r.appliedForceN / (extensionCm / 100f);
            r.hasStaticData = true;
        }

        // Advance to next trial after completed timing
        activeTrial = readings.Count + 1;

        UpdateAllDisplays();
        OnDataChanged?.Invoke();
    }

    public PracticalReading GetOrCreateReading(int trialNum, float massGrams)
    {
        int validTrial = Mathf.Max(1, trialNum);
        for (int i = 0; i < readings.Count; i++)
        {
            if (readings[i].trialNumber == validTrial)
            {
                return readings[i];
            }
        }

        PracticalReading newR = new PracticalReading
        {
            trialNumber = validTrial,
            massGrams = massGrams,
            massKg = massGrams / 1000f
        };
        readings.Add(newR);
        return newR;
    }

    public void SetActiveTrial(int trialNum)
    {
        activeTrial = Mathf.Max(1, trialNum);
        UpdateAllDisplays();
        OnDataChanged?.Invoke();
    }

    public void AdvanceTrialNumber()
    {
        activeTrial = readings.Count + 1;
        UpdateAllDisplays();
        OnDataChanged?.Invoke();
    }

    public void ClearTable()
    {
        readings.Clear();
        activeTrial = 1;
        UpdateAllDisplays();
        OnDataChanged?.Invoke();
    }

    public float CalculateMeanDynamicK()
    {
        if (readings.Count == 0) return 0f;
        float sumK = 0f;
        int valid = 0;
        foreach (var r in readings)
        {
            if (r != null && r.hasDynamicData && r.calculatedK > 1f)
            {
                sumK += r.calculatedK;
                valid++;
            }
        }
        return valid > 0 ? (sumK / valid) : 0f;
    }

    public float GetAverageDynamicK() => CalculateMeanDynamicK();

    public float CalculateMeanStaticK()
    {
        if (readings.Count == 0) return 0f;
        float sumK = 0f;
        int valid = 0;
        foreach (var r in readings)
        {
            if (r != null && r.hasStaticData && r.staticK > 1f)
            {
                sumK += r.staticK;
                valid++;
            }
        }
        return valid > 0 ? (sumK / valid) : 0f;
    }

    public float GetAverageStaticK() => CalculateMeanStaticK();

    public float CalculatePercentError()
    {
        float mean = CalculateMeanDynamicK();
        if (mean <= 0.001f) mean = CalculateMeanStaticK();
        if (mean <= 0.001f) return 0f;
        return Mathf.Abs(mean - THEORETICAL_K) / THEORETICAL_K * 100f;
    }

    public float CalculateSlope() => CalculateDynamicSlope();

    public float CalculateDynamicSlope()
    {
        if (readings.Count == 0) return (4f * Mathf.PI * Mathf.PI) / THEORETICAL_K;

        float sumM2 = 0f;
        float sumMT2 = 0f;
        int valid = 0;

        foreach (var r in readings)
        {
            if (r != null && r.hasDynamicData && r.tSquared > 0.001f)
            {
                float mKg = r.massKg;
                sumM2 += mKg * mKg;
                sumMT2 += mKg * r.tSquared;
                valid++;
            }
        }

        if (valid > 0 && sumM2 > 0.00001f)
        {
            return sumMT2 / sumM2;
        }

        return (4f * Mathf.PI * Mathf.PI) / THEORETICAL_K;
    }

    public float CalculateDynamicGraphicalK()
    {
        float slope = CalculateDynamicSlope();
        if (slope > 0.0001f)
        {
            return (4f * Mathf.PI * Mathf.PI) / slope;
        }
        return THEORETICAL_K;
    }

    public float CalculateGraphicalK() => CalculateDynamicGraphicalK();

    public float CalculateStaticSlope()
    {
        if (readings.Count == 0) return THEORETICAL_K;

        float sumX2 = 0f;
        float sumXF = 0f;
        int valid = 0;

        foreach (var r in readings)
        {
            if (r != null && r.hasStaticData && r.extensionCm > 0.01f)
            {
                float xMetres = r.extensionCm / 100f; // Metres
                float forceN = r.appliedForceN;       // Newtons
                sumX2 += xMetres * xMetres;
                sumXF += xMetres * forceN;
                valid++;
            }
        }

        if (valid > 0 && sumX2 > 0.000001f)
        {
            return sumXF / sumX2; // Gradient = k in N/m
        }

        return THEORETICAL_K;
    }

    public float CalculateStaticGraphicalK() => CalculateStaticSlope();

    // =========================================================================
    // GRAPH FORMATTERS (STATIC & DYNAMIC)
    // =========================================================================

    public string GetFormattedGraphString() => GetFormattedDynamicGraphString();

    /// <summary>
    /// Static Graph: Force F (N) vs Extension x (m) following Hooke's Law: F = k * x
    /// </summary>
    public string GetFormattedStaticGraphString()
    {
        if (readings.Count == 0)
        {
            return "<color=" + HexSky + ">[STATIC METHOD: F vs x GRAPH]\nNo experimental points recorded yet.\nAttach masses (50g - 250g) to measure extension x and plot Hooke's Law slope!</color>";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><color=" + HexMauve + ">STATIC GRAPH: APPLIED FORCE (F) vs EXTENSION (x)</color></b>");
        sb.AppendLine("<color=" + HexSky + ">Hooke's Law: F = k·x   |   Gradient = k_static = ΔF / Δx   [SI: N/m]</color>");
        sb.AppendLine("<b><color=" + HexLavender + ">F (N)</color></b>");

        // Y-axis levels: 2.50N, 2.00N, 1.50N, 1.00N, 0.50N
        float[] yLevels = new float[] { 2.50f, 2.00f, 1.50f, 1.00f, 0.50f };
        for (int y = 0; y < yLevels.Length; y++)
        {
            float yVal = yLevels[y];
            sb.Append(string.Format("<b><color=" + HexLavender + ">{0:F2}</color></b> <color=" + HexSky + ">│</color> ", yVal));

            string pointLabel = "";
            foreach (var r in readings)
            {
                if (r.hasStaticData && Mathf.Abs(r.appliedForceN - yVal) < 0.28f)
                {
                    float xM = r.extensionCm / 100f;
                    pointLabel += string.Format("<b><color=" + HexMauve + ">● [{0}: x={1:F3}m, F={2:F2}N]</color></b>  ", r.massLabel, xM, r.appliedForceN);
                }
            }
            sb.AppendLine(pointLabel);
        }

        sb.AppendLine("<b><color=" + HexLavender + ">0.00</color></b> <color=" + HexSky + ">└───┬─────────┬─────────┬─────────┬─────────┬─────────►</color> <b><color=" + HexLavender + ">x (m)</color></b>");
        sb.AppendLine("     <color=" + HexSky + ">0.02      0.04      0.06      0.08      0.10</color>");

        float slopeK = CalculateStaticSlope();
        float pctDiff = Mathf.Abs(slopeK - THEORETICAL_K) / THEORETICAL_K * 100f;

        sb.AppendLine(string.Format(
            "<b><color=" + HexLavender + ">Gradient (k_static):</color></b> <color=" + HexMauve + "><b>{0:F2} N/m</b></color>  |  " +
            "<b><color=" + HexLavender + ">Theoretical:</color></b> <b>{1:F1} N/m</b>  |  " +
            "<b><color=" + HexLavender + ">Difference:</color></b> <color=" + HexSky + "><b>{2:F1}%</b></color>",
            slopeK, THEORETICAL_K, pctDiff
        ));

        return sb.ToString();
    }

    /// <summary>
    /// Dynamic Graph: Period Squared T^2 (s^2) vs Suspended Mass M (kg) following SHM: T^2 = (4*pi^2/k) * M
    /// </summary>
    public string GetFormattedDynamicGraphString()
    {
        if (readings.Count == 0)
        {
            return "<color=" + HexSky + ">[DYNAMIC METHOD: T^2 vs M GRAPH]\nNo experimental points recorded yet.\nAttach masses (50g - 250g) and oscillate 10 cycles to plot the SHM linear slope!</color>";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><color=" + HexMauve + ">DYNAMIC GRAPH: PERIOD SQUARED (T²) vs SUSPENDED MASS (M)</color></b>");
        sb.AppendLine("<color=" + HexSky + ">SHM Linear Relation: T² = (4π²/k)·M   |   Slope S = 4π²/k   |   k = 4π²/S</color>");
        sb.AppendLine("<b><color=" + HexLavender + ">T² (s²)</color></b>");

        // Y-axis levels: 0.40s^2, 0.30s^2, 0.20s^2, 0.10s^2
        float[] yLevels = new float[] { 0.40f, 0.30f, 0.20f, 0.10f };
        for (int y = 0; y < yLevels.Length; y++)
        {
            float yVal = yLevels[y];
            sb.Append(string.Format("<b><color=" + HexLavender + ">{0:F2}</color></b> <color=" + HexSky + ">│</color> ", yVal));

            string pointLabel = "";
            foreach (var r in readings)
            {
                if (r.hasDynamicData && Mathf.Abs(r.tSquared - yVal) < 0.055f)
                {
                    pointLabel += string.Format("<b><color=" + HexMauve + ">● [{0}: M={1:F3}kg, T²={2:F3}s²]</color></b>  ", r.massLabel, r.massKg, r.tSquared);
                }
            }
            sb.AppendLine(pointLabel);
        }

        sb.AppendLine("<b><color=" + HexLavender + ">0.00</color></b> <color=" + HexSky + ">└───┬─────────┬─────────┬─────────┬─────────┬─────────►</color> <b><color=" + HexLavender + ">M (kg)</color></b>");
        sb.AppendLine("     <color=" + HexSky + ">0.05      0.10      0.15      0.20      0.25</color>");

        float slope = CalculateDynamicSlope();
        float kDyn = CalculateDynamicGraphicalK();
        float pctDiff = Mathf.Abs(kDyn - THEORETICAL_K) / THEORETICAL_K * 100f;

        sb.AppendLine(string.Format(
            "<b><color=" + HexLavender + ">Slope S:</color></b> <color=" + HexMauve + "><b>{0:F3} s²/kg</b></color>  |  " +
            "<b><color=" + HexLavender + ">k_dyn (4π²/S):</color></b> <color=" + HexMauve + "><b>{1:F2} N/m</b></color>  |  " +
            "<b><color=" + HexLavender + ">Theoretical:</color></b> <b>{2:F1} N/m</b>  |  " +
            "<b><color=" + HexLavender + ">Difference:</color></b> <color=" + HexSky + "><b>{3:F1}%</b></color>",
            slope, kDyn, THEORETICAL_K, pctDiff
        ));

        return sb.ToString();
    }

    public void UpdateAllDisplays()
    {
        UpdateTableUI();
        UpdateResultsUI();
        UpdateStepGuideUI();
    }

    private void UpdateTableUI()
    {
        if (tableDisplayText == null) return;

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("<b><color=" + HexSky + ">─────────┬─────────┬─────────┬─────────┬─────────┬─────────┬─────────┐</color></b>");
        sb.AppendLine("<b><color=" + HexLavender + ">TR#</color><color=" + HexSky + ">│</color><color=" + HexLavender + ">MASS(kg)</color><color=" + HexSky + ">│</color><color=" + HexLavender + ">EXT x(m)</color> <color=" + HexSky + ">│</color><color=" + HexLavender + ">FORCE(N)</color> <color=" + HexSky + ">│</color><color=" + HexLavender + ">k_stat</color>   <color=" + HexSky + ">│</color><color=" + HexLavender + ">TIME t(s)</color><color=" + HexSky + ">│</color><color=" + HexLavender + ">PERIOD(s)</color><color=" + HexSky + ">│</color><color=" + HexLavender + ">k_dyn</color>     <color=" + HexSky + ">│</color></b>");
        sb.AppendLine("<b><color=" + HexSky + ">─────────┼─────────┼─────────┼─────────┼─────────┼─────────┼─────────┤</color></b>");

        int displayRows = Mathf.Max(4, readings.Count);
        for (int i = 0; i < displayRows; i++)
        {
            string trLabel = string.Format("#{0} ", i + 1);

            if (i < readings.Count && readings[i] != null)
            {
                var r = readings[i];
                string extStr = r.extensionMeters > 0.0001f ? string.Format("{0:F4}m", r.extensionMeters) : string.Format("{0:F1}cm", r.extensionCm);
                string forceStr = r.appliedForceN > 0.001f ? string.Format("{0:F3}N", r.appliedForceN) : "--- ";
                string statKStr = r.hasStaticData ? string.Format("{0:F1}", r.staticK) : "--- ";
                string timeStr = r.hasDynamicData ? string.Format("{0:F2}s", r.totalTimeSeconds) : "--- ";
                string periodStr = r.hasDynamicData ? string.Format("{0:F3}s", r.timePeriodT) : "--- ";
                string dynKStr = r.hasDynamicData ? string.Format("{0:F1}", r.calculatedK) : "--- ";

                sb.AppendLine(string.Format("<b><color=" + HexSky + ">│</color></b><color=" + HexLavender + ">{0}</color><b><color=" + HexSky + ">│</color></b><color=" + HexSky + ">{1,7:F3}kg</color><b><color=" + HexSky + ">│</color></b><color=" + HexLavender + ">{2,8}</color> <b><color=" + HexSky + ">│</color></b><color=" + HexSky + ">{3,8}</color><b><color=" + HexSky + ">│</color></b><color=" + HexMauve + ">{4,8}</color> <b><color=" + HexSky + ">│</color></b><color=" + HexLavender + ">{5,8}</color><b><color=" + HexSky + ">│</color></b><color=" + HexSky + ">{6,8}</color><b><color=" + HexSky + ">│</color></b><color=" + HexMauve + ">{7,8}</color> <b><color=" + HexSky + ">│</color></b>",
                    trLabel, r.massKg, extStr, forceStr, statKStr, timeStr, periodStr, dynKStr));
            }
            else
            {
                sb.AppendLine(string.Format("<b><color=" + HexSky + ">│</color></b><color=" + HexSky + ">{0}</color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>  <color=" + HexSky + ">---    </color><b><color=" + HexSky + ">│</color></b>", trLabel));
            }
        }

        sb.AppendLine("<b><color=" + HexSky + ">└───┴─────────┴─────────┴─────────┴─────────┴─────────┴─────────┴─────────┘</color></b>");
        tableDisplayText.text = sb.ToString();
    }

    private void UpdateResultsUI()
    {
        if (kResultDisplayText == null) return;

        if (readings.Count == 0)
        {
            kResultDisplayText.text = "<b><color=" + HexLavender + ">SPRING CONSTANT (k):</color></b> <color=" + HexSky + ">Awaiting Trial 1</color>\n<color=" + HexSky + ">Theoretical Reference: 25.0 N/m</color>";
            return;
        }

        float avgDynK = CalculateMeanDynamicK();
        float avgStatK = CalculateMeanStaticK();
        float percentError = CalculatePercentError();
        float slopeK = CalculateGraphicalK();

        kResultDisplayText.text = string.Format(
            "<b><color=" + HexLavender + ">STATIC k (Mean):</color></b> <color=" + HexMauve + "><b>{0:F2} N/m</b></color>   |   <b><color=" + HexLavender + ">DYNAMIC k (Mean):</color></b> <color=" + HexMauve + "><b>{1:F2} N/m</b></color>   |   <b><color=" + HexLavender + ">THEORETICAL:</color></b> <color=" + HexLavender + "><b>{2:F1} N/m</b></color>\n" +
            "<b><color=" + HexLavender + ">GRAPHICAL k (Slope):</color></b> <color=" + HexMauve + "><b>{3:F2} N/m</b></color>   |   <b><color=" + HexLavender + ">ACCURACY:</color></b> <color=" + HexLavender + "><b>{4:F1}% match</b></color> <color=" + HexSky + ">(Relative Error: {5:F1}%)</color>",
            (avgStatK > 0.1f ? avgStatK : THEORETICAL_K),
            (avgDynK > 0.1f ? avgDynK : THEORETICAL_K),
            THEORETICAL_K,
            slopeK,
            (100f - percentError),
            percentError
        );
    }

    private void UpdateStepGuideUI()
    {
        if (stepGuideDisplayText == null) return;

        stepGuideDisplayText.text = readings.Count switch
        {
            0 => "<color=" + HexMauve + "><b>[TRIAL 1]</b></color> <color=" + HexLavender + ">Attach <b>50g mass</b> to hook. Observe ~1.96cm extension & time 10 cycles (~2.81s).</color>",
            1 => "<color=" + HexMauve + "><b>[TRIAL 2]</b></color> <color=" + HexLavender + ">Attach <b>100g mass</b>. Extension increases to ~3.92cm & period rises to ~3.97s.</color>",
            2 => "<color=" + HexMauve + "><b>[TRIAL 3]</b></color> <color=" + HexLavender + ">Attach <b>150g mass</b>. Extension is ~5.89cm & 10 cycles time is ~4.87s.</color>",
            3 => "<color=" + HexMauve + "><b>[TRIAL 4]</b></color> <color=" + HexLavender + ">Attach <b>200g mass</b>. Extension reaches ~7.85cm & 10 cycles time is ~5.62s.</color>",
            4 => "<color=" + HexMauve + "><b>[TRIAL 5]</b></color> <color=" + HexLavender + ">Attach <b>250g mass</b>. Extension reaches ~9.81cm & 10 cycles time is ~6.28s.</color>",
            _ => "<color=" + HexMauve + "><b>[CHALLENGE]</b></color> <color=" + HexLavender + ">Now attach a <b>Mystery Mass (?)</b> from the table to calculate its unknown mass!</color>"
        };
    }
}
