using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class AlienFleet : MonoBehaviour
{
    [Header("Inscribed")]

    public GameObject alienPrefab;

    public AlienCounter alienCounter;

    public List<GameObject> aliens = new List<GameObject> { };

    float speed = .1f;
    float leftBound = -16;
    float rightBound = 16;
    float lowerBound = 4.8f;
    float downTime = .1f;
    bool movingDown = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                GameObject alien = Instantiate<GameObject>(alienPrefab);
                alien.transform.position = new Vector3(2 * i - 12, 10 - 2 * j, 0);
                aliens.Add(alien);
                alien.GetComponent<Alien>().fleet = this;
            }
        }

    }

    // Update is called once per frame
    void Update()
    {
        alienCounter.score = aliens.Count;
    }

    void FixedUpdate()
    {
        //move all aliens
        foreach (GameObject alien in aliens)
        {
            Vector3 position = alien.transform.position;
            if (!movingDown)
            {
                position.x += speed;
            }
            else
            {
                position.y += -Mathf.Abs(speed);
                /*if (position.y <= lowerBound)
                {
                    movingDown = false;
                    lowerBound = position.y - 1;
                }*/
            }
            alien.transform.position = position;
        }

        //change alien direction
        float left = 0, right = 0;
        foreach (GameObject alien in aliens)
        {
            Vector3 position = alien.transform.position;
            if (position.x > right)
            {
                right = position.x;
            }
            if (position.x < left)
            {
                left = position.x;
            }
        }
        if (speed > 0 && right >= rightBound || speed < 0 && left <= leftBound)
        {
            speed = -speed;
            movingDown = true;
            Invoke("startSidewaysMotion", downTime);
        }
    }

    void startSidewaysMotion()
    {
        movingDown = false;
    }
}
