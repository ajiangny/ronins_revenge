using System.Collections;
using UnityEngine;

// The tutorial's last foot soldier: once the army is down he runs in from the top for one desperate shot, so Oni gets
// an arrow to parry that Poguru (busy celebrating) doesn't bonk. Built in code like the Scout; his own riposte home on
// him. Without art he's a plain red marker.
[RequireComponent(typeof(Rigidbody))]
public class Straggler : MonoBehaviour, IHittable
{
    const int SamuraiLayer = 7;
    const float RunSpeed = 6f;
    const float ArrowSpeed = 6f;
    // A beat with his bow up before each shot, so the player sees where it comes from.
    const float Aim = 0.5f;
    static readonly Color Tint = new Color(1f, 0.85f, 0.8f);

    Rigidbody body;
    SpriteRenderer sprite;
    SpriteAnimator animator;
    CharacterArt art;
    bool dead;

    public bool Dead => dead;
    public bool Standing { get; private set; }

    // From just above the top edge at x, down to his spot at y.
    public static Straggler Spawn(CharacterArt art, float x, float y)
    {
        Camera cam = Camera.main;
        var go = new GameObject("Straggler") { layer = SamuraiLayer };
        go.transform.position = new Vector3(x, CameraShake.Center.y + cam.orthographicSize + 1f, 0f);
        go.transform.localScale = Vector3.one * 1.4f;
        var body = go.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        go.AddComponent<SphereCollider>().radius = 0.5f;

        var straggler = go.AddComponent<Straggler>();
        straggler.body = body;
        straggler.art = art;
        var view = new GameObject("Sprite") { layer = SamuraiLayer };
        view.transform.SetParent(go.transform, false);
        straggler.sprite = view.AddComponent<SpriteRenderer>();
        straggler.sprite.color = Tint;
        straggler.animator = view.AddComponent<SpriteAnimator>();
        if (art != null && art.idle != null && art.idle.Length > 0) straggler.animator.Play(art.idle, true, null, art.idleFps * 2f);
        else straggler.sprite.sprite = ProceduralSprites.Glow;
        straggler.StartCoroutine(straggler.RunTo(y));
        return straggler;
    }

    IEnumerator RunTo(float y)
    {
        Sfx.Play(Sfx.SamuraiCry, 0.8f, 1.1f);
        while (!dead && body.position.y > y)
        {
            Vector3 next = body.position;
            next.y = Mathf.Max(y, next.y - RunSpeed * Time.fixedDeltaTime);
            body.MovePosition(next);
            yield return new WaitForFixedUpdate();
        }
        Standing = true;
    }

    // Draws, then looses one ordinary arrow at the target. Null if he fell meanwhile.
    public IEnumerator Shoot(Arrow prefab, Vector3 target, System.Action<Arrow> loosed)
    {
        if (dead) yield break;
        if (art != null && art.attack != null && art.attack.Length > 0) animator.Play(art.attack, false, null, art.attack.Length / Aim);
        yield return new WaitForSeconds(Aim);
        if (dead) yield break;
        Vector3 muzzle = transform.position + Vector3.down * 0.5f;
        Vector3 to = target - muzzle;
        to.z = 0f;
        Arrow arrow = Instantiate(prefab, muzzle, Quaternion.identity);
        arrow.Launch(to.normalized * ArrowSpeed, 2f);
        Sfx.Play(Sfx.ArrowFire);
        if (art != null && art.idle != null && art.idle.Length > 0) animator.Play(art.idle, true, null, art.idleFps * 2f);
        loosed?.Invoke(arrow);
    }

    public void TakeHit(int damage, KillSource source)
    {
        if (dead) return;
        dead = true;
        foreach (Collider c in GetComponentsInChildren<Collider>()) c.enabled = false;
        Debris.BurstSprites(transform.position, art != null ? art.deathParticles : null, Tint, 14, 1f, 7f);
        Fx.Play(Fx.Explosion, transform.position, Color.white, 1.2f);
        CameraShake.Add(0.15f);
        Sfx.Play(Sfx.SamuraiDeath);
        Destroy(gameObject);
    }

    // The lesson's over and he's still up (his riposte struck cover): he falls all the same.
    public void Fall() => TakeHit(1, KillSource.Effect);
}
