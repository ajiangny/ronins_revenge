using UnityEngine;

// Plays the effects in the FxLibrary. One-shots clean themselves up; loops return a player to Stop() later.
// When the library or a clip is missing, Play returns null and callers keep their simpler fallback.
public static class Fx
{
    public const string Impact = "Impact";
    public const string SmokePuff = "SmokePuff";
    public const string Explosion = "Explosion";
    public const string LightningStrike = "LightningStrike";
    public const string IceBurst = "IceBurst";
    public const string FrostWave = "FrostWave";
    public const string SlashArc = "SlashArc";
    public const string WardBubble = "WardBubble";
    public const string BoonPillar = "BoonPillar";

    // Effects drawn above characters; auras pass a lower order to sit behind them.
    const int DefaultOrder = 60;

    public static FxLibrary Library { get; set; }

    public static string Aura(Element element) => "Aura_" + element;
    public static string SoulCrackle(Element element) => "SoulCrackle_" + element;

    public static bool Has(string id) => Library != null && Library.Find(id) != null;

    public static FxPlayer Play(string id, Vector3 position, Color color, float scale = 1f, Transform follow = null, int order = DefaultOrder)
    {
        FxLibrary.Clip clip = Library != null ? Library.Find(id) : null;
        if (clip == null) return null;

        var go = new GameObject("Fx_" + id);
        go.transform.position = new Vector3(position.x, position.y, -1f);
        if (follow != null) go.transform.SetParent(follow, true);
        go.transform.localScale = Vector3.one * scale / (follow != null ? follow.lossyScale.x : 1f);

        var player = go.AddComponent<FxPlayer>();
        player.Begin(clip, color, order);
        return player;
    }
}

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
