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
}
