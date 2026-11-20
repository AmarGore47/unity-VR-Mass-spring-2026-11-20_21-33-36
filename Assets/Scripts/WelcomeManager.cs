using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Master Laboratory Initializer.
/// Deactivates legacy static UI panels to prevent overlapping ghost texts,
/// and initializes the modern 3-display workstation architecture.
/// </summary>
public class WelcomeManager : MonoBehaviour
{
    void Awake()
    {
        // 1. Hide legacy scene canvas elements so no old text overlaps modern displays
        DeactivateLegacyUI();

        // 2. Ensure Audio Narrator is present
        if (gameObject.GetComponent<VRLabAudioNarrator>() == null)
        {
            gameObject.AddComponent<VRLabAudioNarrator>();
        }

        // 3. Ensure Canvas Scaler is present
        if (gameObject.GetComponent<VRLabUIAutoScaler>() == null)
        {
            gameObject.AddComponent<VRLabUIAutoScaler>();
        }

        // 4. Ensure Master Practical Controller is present
        if (gameObject.GetComponent<VRPracticalUIController>() == null)
        {
            gameObject.AddComponent<VRPracticalUIController>();
        }

        // 5. Ensure Hand and Controller Interaction Manager is present
        if (gameObject.GetComponent<VRHandInteractionManager>() == null)
        {
            gameObject.AddComponent<VRHandInteractionManager>();
        }
    }

    private void DeactivateLegacyUI()
    {
        // Deactivate all direct children of this legacy canvas (such as old 'Panel', 'Start Practical', 'Text (TMP)')
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                // Only keep runtime generated screens if any
                if (!child.name.StartsWith("Stage_") && !child.name.StartsWith("Screen_"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
    }

    // Legacy handler in case scene buttons still link to it
    public void OnStartSimulationClicked()
    {
        if (VRPracticalUIController.Instance != null)
        {
            VRPracticalUIController.Instance.StartPractical();
        }
    }
}
