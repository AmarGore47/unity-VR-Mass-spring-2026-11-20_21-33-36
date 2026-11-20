using UnityEngine;

/// <summary>
/// Identifies and configures a physical laboratory weight for masses and spring practical.
/// Supports standard slotted masses (10g - 250g) and Unknown / Mystery masses (Mystery A, B, C).
/// </summary>
public class LabWeight : MonoBehaviour
{
    [Tooltip("Weight in grams (e.g. 50, 100, 125, 250)")]
    public float massInGrams = 100f;

    [Tooltip("Display name in UI table (e.g. 100g Weight, Mystery Mass A)")]
    public string weightLabel = "";

    [Tooltip("Is this an unknown/mystery mass challenge body?")]
    public bool isMystery = false;

    public float MassInKg => massInGrams / 1000f;
}
