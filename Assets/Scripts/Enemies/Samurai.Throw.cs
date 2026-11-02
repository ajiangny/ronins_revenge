using UnityEngine;

// The attack clip: a soldier winds up, and its missile leaves on the clip's release frame, so what you see thrown is
// what flies. A kind without the clip looses at once.
public partial class Samurai
{
    bool shotPending;
    float releaseAt;
    float pendingSpeed;
    ShotMods pendingMods;
    float attackEnds;

    public void Fire(float arrowSpeed, ShotMods mods = default)
    {
        // Picked again mid-clip (a fast volley round), it looses at once rather than eat the shot or restart the throw.
        if (art == null || art.attack == null || art.attack.Length == 0 || shotPending || Time.time < attackEnds)
        {
            Loose(arrowSpeed, mods);
            return;
        }
        Gesture();
        shotPending = true;
        pendingSpeed = arrowSpeed;
        pendingMods = mods;
        releaseAt = Time.time + Mathf.Clamp(art.attackRelease, 0, art.attack.Length - 1) / art.attackFps;
    }

    // Plays the attack clip once and goes back to the march. Shieldbearers and bellringers use it with no missile.
    void Gesture()
    {
        if (art == null || art.attack == null || art.attack.Length == 0 || IsDead || Time.time < attackEnds) return;
        attackEnds = Time.time + art.attack.Length / art.attackFps;
        animator.Play(art.attack, false, ResumeMarch, art.attackFps);
    }

    void ResumeMarch()
    {
        if (!IsDead) animator.Play(art.idle, true, null, art.idleFps);
    }

    // A shot in the windup is lost if its thrower dies, freezes or breaks ranks, or if the army has stopped fighting.
    void UpdateThrow()
    {
        if (!shotPending) return;
        if (IsDead || Encased || army == null || !army.Fighting || army.Frozen || army.IsDiving(this))
        {
            shotPending = false;
            return;
        }
        if (Time.time < releaseAt) return;
        shotPending = false;
        Loose(pendingSpeed, pendingMods);
    }

    void WearShot(Arrow arrow)
    {
        if (art != null && art.shot != null) arrow.Wear(art.shot, art.shotLength);
    }
}
