using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class SPH : MonoBehaviour
{
    public List<GameObject> particles;
    public float gizmoBoxSize = 5;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < 2; i++)
        {
            GameObject particle = new GameObject("Particle " + i);
            particle.transform.position = new Vector3 (0, 0, i);
            particles.Add(particle);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        Gravity();
        StayInBound();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gizmoBoxSize, gizmoBoxSize, gizmoBoxSize));
    }

    void Gravity()
    {
        foreach (GameObject particle in particles)
        {
            particle.transform.position += new Vector3(0f, -9.81f, 0f) * Time.deltaTime;
        }
    }

    void StayInBound()
    {
        foreach(GameObject particle in particles)
        {
            if(particle.transform.position.y < -gizmoBoxSize/2)
            {
                particle.transform.position = new Vector3(particle.transform.position.x, -gizmoBoxSize/2, particle.transform.position.z);
            }
        }
    }
}
