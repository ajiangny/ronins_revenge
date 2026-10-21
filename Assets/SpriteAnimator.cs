using System;
using UnityEngine;

// Cycles a SpriteRenderer through a short list of frames. Enough for 2-4 frame pixel-art loops without an Animator.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteAnimator : MonoBehaviour
{
    [SerializeField] float fps = 8f;

    public SpriteRenderer Renderer { get; private set; }

    Sprite[] clip;
    float clipFps;
    bool loop;
    float frameTime;
    int frame;
    Action finished;

    void Awake()
    {
        Renderer = GetComponent<SpriteRenderer>();
    }

    // Starts a clip from its first frame. A one-shot clip holds its last frame and then calls onFinished.
    // framesPerSecond <= 0 uses the animator's default rate.
    public void Play(Sprite[] frames, bool looping = true, Action onFinished = null, float framesPerSecond = 0)
    {
        if (frames == null || frames.Length == 0) return;
        clip = frames;
        clipFps = framesPerSecond > 0 ? framesPerSecond : fps;
        loop = looping;
        finished = onFinished;
        frame = 0;
        frameTime = 0;
        Renderer.sprite = clip[0];
    }

    // Moves to the next frame by hand, for clips driven by game events (the army's march step) rather than time.
    public void Step()
    {
        if (clip == null) return;
        frame = (frame + 1) % clip.Length;
        Renderer.sprite = clip[frame];
    }

    void Update()
    {
        if (clip == null || clipFps <= 0) return;

        frameTime += Time.deltaTime;
        if (frameTime < 1f / clipFps) return;
        frameTime -= 1f / clipFps;

        if (frame + 1 < clip.Length)
        {
            frame++;
        }
        else if (loop)
        {
            frame = 0;
        }
        else
        {
            Action done = finished;
            finished = null;
            clip = null;
            done?.Invoke();
            return;
        }
        Renderer.sprite = clip[frame];
    }
}
