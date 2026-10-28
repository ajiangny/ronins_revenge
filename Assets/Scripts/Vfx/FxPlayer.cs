using UnityEngine;

// One playing effect from the FxLibrary: Fx.Play adds it with a sprite and animator; a one-shot removes itself when
// its clip ends, a loop runs until Stop.
public class FxPlayer : MonoBehaviour
{
    SpriteRenderer sprite;

    public void Begin(FxLibrary.Clip clip, Color color, int order)
    {
        sprite = gameObject.AddComponent<SpriteRenderer>();
        sprite.color = color;
        sprite.sortingOrder = order;
        var animator = gameObject.AddComponent<SpriteAnimator>();
        animator.Play(clip.frames, clip.loop, clip.loop ? null : (System.Action)Stop, clip.fps);
    }

    public Color Color
    {
        get => sprite.color;
        set => sprite.color = value;
    }

    public void Stop()
    {
        if (this != null) Destroy(gameObject);
    }
}
