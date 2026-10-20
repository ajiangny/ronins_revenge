using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class AlienCounter : MonoBehaviour
{
    TextMeshProUGUI uiText;

    void Awake()
    {
        uiText = GetComponent<TextMeshProUGUI>();
    }

    // Called by the fleet only when the count changes, instead of rebuilding the string every frame.
    public void SetCount(int remaining)
    {
        uiText.SetText("Remaining Aliens: {0}", remaining);
    }
}
