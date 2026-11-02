using System;
using System.Collections.Generic;
using UnityEngine;

// Lodestone Shuriken: rides on the shuriken and cracks each samurai (or boss, or Kuon) it passes within reach, once each,
// and draws in the nearest shots around it, each falling as scrap. The boon's count is its only cap, a throw's worth, so
// a curtain's shots can be caught without touching RiposteCap: a caught shot makes no riposte to swarm with.
public class ShurikenLodestone : MonoBehaviour
{
    // How close a shot must come to the blade to be drawn in.
    const float CatchReach = 2f;

    SamuraiArmy army;
    Func<IEnumerable<Boss>> bosses;
    Func<SpiritWolf> wolf;
    float radius;
    int catchesLeft;
    Action<Vector3> caught;
    readonly HashSet<IHittable> cracked = new HashSet<IHittable>();

    public void Begin(SamuraiArmy owner, Func<IEnumerable<Boss>> currentBosses, Func<SpiritWolf> currentWolf, float reach,
        int catches, Action<Vector3> onCaught)
    {
        army = owner;
        bosses = currentBosses;
        wolf = currentWolf;
        radius = reach;
        catchesLeft = catches;
        caught = onCaught;
        // The field shows as iron filings carried round the blade, under the arrows, gone with it. Nothing without the art.
        Fx.Play(Fx.SteelFilings, transform.position, Color.white, 1f, transform, Fx.UnderArrows);
    }

    void FixedUpdate()
    {
        if (army == null) return;
        Vector3 position = transform.position;
        foreach (Samurai samurai in army.SamuraiWithin(position, radius))
        {
            if (samurai.IsDead || !cracked.Add(samurai)) continue;
            samurai.Crack(Statuses.CrackSeconds);
        }
        foreach (Boss target in bosses())
        {
            if (target == null || target.IsDead || (target.Centre - position).sqrMagnitude > radius * radius * 4f) continue;
            if (cracked.Add(target)) target.Crack(Statuses.CrackSeconds);
        }
        SpiritWolf kuon = wolf();
        if (kuon != null && (kuon.Centre - position).sqrMagnitude <= radius * radius * 4f && cracked.Add(kuon)) kuon.Crack(Statuses.CrackSeconds);
        if (catchesLeft > 0) CatchShots(position);
    }

    // Nearest first until the count runs out. Spears, what only a parry answers and the bells stay flying.
    void CatchShots(Vector3 position)
    {
        var inReach = new List<Arrow>();
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable || arrow.Unparryable || arrow.Resonant) continue;
            if ((arrow.transform.position - position).sqrMagnitude <= CatchReach * CatchReach) inReach.Add(arrow);
        }
        inReach.Sort((a, b) => (a.transform.position - position).sqrMagnitude.CompareTo((b.transform.position - position).sqrMagnitude));
        for (int i = 0; i < inReach.Count && catchesLeft > 0; i++)
        {
            catchesLeft--;
            Vector3 at = inReach[i].transform.position;
            inReach[i].Catch();
            caught?.Invoke(at);
        }
    }
}
