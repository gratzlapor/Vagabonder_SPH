using System.Collections.Generic;
using UnityEngine;

//1.Calculate density

public class SPH : MonoBehaviour
{
    public List<GameObject> particles = new List<GameObject>();
    float gizmoBoxSize = 5;
    float radius = 1;
    float particleCount = 3;
    float gasConstant = 8.314f;
    float restDensity = 1.1f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("Particle " + i);
            particle.transform.position = new Vector3(0, 0, i);
            particle.AddComponent<Properties>();
            particles.Add(particle);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        StayInBound();
        CalculateDensity();
        CalculatePressure();
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
            float density = 1;
            for (int j = 0; j < particleCount; j++)
            {
                 float difference = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
                 if (i!=j && difference <= radius) // if 0<r<h
                 {
                     density += particles[j].GetComponent<Properties>().mass * (315f / (64 * Mathf.PI * radius)) * Mathf.Pow(Mathf.Pow(radius,2) - Mathf.Pow(difference, 2), 3);
                 }
            }
            particles[i].GetComponent<Properties>().density = density;
        }
    }

    void CalculatePressure()
    {
        for (int i = 0; i < particleCount; i++)
        {
            particles[i].GetComponent<Properties>().pressure = gasConstant * (particles[i].GetComponent<Properties>().density - restDensity);
        }
    }

    void CalculateAcceleration()
    {

    }
}
