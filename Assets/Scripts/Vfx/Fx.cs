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
    // The same flare in the molten beam's orange and gold, for its second form.
    public const string UtsuBeamFlareP2 = "Utsu_BeamFlareP2";
    public const string UtsuBeamHit = "Utsu_BeamHit";
    public const string UtsuBurn = "Utsu_Burn";
    public const string UtsuChop = "Utsu_Chop";
    public const string UtsuHourSlash = "Utsu_HourSlash";
    public const string UtsuClockMark = "Utsu_ClockMark";
    public const string UtsuSword = "Utsu_Sword";
    public const string UtsuSwordP2 = "Utsu_SwordP2";
    public const string UtsuSwordFlying = "Utsu_SwordFlying";
    public const string UtsuSwordFlyingP2 = "Utsu_SwordFlyingP2";
    public const string UtsuNumerals = "Utsu_Numerals";
    public const string UtsuBeamRed = "Utsu_BeamRed";
    public const string UtsuBeamMolten = "Utsu_BeamMolten";
    public const string UtsuHaloShards = "Utsu_HaloShards";
    public const string UtsuCracking = "Utsu_Cracking";
    // Its second form's lace: dark steel cracked with molten light, like its halo shards.
    public const string UtsuLaceP2 = "Utsu_LaceP2";
    // Escapement's turning escape wheel.
    public const string UtsuEscapeWheel = "Utsu_EscapeWheel";
    // The reusable safe-zone marker (SafeZone): its ward (left post, tiled band, right post) and its shield icon.
    public const string SafeZone = "SafeZone";
    public const string SafeZoneIcon = "SafeZoneIcon";
    public const string UtsuPlumbPortal = "Utsu_PlumbPortal";
    // The rocks its overload shakes loose: three looks (the stage's Rock, RockB and RockC blockers), each intact,
    // cracked and nearly broken, so frame look * 3 + state.
    public const string UtsuRock = "Utsu_Rock";

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
    // Her ice: a pillar dropped down a lane, the glacier spire far too big to parry, the shards they burst into at the
    // ground, her ice slash and the ice wolf's head that lunges along it.
    public const string ShiroIcePillar = "Shiro_IcePillar";
    public const string ShiroIceSpire = "Shiro_IceSpire";
    public const string ShiroIceShatter = "Shiro_IceShatter";
    public const string ShiroIceSlash = "Shiro_IceSlash";
    public const string ShiroWolfSlash = "Shiro_WolfSlash";
    // A wisp of spirit light flying right, one of the shrine's souls that gather into Kuon at her phase break.
    public const string ShiroSpiritWisp = "Shiro_SpiritWisp";

    // Kagetsune's: a soul-skull shot, an ofuda strip, the great bell of the Last Toll and the thrown Soul Coffin.
    public const string KageSkull = "Kage_Skull";
    public const string KageOfuda = "Kage_Ofuda";
    public const string KageGreatBell = "Kage_GreatBell";
    public const string KageCoffin = "Kage_Coffin";
    // The bullet-hell kit's: the bronze bell-shot a parry rings into Resonance, and the crimson seal-dart no parry turns.
    public const string KageBellShot = "Kage_BellShot";
    public const string KageSealDart = "Kage_SealDart";
    // His set dressing: a hanging bell for the Night of a Thousand Bells, a chain link and a soul-fire column
    // segment drawn tiled along their length, and the burst under a slam.
    public const string KageBell = "Kage_Bell";
    public const string KageChain = "Kage_Chain";
    public const string KageSoulPillar = "Kage_SoulPillar";
    public const string KageShockwave = "Kage_Shockwave";

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
