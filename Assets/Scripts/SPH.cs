using System.Collections.Generic;
using UnityEngine;

//1.Calculate density

public class SPH : MonoBehaviour
{
    public List<GameObject> particles = new List<GameObject>();
    public float gizmoBoxSize = 5;
    public float radius = 1;
    public float particleCount = 3;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("Particle " + i);
            particle.transform.position = new Vector3(0, 0, i*2);
            particle.AddComponent<Properties>();
            particles.Add(particle);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        StayInBound();
        CalculateDensity();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(gizmoBoxSize, gizmoBoxSize, gizmoBoxSize));
    }

    void StayInBound()
    {
        foreach (var particle in particles)
        {
            if (particle.transform.position.y < -gizmoBoxSize / 2)
            {
                particle.transform.position = new Vector3(particle.transform.position.x, -gizmoBoxSize / 2, particle.transform.position.z);
            }
        }
    }

    void CalculateDensity()
    {
        for (int i = 0; i < particleCount; i++)
        {
            float allMass = 0;
            for (int j = 0; j < particleCount; j++)
            {
                float difference = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
                if (difference < radius)
                {
                    allMass += particles[j].GetComponent<Properties>().mass * (315f / (64 * Mathf.PI * radius)) * Mathf.Pow(radius - Mathf.Pow(difference, 2), 3);
                }
            }
            particles[i].GetComponent<Properties>().density = allMass;
        }
    }
}
