using System;
using System.Collections.Generic;
using UnityEngine;

// Thunder Shuriken: rides on the Spirit Shuriken and gives every samurai (or the boss) that comes within reach of it
// one roll to be struck by lightning arcing off the blade. Added to the shuriken by BoonEffects; dies with it.
public class ShurikenStorm : MonoBehaviour
{
    SamuraiArmy army;
    Func<Boss> boss;
    float radius;
    float chance;
    Action<Vector3, IHittable, Vector3> strike;
    readonly HashSet<IHittable> rolled = new HashSet<IHittable>();

    // strike(from, target, targetPoint) draws and deals the arc.
    public void Begin(SamuraiArmy owner, Func<Boss> currentBoss, float reach, float strikeChance, Action<Vector3, IHittable, Vector3> onStrike)
    {
        army = owner;
        boss = currentBoss;
        radius = reach;
        chance = strikeChance;
        strike = onStrike;
    }

    void FixedUpdate()
    {
        if (strike == null) return;
        Vector3 position = transform.position;
        foreach (Samurai samurai in army.SamuraiWithin(position, radius))
        {
            if (samurai.IsDead) continue;
            Roll(position, samurai, samurai.transform.position);
        }
        Boss target = boss();
        if (target != null && !target.IsDead && (target.Centre - position).sqrMagnitude <= radius * radius * 4f)
        {
            Roll(position, target, target.Centre);
        }
    }

    void Roll(Vector3 from, IHittable target, Vector3 point)
    {
        if (!rolled.Add(target)) return;
        if (UnityEngine.Random.value < chance) strike(from, target, point);
    }
}
