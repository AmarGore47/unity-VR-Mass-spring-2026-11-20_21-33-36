using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Configures LabUI_Canvas in World Space so that all UI screens
/// float behind or cleanly flanking the practical table without blocking apparatus.
/// </summary>
public class VRLabUIAutoScaler : MonoBehaviour
{
    public float globalCanvasScale = 0.0014f;

    void Awake()
    {
        ConfigureCanvas();
    }

    void Start()
    {
        ConfigureCanvas();
    }

    public void ConfigureCanvas()
    {
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null) return;

        canvas.renderMode = RenderMode.WorldSpace;
        if (canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect != null)
        {
            // Canvas at origin allows screens to place directly in ergonomic world space
            canvasRect.position = Vector3.zero;
            canvasRect.rotation = Quaternion.identity;
            canvasRect.localScale = new Vector3(globalCanvasScale, globalCanvasScale, globalCanvasScale);
            canvasRect.sizeDelta = new Vector2(2000f, 1500f);
        }

        // Ensure TrackedDeviceGraphicRaycaster exists for VR ray / hand interactions
        var raycaster = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        if (raycaster == null)
        {
            canvas.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
        }

        // Hide legacy panels in scene hierarchy if any
        Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
        foreach (var t in allChildren)
        {
            if (t != null && t != canvas.transform && (t.name == "Panel" || t.name == "Start Practical"))
            {
                t.gameObject.SetActive(false);
            }
        }
    }
}
