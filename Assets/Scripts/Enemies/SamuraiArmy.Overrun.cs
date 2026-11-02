using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The point the round can no longer be won: soldiers left standing in the last row too long overrun the ronin. The
// time counts up while anyone holds the row and drains while it's clear; when it runs out every soldier left rushes
// him and the run ends (ArmyReachedRonin). A boss's levy has no such clock: it's only there to crowd his fight.
public partial class SamuraiArmy
{
    // The rush has begun: nothing can stop it now.
    public event Action Overrunning;

    float overrunHeld;
    float nextOverrunBeat;
    bool overrunning;
    bool levy;
    Coroutine overrun;

    // 0 while the row is safe, 1 at the rush. What the red glow at the foot of the screen shows.
    public float OverrunProgress
    {
        get
        {
            if (overrunning) return 1f;
            if (!advancing || soldiers.Count == 0 || overrunTime <= 0f) return 0f;
            return Mathf.Clamp01(overrunHeld / overrunTime);
        }
    }

    public float OverrunSecondsLeft => overrunning ? 0f : Mathf.Max(0f, overrunTime - overrunHeld);

    void ResetOverrun()
    {
        if (overrun != null) StopCoroutine(overrun);
        overrun = null;
        overrunning = false;
        overrunHeld = 0f;
        nextOverrunBeat = 0f;
    }

    // Whether anyone stands in the last row. A soldier of the row out on a lunge still counts, or a lunge would stop
    // the clock; one the tide has lifted off the row doesn't, so Water can buy time.
    bool RowOccupied()
    {
        float rowY = RowY + 0.05f;
        foreach (Samurai soldier in soldiers)
        {
            if (soldier.IsDead) continue;
            if (IsDiving(soldier) ? loose.ContainsKey(soldier) : soldier.Body.position.y <= rowY) return true;
        }
        return false;
    }

    // One physics step of the clock. Returns whether the rush just began, which ends the march.
    bool UpdateOverrun(float dt)
    {
        if (levy || overrunTime <= 0f) return false;
        if (!RowOccupied())
        {
            overrunHeld = Mathf.Max(0f, overrunHeld - dt * overrunRecovery);
            return false;
        }
        overrunHeld += dt;
        // A war drum under the glow, quickening as the time runs out.
        if (Time.time >= nextOverrunBeat)
        {
            float progress = overrunHeld / overrunTime;
            nextOverrunBeat = Time.time + Mathf.Lerp(1.3f, 0.3f, progress);
            Sfx.Play(Sfx.MarchStep, Mathf.Lerp(0.5f, 1f, progress), 0.55f);
        }
        if (overrunHeld < overrunTime) return false;
        advancing = false;
        overrunning = true;
        overrun = StartCoroutine(RushRonin());
        return true;
    }

    // Every soldier left throws itself at the ronin, the nearest first, and they pile up on him.
    IEnumerator RushRonin()
    {
        foreach (Dive dive in dives) dive.ClearTell();
        dives.Clear();
        Overrunning?.Invoke();
        Sfx.Play(Sfx.SamuraiCry, 1f, 0.8f);
        CameraShake.Add(0.4f);

        Vector3 at = target != null ? target.position : Vector3.zero;
        var rushers = new List<(Samurai soldier, Vector3 from, Vector3 to, float delay)>();
        foreach (Samurai soldier in soldiers)
        {
            Vector3 from = soldier.Body.position;
            Vector3 to = at + (Vector3)(UnityEngine.Random.insideUnitCircle * 0.5f);
            // The row goes first and the ranks behind it follow.
            float delay = Mathf.Clamp01((from.y - at.y) / 16f) * overrunRush * 0.5f + UnityEngine.Random.Range(0f, 0.08f);
            rushers.Add((soldier, from, to, delay));
        }

        for (float t = 0f; t < overrunRush; t += Time.fixedDeltaTime)
        {
            foreach (var rusher in rushers)
            {
                if (rusher.soldier == null || rusher.soldier.IsDead) continue;
                float u = Mathf.Clamp01((t - rusher.delay) / Mathf.Max(0.01f, overrunRush - rusher.delay));
                rusher.soldier.Body.MovePosition(Vector3.Lerp(rusher.from, rusher.to, u * u));
            }
            yield return new WaitForFixedUpdate();
        }

        overrunning = false;
        overrun = null;
        // Live again for the event: a defeat halts it, and god mode sweeps the army away and needs the round to end.
        advancing = true;
        ArmyReachedRonin?.Invoke();
        if (advancing && soldiers.Count == 0)
        {
            advancing = false;
            ArmyDefeated?.Invoke();
        }
    }
}
