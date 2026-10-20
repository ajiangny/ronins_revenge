using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Alien : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [Header("Inscribed")]
    public AlienBullet bulletPrefab;

    public Rigidbody Body { get; private set; }
    public int Column { get; private set; }
    public int Points { get; private set; }

    AlienFleet fleet;
    Renderer bodyRenderer;
    Color color;
    bool dead;

    void Awake()
    {
        Body = GetComponent<Rigidbody>();
        bodyRenderer = GetComponent<Renderer>();
    }

    public void Init(AlienFleet owner, int column, int points, Color tint)
    {
        fleet = owner;
        Column = column;
        Points = points;
        color = tint;

        var block = new MaterialPropertyBlock();
        block.SetColor(BaseColorId, tint);
        bodyRenderer.SetPropertyBlock(block);
    }

    public void Fire(float bulletSpeed)
    {
        AlienBullet bullet = Instantiate(bulletPrefab, transform.position + Vector3.down * 0.6f, Quaternion.identity);
        bullet.Launch(bulletSpeed);
    }

    void OnCollisionEnter(Collision coll)
    {
        Dart dart = coll.collider.GetComponentInParent<Dart>();
        if (dead || dart == null || !dart.TryConsume()) return;

        dead = true;
        Debris.Burst(transform.position, bodyRenderer.sharedMaterial, color, 10, 0.3f, 7f);
        fleet.OnAlienKilled(this);
        Destroy(gameObject);
    }
}
