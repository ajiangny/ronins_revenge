using System;
using System.Collections.Generic;
using UnityEngine;

// Tidewake Shuriken: rides on the shuriken and soaks each samurai (or boss) it passes within reach, once each, and
// washes away the ordinary arrows in its wake. Nothing here kills, so it can set off nothing else.
public class ShurikenWake : MonoBehaviour
{
    SamuraiArmy army;
    Func<IEnumerable<Boss>> bosses;
    float radius;
    float seconds;
    Color water;
    readonly HashSet<IHittable> soaked = new HashSet<IHittable>();

    public void Begin(SamuraiArmy owner, Func<IEnumerable<Boss>> currentBosses, float reach, float soakSeconds, Color colour)
    {
        army = owner;
        bosses = currentBosses;
        radius = reach;
        seconds = soakSeconds;
        water = colour;
    }

    void FixedUpdate()
    {
        if (army == null) return;
        Vector3 position = transform.position;
        foreach (Samurai samurai in army.SamuraiWithin(position, radius))
        {
            if (samurai.IsDead || !soaked.Add(samurai)) continue;
            samurai.Soak(seconds);
        }
        foreach (Boss target in bosses())
        {
            if (target == null || target.IsDead || (target.Centre - position).sqrMagnitude > radius * radius * 4f) continue;
            if (soaked.Add(target)) target.Soak(seconds);
        }
        foreach (Arrow arrow in FindObjectsByType<Arrow>())
        {
            if (arrow.Unbreakable || arrow.Reflectable) continue;
            if ((arrow.transform.position - position).sqrMagnitude <= radius * radius) arrow.Wash(water);
        }
    }
}
