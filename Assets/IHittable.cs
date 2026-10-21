// Anything the ronin's projectiles and boon effects can damage: samurai, scouts, bosses.
public interface IHittable
{
    void TakeHit(int damage, KillSource source);
}

public static class Damage
{
    // A kunai is one point; the shuriken and parried arrows hit harder, which matters against armour and bosses.
    public static int Of(KillSource source) => source switch
    {
        KillSource.Shuriken => 3,
        KillSource.Deflected => 2,
        _ => 1,
    };
}
