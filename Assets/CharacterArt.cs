using UnityEngine;

// The sprite frames for one character (the ronin or a samurai type). Swapping the asset re-skins the character.
[CreateAssetMenu(menuName = "Ninja/Character Art")]
public class CharacterArt : ScriptableObject
{
    [Tooltip("Looping idle, or the two-step march for samurai.")]
    public Sprite[] idle;
    public Sprite[] attack;
    public Sprite[] hit;
    public Sprite[] death;
    [Tooltip("The ronin's katana parry stance. Falls back to the attack clip when empty.")]
    public Sprite[] parry;
    [Tooltip("The ronin's katana swing for his main attack. Falls back to the parry clip when empty.")]
    public Sprite[] slash;
    [Tooltip("Sprint loops played while moving left/right. Leave empty to keep the idle clip while moving.")]
    public Sprite[] runLeft;
    public Sprite[] runRight;
    [Tooltip("The ronin walking away up the screen, between maps. Leave empty to bob on the idle clip instead.")]
    public Sprite[] walkUp;
    [Tooltip("The ronin's spirit wisps, one per sprite on a canvas matching his frames; layered on and bobbed in code.")]
    public Sprite[] wisps;

    [Tooltip("Frames per second for the looping idle/march clip.")]
    public float idleFps = 6f;
    [Tooltip("Frames per second for the one-shot attack, hit and death clips.")]
    public float actionFps = 12f;
    [Tooltip("Frames per second for the sprint loops.")]
    public float runFps = 14f;
    [Tooltip("Frames per second for the walk-up loop.")]
    public float walkFps = 8f;

    [Tooltip("Small pieces burst out on death (sakura petals, smoke).")]
    public Sprite[] deathParticles;
}
