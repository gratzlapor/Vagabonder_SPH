using System.Runtime.InteropServices;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using static UnityEngine.ParticleSystem;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 68)]
public struct Particle // 68bytes
{
    public float density;
    public float pressure;
    public float3 pressureForce;
    public float3 viscosityForce;
    public float3 acceleration;
    public float3 velocity;
    public float3 position;
}
public class ComputeSetup : MonoBehaviour
{
    ComputeBuffer buffer;
    public ComputeShader computeShader;
    int calcVariablesKernel;
    int calcForcesKernel;
    int calcPosition;
    public Particle[] Particles; // 64 water particles + 52 wall particles
    public GameObject[] renderedParticles;
    Vector3 wCountVector = new Vector3(8,6,9); // 8*6*8 = 432
    int wCountInt;
    int wallSize = 20; // 2168
    int threads = 100; // Frissítsd az gpu oldalt is

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ParticleSetup();
        RenderSetup();
        BufferAndDispatchSetup();
        RenderParticles(Particles.Length);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        computeShader.Dispatch(calcVariablesKernel, Particles.Length / threads, 1, 1);
        computeShader.Dispatch(calcForcesKernel, Particles.Length / threads, 1, 1);
        computeShader.Dispatch(calcPosition, Particles.Length / threads, 1, 1);

        buffer.GetData(Particles);

        RenderParticles(wCountInt);

        for (int i = wCountInt; i < Particles.Length; i++)
        {
            Particles[i].position = renderedParticles[i].transform.position;
        }

        buffer.SetData(Particles);
    }

    private void OnDestroy()
    {
        buffer.Dispose();
    }

    void ParticleSetup()
    {
        wCountInt = (int)(wCountVector.x * wCountVector.y * wCountVector.z);
        Particles = new Particle[wCountInt + 2168];
        int counter = 0;

        // Water
        for (int i = 0; i < wCountVector.x; i++)
        {
            for (int j = 0; j < wCountVector.y; j++)
            {
                for (int k = 0; k < wCountVector.z; k++)
                {
                    Particles[counter] = new Particle();
                    Particles[counter].position = new Vector3(transform.position.x+i, transform.position.y+j, transform.position.z+k);
                    counter++;
                }
            }
        }

        //Walls
        for (int i = -10; i < wallSize - 10; i++)
        {
            for (int j = 0; j < wallSize; j++)
            {
                for (int k = 0; k < wallSize; k++)
                {
                    if(i!=-10 && i != 9 && j!=0 && j!=19 && k == 1)
                    {
                        k += 18;
                    }
                    Particles[counter] = new Particle();
                    Particles[counter].position = new Vector3(k, i, j);
                    counter++;
                }
            }
        }
    }

    void RenderSetup()
    {
        renderedParticles = new GameObject[Particles.Length];
        GameObject wallParent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallParent.transform.position = new Vector3(10, 0, 10);
        wallParent.transform.localScale = new Vector3(20, 20, 20);
        wallParent.GetComponent<MeshRenderer>().enabled = false;
        for (int i = 0; i < Particles.Length; i++)
        {
            if (i < wCountInt)
            {
                renderedParticles[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                renderedParticles[i].transform.localScale = new Vector3(1f, 1f, 1f);
            }
            else
            {
                renderedParticles[i] = new GameObject();
                renderedParticles[i].transform.SetParent(wallParent.transform,false);
            }

            renderedParticles[i].name = "p_" + i;
        }
    }

    void BufferAndDispatchSetup()
    {
        buffer = new ComputeBuffer(Particles.Length, 68);

        buffer.SetData(Particles);

        calcVariablesKernel = computeShader.FindKernel("CalculateVariables");
        calcForcesKernel = computeShader.FindKernel("CalculateForces");
        calcPosition = computeShader.FindKernel("CalculatePosition");

        computeShader.SetFloat("pi", 3.1415f);
        computeShader.SetFloat("mass", 80f); // 0.004
        computeShader.SetFloat("radius", 1.1f); // 0.1
        computeShader.SetFloat("radius2", 1.21f); 
        computeShader.SetFloat("gasConstant", 500f); // 2
        computeShader.SetFloat("restDensity", 1f); // 1
        computeShader.SetFloat("fluidResistance", 0.1f); // 0.001
        computeShader.SetFloat("gravity", -9.81f);
        computeShader.SetFloat("timeStep", 0.009f); // 0.007
        computeShader.SetFloat("friction", 0.99f);
        computeShader.SetInt("wParticleCount", wCountInt);
        computeShader.SetInt("allParticlesCount", Particles.Length); 

        computeShader.SetBuffer(calcVariablesKernel, "Particles",buffer);
        computeShader.SetBuffer(calcForcesKernel, "Particles", buffer);
        computeShader.SetBuffer(calcPosition, "Particles", buffer);
    }

    void RenderParticles(int stop)
    {
        for (int i = 0; i < stop; i++)
        {
            renderedParticles[i].transform.position = Particles[i].position;
        }
    }
}
