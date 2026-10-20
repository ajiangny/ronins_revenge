using UnityEngine;

public class Alien : MonoBehaviour
{
    [Header("Inscribed")]

    public GameObject bulletPrefab;
    public AlienFleet fleet;
    
    static float bulletSpawnRate = .002f;
    public float lastShot = 0;
    public static float shotCooldown = .1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void FixedUpdate()
    {
        Vector3 position = transform.position;
        
        if(UnityEngine.Random.value < bulletSpawnRate && Time.time - lastShot > shotCooldown){
            GameObject bullet = Instantiate<GameObject>(bulletPrefab);
            bullet.transform.position = position;
            lastShot = Time.time;
        }
    }

    void OnCollisionEnter(Collision coll)
    {
        fleet.GetComponent<AlienFleet>().aliens.Remove(gameObject);
        Destroy(gameObject);
        Destroy(coll.gameObject);
    }
}
