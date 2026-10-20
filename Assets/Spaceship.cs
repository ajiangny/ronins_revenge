using UnityEngine;
using UnityEngine.InputSystem;

public class Spaceship : MonoBehaviour
{
    [Header("Inscribed")]
    public GameObject dartPrefab;

    public static float shotCooldown = .05f;
    public float lastShot = 0;
    public float speed = 10f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Keyboard.current.rightArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.x += speed * Time.deltaTime;
            transform.position = position;
        }
        if (Keyboard.current.leftArrowKey.isPressed)
        {
            Vector3 position = transform.position;
            position.x -= speed * Time.deltaTime;
            transform.position = position;
        }
        if (Keyboard.current.spaceKey.wasPressedThisFrame && Time.time - lastShot > shotCooldown)
        {
            Vector3 position = transform.position;
            GameObject bullet = Instantiate<GameObject>(dartPrefab);
            bullet.transform.position = position;
            lastShot = Time.time;
        }
    }

}


