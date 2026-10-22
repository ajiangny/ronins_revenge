using UnityEngine;
using TMPro;

[RequireComponent(typeof(TextMeshProUGUI))]
public class SamuraiCounter : MonoBehaviour
{
    TextMeshProUGUI uiText;

    void Awake()
    {
        uiText = GetComponent<TextMeshProUGUI>();
    }

    // Called by the army only when the count changes, instead of rebuilding the string every frame.
    public void SetCount(int remaining)
    {
        uiText.SetText("Samurai left  {0}", remaining);
    }
}
