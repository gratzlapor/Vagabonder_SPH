using System.Collections.Generic;
using UnityEngine;

//1.Calculate density

public class SPH : MonoBehaviour
{
    public List<GameObject> particles = new List<GameObject>();
    float gizmoBoxSize = 5;
    float radius = 1;
    float particleCount = 3;
    float gasConstant = 10f;
    float restDensity = 2f;
    float friction = 5f;
    Vector3 gravity = new Vector3(0,-10,0);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < particleCount; i++)
        {
            GameObject particle = new GameObject("Particle " + i);
            particle.transform.position = new Vector3(0, 0, 2*(i-1));
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
        CalculatePressureForce();
        CalculateViscosity();
        CalculateAcceleration();
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
            float density = 0;
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


    void CalculatePressureForce()
    {
       for (int i = 0; i < particleCount; i++)
        {
            Vector3 pressureForce = new Vector3();
            for (int j = 0; j < particleCount; j++)
            {
                float difference = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
                Vector3 direction = particles[i].transform.position - particles[j].transform.position;
                 if (i!=j && difference <= radius) // if 0<r<h
                 {
                    pressureForce += particles[j].GetComponent<Properties>().mass *(particles[i].GetComponent<Properties>().pressure+ particles[j].GetComponent<Properties>().pressure)/(2* particles[j].GetComponent<Properties>().density) * -(45f / (Mathf.PI * Mathf.Pow(radius, 6))) * ((direction / difference) * (radius-Mathf.Pow(difference,2)));
                 }
            }
            particles[i].GetComponent<Properties>().pressureForce = -pressureForce;
        }
    }

    void CalculateViscosity()
    {
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 viscosity = new Vector3();
            for (int j = 0; j < particleCount; j++)
            {
                float difference = Vector3.Distance(particles[i].transform.position, particles[j].transform.position);
                Vector3 direction = particles[i].transform.position - particles[j].transform.position;
                if (i != j && difference <= radius) // if 0<r<h
                {
                    viscosity += particles[j].GetComponent<Properties>().mass * ((particles[j].GetComponent<Properties>().velocity - particles[i].GetComponent<Properties>().velocity) / particles[j].GetComponent<Properties>().density) * (45f / (Mathf.PI * Mathf.Pow(radius, 6))) * (radius - difference);
                }
            }
            viscosity *= friction;
            particles[i].GetComponent<Properties>().viscosity = viscosity;
        }
    }

    void CalculateAcceleration()
    {
        for (int i = 0; i < particleCount; i++)
        {
            Vector3 allForces = particles[i].GetComponent<Properties>().pressureForce + particles[i].GetComponent<Properties>().viscosity + gravity;
            particles[i].GetComponent<Properties>().acceleration = allForces / particles[i].GetComponent<Properties>().density;
        }
    }
}
