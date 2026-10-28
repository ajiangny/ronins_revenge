using System;
using UnityEngine;
using UnityEngine.UI;

// Loops frames on a UI Image in unscaled time, so it keeps animating while the game is paused for a boon choice.
[RequireComponent(typeof(Image))]
public class SpriteAnimatorUI : MonoBehaviour
{
    Image image;
    Sprite[] frames;
    float fps;
    // A one-shot clip holds its last frame and calls back once it has shown it.
    bool loop = true;
    float startedAt;
    Action done;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    // null or empty frames stops the animation and leaves the current sprite.
    public void Play(Sprite[] clip, float framesPerSecond)
    {
        frames = clip;
        fps = framesPerSecond;
        loop = true;
        done = null;
    }

    // Plays the clip once from its first frame, holds the last one, then calls `onDone`.
    public void PlayOnce(Sprite[] clip, float framesPerSecond, Action onDone = null)
    {
        frames = clip;
        fps = framesPerSecond;
        loop = false;
        startedAt = Time.unscaledTime;
        done = onDone;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0) return;
        if (loop)
        {
            image.sprite = frames[(int)(Time.unscaledTime * fps) % frames.Length];
            return;
        }
        int frame = (int)((Time.unscaledTime - startedAt) * fps);
        image.sprite = frames[Mathf.Min(frame, frames.Length - 1)];
        if (frame < frames.Length || done == null) return;
        Action finished = done;
        done = null;
        finished();
    }
}
