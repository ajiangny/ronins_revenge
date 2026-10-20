using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AlienCounter : MonoBehaviour
{
    [Header("Dynamic")]
    public int score = 0;
    private TextMeshProUGUI uiText;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        uiText = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        uiText.text = "Remaining Aliens: " + score.ToString("#,0");
    }
}
