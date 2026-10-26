using System;
using System.Collections.Generic;
using UnityEngine;

// The animated pixel-art effects (made in PixelLab's VFX workbench), looked up by id. Built by Ninja/Apply VFX.
[CreateAssetMenu(menuName = "Ninja/Fx Library")]
public class FxLibrary : ScriptableObject
{
    [Serializable]
    public class Clip
    {
        public string id;
        public Sprite[] frames;
        public float fps = 12f;
        public bool loop;
    }

    public Clip[] clips;
    [Tooltip("Draws a sprite as a solid silhouette in its vertex colour (Ninja/SpriteFlash), for hit flashes.")]
    public Material flashMaterial;

    // Effects are looked up on every hit and kill, so the clips are indexed by id on first use (and again if
    // Apply VFX swaps the array).
    [NonSerialized] Dictionary<string, Clip> byId;
    [NonSerialized] Clip[] indexed;

    public Clip Find(string id)
    {
        if (clips == null || id == null) return null;
        if (byId == null || indexed != clips)
        {
            byId = new Dictionary<string, Clip>();
            foreach (Clip clip in clips)
            {
                if (clip.id != null && clip.frames != null && clip.frames.Length > 0 && !byId.ContainsKey(clip.id)) byId[clip.id] = clip;
            }
            indexed = clips;
        }
        return byId.TryGetValue(id, out Clip found) ? found : null;
    }
}
