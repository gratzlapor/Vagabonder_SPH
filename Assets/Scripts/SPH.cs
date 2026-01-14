using System.Collections.Generic;
using UnityEngine;

//1.Calculate density

public class SPH : MonoBehaviour
{
    public List<GameObject> particles = new List<GameObject>();
    public List<GameObject> particlesBox = new List<GameObject>();
    public List<GameObject> allParticles = new List<GameObject>();

    float gizmoBoxSize = 10;
    float radius = 1.2f;
    int particleCount;
    int particleBoxCount;
    int allParticlesCount;
    float gasConstant = 100f;
    float restDensity = 1.1f;
    float friction = 0.99f;
    float fluidResistance = 1f;
    float timeStep = 0.008f;
    Vector3 gravity = new Vector3(0,-10,0);

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        for (int i = 0; i < 3; i++)
        {
            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 3; k++)
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    particle.name = i.ToString();
                    particle.transform.position = new Vector3(i - 2, k-2, j - 2);
                    particle.AddComponent<Properties>();
                    particles.Add(particle);
                    allParticles.Add(particle);
                }
            }
        }

        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < 7; j++)
            {
                GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                particle.name = "box" + i;
                particle.transform.position = new Vector3(i - 5, -6, j - 5);
                particle.AddComponent<Properties>();
                particlesBox.Add(particle);
                allParticles.Add(particle);
            }
        }

        for (int i = -5; i < -3; i++)
        {
            for (int j = 0; j < 17; j++)
            {
                if (j < 5)
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    particle.name = "wall" + i +j;
                    particle.transform.position = new Vector3(j - 2, i, -2);
                    particle.AddComponent<Properties>();
                    particlesBox.Add(particle);
                    allParticles.Add(particle);
                }
                else if(j < 9) 
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    particle.name = "wall" + i + j;
                    particle.transform.position = new Vector3(2, i, j-6);
                    particle.AddComponent<Properties>();
                    particlesBox.Add(particle);
                    allParticles.Add(particle);
                }
                else if (j < 13)
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    particle.name = "wall" + i + j;
                    particle.transform.position = new Vector3(10-j, i, 2);
                    particle.AddComponent<Properties>();
                    particlesBox.Add(particle);
                    allParticles.Add(particle);
                }
                else if( j<16 )
                {
                    GameObject particle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    particle.name = "wall" + i + j;
                    particle.transform.position = new Vector3(-2, i, 14-j);
                    particle.AddComponent<Properties>();
                    particlesBox.Add(particle);
                    allParticles.Add(particle);
                }
            }
        }

        particleCount = particles.Count;
        particleBoxCount = particlesBox.Count;
        allParticlesCount = allParticles.Count;


    }

    // Update is called once per frame
    void FixedUpdate()
    {
        CalculateDensity();
        CalculatePressure();
        CalculatePressureForce();
        CalculateViscosity();
        CalculateAcceleration();
        CalculateVelocityandPosition();
    }

    void CalculateDensity()
    {
        for (int i = 0; i < allParticlesCount; i++)
        {
            float density = 1;
            for (int j = 0; j < allParticlesCount; j++)
            {
                 float difference = Vector3.Distance(allParticles[i].transform.position, allParticles[j].transform.position);
                 if (i!=j && difference <= radius) // if 0<r<h
                 {
                     density += allParticles[j].GetComponent<Properties>().mass * (315f / (64 * Mathf.PI * radius)) * Mathf.Pow(Mathf.Pow(radius,2) - Mathf.Pow(difference, 2), 3);
                 }
            }
            allParticles[i].GetComponent<Properties>().density = density;
        }
    }

    void CalculatePressure()
    {
        for (int i = 0; i < allParticlesCount; i++)
        {
            allParticles[i].GetComponent<Properties>().pressure = gasConstant * (allParticles[i].GetComponent<Properties>().density - restDensity);
        }
    }


    void CalculatePressureForce()
    {
       for (int i = 0; i < allParticlesCount; i++)
        {
            Vector3 pressureForce = new Vector3();
            for (int j = 0; j < allParticlesCount; j++)
            {
                float difference = Vector3.Distance(allParticles[i].transform.position, allParticles[j].transform.position);
                Vector3 direction = allParticles[i].transform.position - allParticles[j].transform.position;
                 if (i!=j && difference <= radius) // if 0<r<h
                 {
                    pressureForce += allParticles[j].GetComponent<Properties>().mass *(allParticles[i].GetComponent<Properties>().pressure+ allParticles[j].GetComponent<Properties>().pressure)/(2* allParticles[j].GetComponent<Properties>().density) * -(45f / (Mathf.PI * Mathf.Pow(radius, 6))) * ((direction / difference) * (radius-Mathf.Pow(difference,2)));
                 }
            }
            allParticles[i].GetComponent<Properties>().pressureForce = -pressureForce;
        }
    }

    void CalculateViscosity()
    {
        for (int i = 0; i < allParticlesCount; i++)
        {
            Vector3 viscosity = new Vector3();
            for (int j = 0; j < allParticlesCount; j++)
            {
                float difference = Vector3.Distance(allParticles[i].transform.position, allParticles[j].transform.position);
                Vector3 direction = allParticles[i].transform.position - allParticles[j].transform.position;
                if (i != j && difference <= radius) // if 0<r<h
                {
                    viscosity += allParticles[j].GetComponent<Properties>().mass * ((allParticles[j].GetComponent<Properties>().velocity - allParticles[i].GetComponent<Properties>().velocity) / allParticles[j].GetComponent<Properties>().density) * (45f / (Mathf.PI * Mathf.Pow(radius, 6))) * (radius - difference);
                }
            }
            viscosity *= fluidResistance;
            allParticles[i].GetComponent<Properties>().viscosityForce = viscosity;
        }
    }

    void CalculateAcceleration()
    {
        Vector3 allForces = new Vector3();
        for (int i = 0; i < allParticlesCount; i++)
        {
            allForces = allParticles[i].GetComponent<Properties>().pressureForce + allParticles[i].GetComponent<Properties>().viscosityForce + gravity;
            allParticles[i].GetComponent<Properties>().acceleration = allForces / allParticles[i].GetComponent<Properties>().density;
        }
    }

    void CalculateVelocityandPosition()
    {
        Vector3 newVel = new Vector3();
        Vector3 newPos = new Vector3();
        for (int i = 0; i < allParticlesCount; i++)
        {
            newVel = allParticles[i].GetComponent<Properties>().velocity + timeStep * allParticles[i].GetComponent<Properties>().acceleration;

            allParticles[i].GetComponent<Properties>().velocity = newVel*friction;
        }
        for (int i = 0;i < particleCount; i++)
        {
            newPos = particles[i].transform.position + timeStep * particles[i].GetComponent<Properties>().velocity;
            particles[i].transform.position = newPos;
        }
    }
}
