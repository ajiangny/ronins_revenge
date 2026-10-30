using System;
using System.Collections.Generic;
using UnityEngine;

// Static Charge: rides on the shuriken thrown after a landed parry and Jolts each samurai (or boss, or Kuon) it passes
// within reach, once each. Nothing here deals damage, so it can set off nothing else.
public class ShurikenCharge : MonoBehaviour
{
    SamuraiArmy army;
    Func<IEnumerable<Boss>> bosses;
    Func<SpiritWolf> wolf;
    float radius;
    readonly HashSet<object> jolted = new HashSet<object>();

    public void Begin(SamuraiArmy owner, Func<IEnumerable<Boss>> currentBosses, Func<SpiritWolf> currentWolf, float reach)
    {
        army = owner;
        bosses = currentBosses;
        wolf = currentWolf;
        radius = reach;
    }

    void FixedUpdate()
    {
        if (army == null) return;
        Vector3 position = transform.position;
        foreach (Samurai samurai in army.SamuraiWithin(position, radius))
        {
            if (!samurai.IsDead && jolted.Add(samurai)) samurai.Jolt(Statuses.JoltSeconds);
        }
        foreach (Boss target in bosses())
        {
            if (target == null || target.IsDead || (target.Centre - position).sqrMagnitude > radius * radius * 4f) continue;
            if (jolted.Add(target)) target.Jolt(Statuses.JoltSeconds);
        }
        SpiritWolf kuon = wolf();
        if (kuon != null && (kuon.Centre - position).sqrMagnitude <= radius * radius * 4f && jolted.Add(kuon)) kuon.Jolt(Statuses.JoltSeconds);
    }
}
