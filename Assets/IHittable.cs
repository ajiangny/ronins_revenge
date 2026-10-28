// Anything the ronin's projectiles and boon effects can damage: samurai, scouts, bosses.
public interface IHittable
{
    void TakeHit(int damage, KillSource source);
}

public static class Damage
{
    // A slash or kunai is one point; the shuriken and parried arrows hit harder, which matters against armour and bosses.
    // A boss's own slash parried back does no damage: it only breaks her stance, opening her to double damage.
    public static int Of(KillSource source) => source switch
    {
        KillSource.Shuriken => 3,
        KillSource.Deflected => 2,
        KillSource.Reflected => 0,
        _ => 1,
    };

    // The ronin's own hits that set off boon effects: not effects themselves, and not the lesser blades (Split Slash's
    // forks, the Shadow Clone's).
    public static bool Procs(KillSource source) => source != KillSource.Effect && !IsLesser(source);

    public static bool IsLesser(KillSource source) => source == KillSource.Shadow || source == KillSource.Clone;

    // How much of a boss's stance meter a hit fills (1 is a full meter, and a stagger). Plain cuts barely dent it,
    // parried shots and the shuriken chip at it, and her own slash sent back takes a solid chunk. Boon effects add
    // nothing, so a pile of procs can't lock a boss in place.
    public static float Stance(KillSource source) => source switch
    {
        KillSource.Reflected => 0.15f,
        KillSource.Deflected => 0.07f,
        KillSource.Shuriken => 0.06f,
        KillSource.Shadow or KillSource.Clone => 0.0075f,
        KillSource.Effect => 0f,
        _ => 0.015f,
    };

    // A parry-only set piece sent home (the hour cleave, the Last Toll, the Soul Coffin, the Fang) fills the most.
    public const float SetPieceStance = 0.35f;

    // What a boss or Kuon takes from a lesser slash: half, with the halves adding up until they make a whole point.
    public const float ShadowShare = 0.5f;

    public static int Lessen(int damage, KillSource source, ref float chip)
    {
        if (!IsLesser(source)) return damage;
        chip += damage * ShadowShare;
        int whole = UnityEngine.Mathf.FloorToInt(chip);
        chip -= whole;
        return whole;
    }
}
