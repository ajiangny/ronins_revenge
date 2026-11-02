using UnityEngine;

// Pushing single soldiers back up the field (Water's knockback, the Undertow wave).
public partial class SamuraiArmy
{
    // The furthest one shove lifts a soldier, however strong the push.
    const float MaxShove = 1.6f;

    // Knocks one soldier back up the field by `up`, capped by MaxShove, its slot height (Samurai.HomeY) and the top of
    // the view; it stays where it lands, so the formation goes ragged. A loose soldier stays loose and sinks back to
    // the last row at the march's pace (FixedUpdate). Divers are left to their swoop. Returns whether it moved.
    // Call it from Update (or a physics callback), not FixedUpdate: see the note on setting the position below.
    public bool Shove(Samurai soldier, float up)
    {
        if (Entering || overrunning || soldier == null || soldier.IsDead || up <= 0f || IsDiving(soldier)) return false;
        Vector3 at = soldier.Body.position;
        float viewTop = CameraShake.Center.y + cam.orthographicSize - samuraiRadius - 0.3f;
        float lift = Mathf.Min(up, MaxShove, soldier.HomeY - at.y, viewTop - at.y);
        if (lift <= 0f) return false;
        at.y += lift;
        // Set, not MovePosition: the kinematic body only queues a MovePosition for the next physics step, and the
        // march's own MovePosition in FixedUpdate would replace it with the old position plus a step.
        soldier.transform.position = at;
        soldier.Body.position = at;
        HitFlash.Flash(soldier.Look, 0.1f);
        Fx.Play(Fx.Splash, at, Color.white, 0.6f);
        if (holding && !AnyAtRow()) holding = false;
        return true;
    }

    // Whether any soldier still stands in the last row: once the tide has lifted them all off it, the army stops
    // holding (no lunges) until one gets back down and holds afresh.
    bool AnyAtRow()
    {
        float rowY = RowY + 0.05f;
        foreach (Samurai soldier in soldiers)
        {
            if (!soldier.IsDead && !IsDiving(soldier) && soldier.Body.position.y <= rowY) return true;
        }
        return false;
    }
}
