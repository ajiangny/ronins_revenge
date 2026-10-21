using System;
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

    public Clip Find(string id)
    {
        if (clips == null) return null;
        foreach (Clip clip in clips)
        {
            if (clip.id == id && clip.frames != null && clip.frames.Length > 0) return clip;
        }
        return null;
    }
}
