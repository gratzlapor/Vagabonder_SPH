using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
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

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct BoundaryParticle // 68bytes
{
    public float boundaryMass;
    public float3 position;
}
public class ComputeSetup : MonoBehaviour
{
    ComputeBuffer particleBuffer;
    ComputeBuffer boundaryBuffer;
    public ComputeShader computeShader;
    int calcVariablesKernel;
    int calcForcesKernel;
    int calcPosition;
    public Particle[] Particles;
    public Particle[] boundaryParticles;
    GameObject[] renderedFluidParticles;
    GameObject[] renderedBoundaryParticles;
    Vector3 wCountVector = new Vector3(8,6,9); // 8*6*8 = 432
    public int wCountInt;
    public int bCountInt; // 2168
    int wallSize = 20;
    int threads = 16; // Frissítsd az gpu oldalt is

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ParticleSetup();
        RenderSetup();
        BufferAndDispatchSetup();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        computeShader.Dispatch(calcVariablesKernel, (Particles.Length) / threads, 1, 1);
        computeShader.Dispatch(calcForcesKernel, (Particles.Length) / threads, 1, 1);
        computeShader.Dispatch(calcPosition, (Particles.Length) / threads, 1, 1);

        particleBuffer.GetData(Particles);
        boundaryBuffer.GetData(boundaryParticles);

        RenderParticlesUpdate();
        RenderBoundaryParticlesUpdate();

        particleBuffer.SetData(Particles);
        boundaryBuffer.SetData(boundaryParticles);

    }

    private void OnDestroy()
    {
        particleBuffer.Dispose();
        boundaryBuffer.Dispose();
    }

    void ParticleSetup()
    {
        List<Particle> fluidList = new List<Particle>();

        // Water
        for (int i = 0; i < wCountVector.x; i++)
        {
            for (int j = 0; j < wCountVector.y; j++)
            {
                for (int k = 0; k < wCountVector.z; k++)
                {
                    Particle p = new Particle();
                    p.position = new Vector3(transform.position.x+i, transform.position.y+j, transform.position.z+k);
                    fluidList.Add(p);
                }
            }
        }
        Particles = fluidList.ToArray();
        wCountInt = Particles.Length;


        List<Particle> boundaryList = new List<Particle>();
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
                    Particle p = new Particle();
                    p.position = new Vector3(k, i, j);
                    boundaryList.Add(p);
                }
            }
        }
        boundaryParticles = boundaryList.ToArray();
        bCountInt = boundaryParticles.Length;
    }

    void RenderSetup()
    {
        renderedFluidParticles = new GameObject[Particles.Length];
        renderedBoundaryParticles = new GameObject[boundaryParticles.Length];
        GameObject wallParent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallParent.transform.position = new Vector3(10, 0, 10);
        wallParent.transform.localScale = new Vector3(20, 20, 20);
        wallParent.GetComponent<MeshRenderer>().enabled = false;
        for (int i = 0; i < Particles.Length; i++)
        {
            renderedFluidParticles[i] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            renderedFluidParticles[i].transform.localScale = new Vector3(1f, 1f, 1f);
            renderedFluidParticles[i].name = "p_" + i;
            renderedFluidParticles[i].transform.position = Particles[i].position;
        }
        for(int i = 0;i < boundaryParticles.Length;i++)
        {
            renderedBoundaryParticles[i] = new GameObject();
            renderedBoundaryParticles[i].transform.SetParent(wallParent.transform, false);
            renderedBoundaryParticles[i].name = "b_" + i;
            renderedBoundaryParticles[i].transform.position = boundaryParticles[i].position;
        }
    }

    void BufferAndDispatchSetup()
    {
        particleBuffer = new ComputeBuffer(Particles.Length, 68);
        boundaryBuffer = new ComputeBuffer(boundaryParticles.Length, 68);

        particleBuffer.SetData(Particles);
        boundaryBuffer.SetData(boundaryParticles);

        calcVariablesKernel = computeShader.FindKernel("CalculateVariables");
        calcForcesKernel = computeShader.FindKernel("CalculateForces");
        calcPosition = computeShader.FindKernel("CalculatePosition");

        computeShader.SetFloat("pi", 3.1415f);
        computeShader.SetFloat("mass", 1f);
        computeShader.SetFloat("radius", 2f);
        computeShader.SetFloat("radius2", 4f); 
        computeShader.SetFloat("gasConstant", 20f);
        computeShader.SetFloat("exponent", 7f);
        computeShader.SetFloat("restDensity", 0.35f);
        computeShader.SetFloat("fluidResistance", 0.1f);
        computeShader.SetFloat("gravity", -9.81f);
        computeShader.SetFloat("timeStep", 0.01f);
        computeShader.SetFloat("friction", 0.99f);
        computeShader.SetInt("wParticleCount", wCountInt);
        computeShader.SetInt("bParticleCount", bCountInt);
        computeShader.SetInt("allParticlesCount", Particles.Length + boundaryParticles.Length); 

        computeShader.SetBuffer(calcVariablesKernel, "Particles",particleBuffer);
        computeShader.SetBuffer(calcForcesKernel, "Particles", particleBuffer);
        computeShader.SetBuffer(calcPosition, "Particles", particleBuffer);

        computeShader.SetBuffer(calcVariablesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcForcesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcPosition, "boundaryParticles", boundaryBuffer);
    }

    void RenderParticlesUpdate()
    {
        for (int i = 0; i < Particles.Length; i++)
        {
            renderedFluidParticles[i].transform.position = Particles[i].position;
        }
    }

    void RenderBoundaryParticlesUpdate()
    {
        for (int i = 0; i < boundaryParticles.Length; i++)
        {
            boundaryParticles[i].position = renderedBoundaryParticles[i].transform.position;
        }
    }
}
