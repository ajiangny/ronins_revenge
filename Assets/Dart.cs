using UnityEngine;

public class Dart : MonoBehaviour
{
    [Header("Inscribed")]
    static float speed = .3f;
    
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
        position.y += speed;
        transform.position = position;
        if(position.y > 10)
        {
            Destroy(gameObject);
        }
    }

}
