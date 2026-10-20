using UnityEngine;

// The sprite frames for one character (the ninja or a samurai type). Swapping the asset re-skins the character.
[CreateAssetMenu(menuName = "Ninja/Character Art")]
public class CharacterArt : ScriptableObject
{
    [Tooltip("Looping idle, or the two-step march for samurai.")]
    public Sprite[] idle;
    public Sprite[] attack;
    public Sprite[] hit;
    public Sprite[] death;

    [Tooltip("Frames per second for the looping idle/march clip.")]
    public float idleFps = 6f;
    [Tooltip("Frames per second for the one-shot attack, hit and death clips.")]
    public float actionFps = 12f;

    [Tooltip("Small pieces burst out on death (sakura petals, smoke).")]
    public Sprite[] deathParticles;
}
