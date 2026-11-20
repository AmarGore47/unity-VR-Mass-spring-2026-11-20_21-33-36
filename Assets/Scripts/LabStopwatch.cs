using UnityEngine;
using TMPro;

public class LabStopwatch : MonoBehaviour
{
    [Header("UI / 3D Text Reference")]
    [Tooltip("Stopwatch ki screen par jo TextMeshPro laga hai use yahan drag karein")]
    public TextMeshPro lcdText3D;
    public TextMeshProUGUI lcdTextUI;

    private float elapsedTime = 0f;
    private bool isRunning = false;

    public float ElapsedSeconds => elapsedTime;
    public bool IsTimerRunning => isRunning;

    void Update()
    {
        if (isRunning)
        {
            elapsedTime += Time.deltaTime;
            UpdateDisplay();
        }
    }

    public void ToggleStartStop()
    {
        isRunning = !isRunning;
    }

    public void StartTimer()
    {
        isRunning = true;
    }

    public void StopTimer()
    {
        isRunning = false;
        UpdateDisplay();
    }

    public void ResetTimer()
    {
        isRunning = false;
        elapsedTime = 0f;
        UpdateDisplay();
    }

    public void SetTime(float timeInSeconds)
    {
        elapsedTime = timeInSeconds;
        UpdateDisplay();
    }

    void UpdateDisplay()
    {
        int minutes = Mathf.FloorToInt(elapsedTime / 60f);
        int seconds = Mathf.FloorToInt(elapsedTime % 60f);
        int fraction = Mathf.FloorToInt((elapsedTime * 100f) % 100f);

        string timeString = string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);

        if (lcdText3D != null)
            lcdText3D.text = timeString;

        if (lcdTextUI != null)
            lcdTextUI.text = timeString;
    }
}
