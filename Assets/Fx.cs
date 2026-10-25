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
    // Looping projectile art for the Burning and Frozen Riposte shots.
    public const string Fireball = "Fireball";
    public const string IceShard = "IceShard";

    // Benibana's effects.
    public const string BeniSmokeRing = "Beni_SmokeRing";
    public const string BeniCrescent = "Beni_Crescent";
    public const string BeniIaidoFlash = "Beni_IaidoFlash";
    public const string BeniCross = "Beni_Cross";
    public const string BeniChain = "Beni_Chain";
    public const string BeniChainImpact = "Beni_ChainImpact";
    public const string BeniSmokeVeil = "Beni_SmokeVeil";
    public const string BeniPetals = "Beni_Petals";

    // Utsusemi's effects.
    public const string UtsuKasa = "Utsu_Kasa";
    public const string UtsuLace = "Utsu_Lace";
    public const string UtsuPlumb = "Utsu_Plumb";
    public const string UtsuThrust = "Utsu_Thrust";
    public const string UtsuGears = "Utsu_Gears";
    public const string UtsuParts = "Utsu_Parts";
    public const string UtsuCharge = "Utsu_Charge";
    public const string UtsuBeamFlare = "Utsu_BeamFlare";
    public const string UtsuBeamHit = "Utsu_BeamHit";
    public const string UtsuBurn = "Utsu_Burn";
    public const string UtsuChop = "Utsu_Chop";
    public const string UtsuHourSlash = "Utsu_HourSlash";
    public const string UtsuClockMark = "Utsu_ClockMark";
    public const string UtsuSword = "Utsu_Sword";
    public const string UtsuPlumbPortal = "Utsu_PlumbPortal";

    // Shirogane's effects.
    public const string ShiroCrescent = "Shiro_Crescent";
    public const string ShiroSword = "Shiro_Sword";
    public const string ShiroOrb = "Shiro_Orb";
    public const string ShiroWolfCharge = "Shiro_WolfCharge";
    public const string ShiroMist = "Shiro_Mist";
    public const string ShiroBeamFlare = "Shiro_BeamFlare";
    public const string ShiroMoon = "Shiro_Moon";
    // Kuon's Maw: the swirling orb at its jaws, a beam segment tiled down the lane, the splash where it lands and
    // the shockwave as it fires.
    public const string ShiroMawOrb = "Shiro_MawOrb";
    public const string ShiroMawBeam = "Shiro_MawBeam";
    public const string ShiroMawHit = "Shiro_MawHit";
    public const string ShiroMawBurst = "Shiro_MawBurst";
    // The lock-on reticle that closes on the ronin before a blow that must be parried.
    public const string ShiroLockOn = "Shiro_LockOn";

    // Effects drawn above characters; auras pass a lower order to sit behind them.
    const int DefaultOrder = 60;

    public static FxLibrary Library { get; set; }

    public static string Aura(Element element) => "Aura_" + element;
    public static string SoulCrackle(Element element) => "SoulCrackle_" + element;

    public static bool Has(string id) => Library != null && Library.Find(id) != null;

    // A clip's frames, for things that animate themselves (projectiles, tiles); null when it's missing.
    public static Sprite[] Frames(string id) => Library != null ? Library.Find(id)?.frames : null;

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
