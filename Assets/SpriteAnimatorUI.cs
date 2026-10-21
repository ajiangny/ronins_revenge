using UnityEngine;
using UnityEngine.UI;

// Loops frames on a UI Image in unscaled time, so it keeps animating while the game is paused for a boon choice.
[RequireComponent(typeof(Image))]
public class SpriteAnimatorUI : MonoBehaviour
{
    Image image;
    Sprite[] frames;
    float fps;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    // null or empty frames stops the animation and leaves the current sprite.
    public void Play(Sprite[] clip, float framesPerSecond)
    {
        frames = clip;
        fps = framesPerSecond;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        image.sprite = frames[(int)(Time.unscaledTime * fps) % frames.Length];
    }
}
