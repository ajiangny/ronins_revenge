using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Spaceship : MonoBehaviour
{
    [Header("Inscribed")]
    public GameObject dartPrefab;
    public float speed = 10f;
    [SerializeField] float shotCooldown = 0.35f;
    [SerializeField] float invulnerableTime = 2f;
    [SerializeField] float screenMargin = 0.2f;

    public event Action Hit;

    public bool ControlsEnabled { get; set; }
    // World-space height of the top of the hull; aliens reaching it have landed.
    public float TopY => transform.position.y + topOffset;

    Rigidbody body;
    Renderer[] renderers;
    Camera cam;
    float minOffsetX, maxOffsetX, centerOffsetX, topOffset;
    float lastShot = float.NegativeInfinity;
    float invulnerableUntil;
    float moveInput;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        renderers = GetComponentsInChildren<Renderer>();
        cam = Camera.main;

        // The hull isn't centred on the pivot, so measure it once and clamp/shoot from the real extents.
        Bounds hull = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            hull.Encapsulate(r.bounds);
        }
        Vector3 pivot = transform.position;
        minOffsetX = hull.min.x - pivot.x;
        maxOffsetX = hull.max.x - pivot.x;
        centerOffsetX = hull.center.x - pivot.x;
        topOffset = hull.max.y - pivot.y;
    }

    void Update()
    {
        moveInput = 0;
        bool invulnerable = Time.time < invulnerableUntil;
        SetVisible(!invulnerable || Mathf.Repeat(Time.time, 0.2f) < 0.1f);

        Keyboard keyboard = Keyboard.current;
        if (!ControlsEnabled || keyboard == null) return;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) moveInput += 1;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed) moveInput -= 1;

        if (keyboard.spaceKey.isPressed && Time.time - lastShot >= shotCooldown)
        {
            Vector3 muzzle = new Vector3(transform.position.x + centerOffsetX, transform.position.y, 0);
            Instantiate(dartPrefab, muzzle, Quaternion.identity);
            lastShot = Time.time;
        }
    }

    void FixedUpdate()
    {
        Vector3 position = body.position;
        position.x += moveInput * speed * Time.fixedDeltaTime;

        // Keep the whole hull inside the camera's view.
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
        Debris.Burst(coll.GetContact(0).point, renderers[0].sharedMaterial, Color.white, 14, 0.3f, 6f);
        Hit?.Invoke();
    }

    public void Explode()
    {
        Debris.Burst(transform.position + Vector3.right * centerOffsetX, renderers[0].sharedMaterial, Color.white, 30, 0.45f, 9f);
        gameObject.SetActive(false);
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers)
        {
            r.enabled = visible;
        }
    }
}
