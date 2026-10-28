using UnityEngine;
using UnityEngine.Rendering.Universal;

// The full-screen pass that draws TimeRipple's rings and stopped-time colour, added to the renderers by
// Ninja/Apply Screen Ripples. It only enqueues while something is live, so it costs nothing the rest of the fight.
public class TimeRippleFeature : FullScreenPassRendererFeature
{
    // The last frame the renderer asked this feature for passes: TimeRipple falls back to sprite rings without it.
    public static int LastSeenFrame = -100;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        LastSeenFrame = Time.frameCount;
        if (!TimeRipple.Live || renderingData.cameraData.cameraType != CameraType.Game) return;
        base.AddRenderPasses(renderer, ref renderingData);
    }
}
