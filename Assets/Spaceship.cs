using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Spaceship : MonoBehaviour
{
    [Header("Inscribed")]
    public GameObject dartPrefab;
    public CharacterArt art;
    public float speed = 10f;
    [SerializeField] float shotCooldown = 0.35f;
    [SerializeField] float invulnerableTime = 2f;
    [SerializeField] float screenMargin = 0.2f;

    public event Action Hit;

    public bool ControlsEnabled { get; set; }
    // World-space height of the top of the ronin; samurai reaching it have landed.
    public float TopY => transform.position.y + topOffset;

    Rigidbody body;
    SpriteRenderer sprite;
    SpriteAnimator animator;
    Camera cam;
    float minOffsetX, maxOffsetX, centerOffsetX, topOffset;
    float lastShot = float.NegativeInfinity;
    float invulnerableUntil;
    float moveInput;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        sprite = GetComponentInChildren<SpriteRenderer>();
        animator = sprite.GetComponent<SpriteAnimator>();
        cam = Camera.main;

        // The sprite isn't centred on the pivot, so measure it once and clamp/shoot from the real extents.
        Bounds bounds = sprite.bounds;
        Vector3 pivot = transform.position;
        minOffsetX = bounds.min.x - pivot.x;
        maxOffsetX = bounds.max.x - pivot.x;
        centerOffsetX = bounds.center.x - pivot.x;
        topOffset = bounds.max.y - pivot.y;
    }

    void Start()
    {
        PlayIdle();
    }

    void Update()
    {
        moveInput = 0;
        bool invulnerable = Time.time < invulnerableUntil;
        sprite.enabled = !invulnerable || Mathf.Repeat(Time.time, 0.2f) < 0.1f;

        Keyboard keyboard = Keyboard.current;
        if (!ControlsEnabled || keyboard == null) return;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput += 1;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput -= 1;

        if (keyboard.spaceKey.isPressed && Time.time - lastShot >= shotCooldown)
        {
            Fire(dartPrefab);
        }
    }

    // Every attack goes through here, so a boon can later swap in a different projectile.
    public void Fire(GameObject projectilePrefab)
    {
        Vector3 muzzle = new Vector3(transform.position.x + centerOffsetX, transform.position.y, 0);
        Instantiate(projectilePrefab, muzzle, Quaternion.identity);
        lastShot = Time.time;
        animator.Play(art.attack, false, PlayIdle, art.actionFps);
    }

    void FixedUpdate()
    {
        Vector3 position = body.position;
        position.x += moveInput * speed * Time.fixedDeltaTime;

        // Keep the whole ronin inside the camera's view.
        float halfWidth = cam.orthographicSize * cam.aspect;
        float centerX = cam.transform.position.x;
        position.x = Mathf.Clamp(position.x,
            centerX - halfWidth + screenMargin - minOffsetX,
            centerX + halfWidth - screenMargin - maxOffsetX);
        body.MovePosition(position);
    }

    void OnCollisionEnter(Collision coll)
    {
        if (!ControlsEnabled || Time.time < invulnerableUntil) return;
        if (coll.collider.GetComponentInParent<AlienBullet>() == null) return;

        invulnerableUntil = Time.time + invulnerableTime;
        Debris.BurstSprites(coll.GetContact(0).point, art.deathParticles, Color.white, 10, 1f, 6f);
        animator.Play(art.hit, false, PlayIdle, art.actionFps);
        Hit?.Invoke();
    }

    public void Explode()
    {
        Debris.BurstSprites(sprite.bounds.center, art.deathParticles, Color.white, 30, 1.4f, 9f);
        gameObject.SetActive(false);
    }

    void PlayIdle()
    {
        animator.Play(art.idle, true, null, art.idleFps);
    }
}
