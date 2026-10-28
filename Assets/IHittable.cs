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

    // The ronin's own hits that set off boon effects: not effects themselves, and not the lesser shadow slashes.
    public static bool Procs(KillSource source) => source != KillSource.Effect && source != KillSource.Shadow;

    // What a boss or Kuon takes from a lesser slash: half, with the halves adding up until they make a whole point.
    public const float ShadowShare = 0.5f;

    public static int Lessen(int damage, KillSource source, ref float chip)
    {
        if (source != KillSource.Shadow) return damage;
        chip += damage * ShadowShare;
        int whole = UnityEngine.Mathf.FloorToInt(chip);
        chip -= whole;
        return whole;
    }
}
