using System.Collections.Generic;
using UnityEngine;

// Something the ronin collects by touching it: a soul or a heart. It drifts down to his row and waits there until
// he walks into it; when a round ends, GatherAll sends every one still out flying to him.
public abstract class Pickup : MonoBehaviour
{
    const float FallSpeed = 2f;
    const float SwayWidth = 0.25f;
    const float PickupRadius = 0.35f;
    const float MagnetStartSpeed = 2f;
    const float MagnetAcceleration = 30f;

    static readonly List<Pickup> live = new List<Pickup>();

    // How many pickups are still out, so the round can wait for them to arrive.
    public static int Count => live.Count;

    protected Ronin ronin;
    protected float age;
    float originX;
    float restY = float.NegativeInfinity;
    bool gathering;
    float magnetSpeed;

    // Every pickup still out flies to the ronin and is collected on arrival, whether or not he can move.
    public static void GatherAll()
    {
        foreach (Pickup pickup in live)
        {
            pickup.gathering = true;
            pickup.magnetSpeed = MagnetStartSpeed;
        }
    }

    public static void DestroyAll()
    {
        foreach (Pickup pickup in live.ToArray())
        {
            Destroy(pickup.gameObject);
        }
    }

    protected void Begin(Ronin ronin, Vector3 position)
    {
        this.ronin = ronin;
        originX = position.x;
        // He only moves sideways in a fight, so his height at the drop is the row it settles on.
        if (ronin != null) restY = ronin.Bounds.center.y;
    }

    // Called once when the ronin touches it, just before it's destroyed.
    protected abstract void Collect();

    // Per-frame looks (pulsing, bobbing); movement and pickup are handled here.
    protected virtual void Animate(float dt) { }

    void OnEnable() => live.Add(this);
    void OnDisable() => live.Remove(this);

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        bool roninHere = ronin != null && ronin.isActiveAndEnabled;
        Vector3 position = transform.position;
        if (gathering && roninHere)
        {
            // Speeds up as it goes, so a far one doesn't hold up the next round.
            magnetSpeed += MagnetAcceleration * dt;
            Vector3 target = ronin.Bounds.center;
            target.z = position.z;
            position = Vector3.MoveTowards(position, target, magnetSpeed * dt);
        }
        else
        {
            // Settles on his row and sways in place there instead of falling past him.
            position.y = Mathf.Max(position.y - FallSpeed * dt, restY);
            position.x = originX + Mathf.Sin(age * 3f) * SwayWidth;
        }
        transform.position = position;
        Animate(dt);

        if (!roninHere || !(gathering || ronin.ControlsEnabled)) return;
        Bounds bounds = ronin.Bounds;
        if (bounds.SqrDistance(new Vector3(position.x, position.y, bounds.center.z)) <= PickupRadius * PickupRadius)
        {
            Collect();
            Destroy(gameObject);
        }
    }

    protected static SpriteRenderer CreateLayer(Transform parent, string name, Sprite sprite, float scale, int order)
    {
        var layer = new GameObject(name).AddComponent<SpriteRenderer>();
        layer.transform.SetParent(parent, false);
        layer.transform.localScale = Vector3.one * scale;
        layer.sprite = sprite;
        layer.sortingOrder = order;
        return layer;
    }
}
