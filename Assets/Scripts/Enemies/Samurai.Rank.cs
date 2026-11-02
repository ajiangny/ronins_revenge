using UnityEngine;

// A soldier's rank in the formation: the round's Elite, a veteran row, and the slot it marched into.
public partial class Samurai
{
    // The Elite stands a quarter taller than the ranks, so it's found at a glance.
    const float EliteScale = 1.25f;
    // A veteran's thin steel edge: cold grey-blue, never the gold, bronze, red or violet the shots speak in.
    static readonly Color VeteranEdge = new Color(0.72f, 0.78f, 0.88f, 0.85f);

    public bool IsElite { get; private set; }
    public bool IsVeteran { get; private set; }
    public int MaxHealth => maxHealth;
    // The formation row it was spawned in, 0 at the top.
    public int Row { get; set; }
    // The slot's height once marched in; the most a shove lifts it to.
    public float HomeY { get; set; }

    // When a burning tough one next loses a point (Statuses.BurnTick apart); set as a fresh burn starts (Ignite).
    float nextBurnTick;
    SpriteRenderer eliteGlow;
    Color eliteGlowColour;

    // The round's Elite: several hits to fall, worth three of its kind, bigger, edged and haloed in its clan's colour.
    public void MakeElite(int hitPoints, Color clan)
    {
        health = maxHealth = Mathf.Max(1, hitPoints);
        IsElite = true;
        Points *= 3;
        transform.localScale *= EliteScale;
        // The clan's colour washed towards white, so it never reads as a shot's gold, bronze, red or violet.
        Color edge = Color.Lerp(clan, Color.white, 0.35f);
        SpriteOutline.Wrap(Look, edge, Color.white);
        eliteGlowColour = new Color(clan.r, clan.g, clan.b, 0.5f);
        eliteGlow = Projectile.CreateGlow(transform, 1.8f / transform.lossyScale.x, Look.sortingOrder - 1, eliteGlowColour);
        SetFrost(frost);
    }

    // A veteran row's soldier: more hit points, ten more points, a thin steel edge.
    public void MakeVeteran(int hitPoints)
    {
        health = maxHealth = Mathf.Max(1, hitPoints);
        IsVeteran = true;
        Points += 10;
        SpriteOutline.Wrap(Look, VeteranEdge, VeteranEdge);
        SetFrost(frost);
    }

    // Burning wears down a soldier with more than one hit point, a tick at a time. Fire wears a tough one down but
    // never finishes it, so a burn can't kill, chain into other effects or drop a heart. Also pulses the Elite's halo.
    void UpdateBurn()
    {
        if (eliteGlow != null && !IsDead)
        {
            Color c = eliteGlowColour;
            c.a *= 0.75f + 0.25f * Mathf.Sin(Time.time * 3f);
            eliteGlow.color = c;
        }
        if (!Burning || maxHealth <= 1 || IsDead || army == null || army.Entering || Time.time < nextBurnTick) return;
        nextBurnTick = Time.time + Statuses.BurnTick;
        int damage = 1 + (Soaked ? 1 : 0);
        int dealt = Mathf.Min(damage, health - 1);
        if (dealt <= 0) return;
        health -= dealt;
        HitFlash.Flash(Look, 0.1f);
        Fx.Play(Fx.Impact, transform.position, Statuses.BurnColor, 0.5f);
        // Retints for the wound, so the armour darkens as the fire eats at it.
        SetFrost(frost);
    }
}
