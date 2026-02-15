using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;
using static UnityEngine.ParticleSystem;

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 72)]
public struct Particle // 68bytes
{
    public float density;
    public float pressure;
    public float3 pressureForce;
    public float3 viscosityForce;
    public float3 acceleration;
    public float3 velocity;
    public float3 position;
    public float starter;
}

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct BoundaryParticle // 16bytes
{
    public float3 position;
    public float erodable;
}

public class ComputeSetup : MonoBehaviour
{
    ComputeBuffer particleBuffer;
    ComputeBuffer boundaryBuffer;
    ComputeBuffer startingPosBuffer;
    public ComputeShader computeShader;

    [Header("Kernels")]
    int calcVariablesKernel;
    int calcForcesKernel;
    int calcPositionKernel;
    int fluvHeightGainKernel;
    int fluvHeightLostKernel;
    int heightSmoothingKernel;


    Particle[] Particles;
    public BoundaryParticle[] boundaryParticles;
    Vector4[] startingPositions;
    GameObject[] renderedBoundaryParticles;
    Vector3 wCountVector = new Vector3(12,16,32); 
    int wCountInt;
    int bCountInt;
    int threads = 256; // Frissítsd az gpu oldalt is

    [Header("Render")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Material particleMaterial;
    [SerializeField] Material planeMaterial;
    Bounds renderBounds;


    [Header("Variables")]
    public float spacing;
    public float radius;
    public float smoothingRadius;

    [Header("Adjustable Variables")]
    public bool fastForward = false;
    public float spacingMultiplier;
    public float restDensity;
    public float fluidMass;
    public float boundaryMass;
    public float wind;
    public float pressureMin;
    public float pressureMax;
    public float boundaryRadius;

    [Header("Plane")]
    [SerializeField] Mesh planeMesh;
    [SerializeField] Transform planeTransform;

    private void Awake()
    {
        ParticleSetup();
        CalculateVariables();
        BufferAndDispatchSetup();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        renderBounds = new Bounds(transform.position, Vector3.one * 1000f);
    }

    void Update()
    {
        particleMaterial.SetBuffer("Particles", particleBuffer);
        planeMaterial.SetBuffer("boundaryParticles", boundaryBuffer);

        particleMaterial.SetFloat("_PressureMin", pressureMin);
        particleMaterial.SetFloat("_PressureMax", pressureMax);
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, renderBounds, Particles.Length);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (fastForward)
        {
            for (int i = 0; i < 3; i++)
            {
                computeShader.Dispatch(calcVariablesKernel, Particles.Length / threads, 1, 1);
                computeShader.Dispatch(calcForcesKernel, Particles.Length / threads, 1, 1);
                computeShader.Dispatch(calcPositionKernel, Particles.Length / threads, 1, 1);
                computeShader.Dispatch(fluvHeightLostKernel, boundaryParticles.Length / 203, 1, 1);
            }
        }
        else
        {
            computeShader.Dispatch(calcVariablesKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(calcForcesKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(calcPositionKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(fluvHeightLostKernel, boundaryParticles.Length / 203, 1, 1);
        }

        //computeShader.Dispatch(heightSmoothingKernel, boundaryParticles.Length / 320, 1, 1);

        //boundaryBuffer.GetData(boundaryParticles);

        //RenderBoundaryParticlesUpdate();

        //boundaryBuffer.SetData(boundaryParticles);


        //if (Time.frameCount % 60 != 0) return;

        //particleBuffer.GetData(Particles);

        //float min = float.MaxValue;
        //float max = float.MinValue;

        //for (int i = 0; i < Particles.Length; i++)
        //{
        //    min = Mathf.Min(min, Particles[i].pressure);
        //    max = Mathf.Max(max, Particles[i].pressure);
        //}

        //Debug.Log($"Density range: {min} → {max}");

        radius = spacing * spacingMultiplier;
        computeShader.SetFloat("radius", radius);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("mass", fluidMass);
        computeShader.SetFloat("boundaryMass", boundaryMass);
        computeShader.SetFloat("wind", wind);
        computeShader.SetFloat("boundaryRadius", boundaryRadius);
        computeShader.SetFloat("boundaryRadius2", boundaryRadius* boundaryRadius);
    }

    private void OnDestroy()
    {
        particleBuffer.Dispose();
        boundaryBuffer.Dispose();
        startingPosBuffer.Dispose();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(new Vector3(transform.position.x,transform.position.y,transform.position.z+28.5f), new Vector3(wCountVector.x,wCountVector.y*2,wCountVector.z+57));
    }

    void ParticleSetup()
    {
        List<Particle> fluidList = new List<Particle>();
        List<Vector4> startingPositionsList = new List<Vector4>();
        int adjustx = (int)wCountVector.x / 2;
        int adjusty = (int)wCountVector.y / 2;
        int adjustz = (int)wCountVector.z / 2;

        // Water
        for (int i = 0; i < wCountVector.x; i++)
        {
            for (int j = 0; j < wCountVector.y; j++)
            {
                for (int k = 0; k < wCountVector.z; k++)
                {
                    Particle p = new Particle();
                    p.starter = 1;
                    p.position = new Vector3(transform.position.x+i - adjustx, transform.position.y+j- adjusty, transform.position.z+k - adjustz);
                    fluidList.Add(p);
                    startingPositionsList.Add(new Vector4(p.position.x, p.position.y, p.position.z, 0f));
                }
            }
        }
        Particles = fluidList.ToArray();
        startingPositions = startingPositionsList.ToArray();
        wCountInt = Particles.Length;


        //List<BoundaryParticle> boundaryList = new List<BoundaryParticle>();
        //int wallSize = 20;
        ////Walls
        //for (int i = -10; i < wallSize - 10; i++)
        //{
        //    for (int j = 0; j < wallSize; j++)
        //    {
        //        for (int k = 0; k < wallSize; k++)
        //        {
        //            if (i != -10 && i != 9 && j != 0 && j != 19 && k == 1)
        //            {
        //                k += 18;
        //            }
        //            BoundaryParticle p = new BoundaryParticle();
        //            p.position = new Vector3(k, i, j);
        //            boundaryList.Add(p);
        //        }
        //    }
        //}

        Vector3[] Vertices = planeMesh.vertices;
        Color[] colors = planeMesh.colors;
        boundaryParticles = new BoundaryParticle[Vertices.Length];
        for (int i =0; i < Vertices.Length; i++)
        {
            bool isHard = colors[i].r > 0.5f;
            if (isHard)
            {
                boundaryParticles[i].erodable = 0;
            }
            else
            {
                boundaryParticles[i].erodable = 1;
            }
            if (Vertices[i].y < -5f)
            {
                Vertices[i].y = -5f;
            }

            boundaryParticles[i].position = planeTransform.TransformPoint(Vertices[i]);
        }

        bCountInt = boundaryParticles.Length;
    }

    void RenderSetup()
    {
        renderedBoundaryParticles = new GameObject[boundaryParticles.Length];
        GameObject wallParent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wallParent.transform.position = new Vector3(10, 0, 10);
        wallParent.transform.localScale = new Vector3(20, 20, 20);
        wallParent.GetComponent<MeshRenderer>().enabled = false;

        for (int i = 0; i < boundaryParticles.Length; i++)
        {
            renderedBoundaryParticles[i] = new GameObject();
            renderedBoundaryParticles[i].transform.SetParent(planeTransform.transform, false); // Amúgy a kikommentezett wallParent volt
            renderedBoundaryParticles[i].name = "b_" + i;
            renderedBoundaryParticles[i].transform.position = boundaryParticles[i].position;
        }
    }

    void CalculateVariables()
    {

        Vector3 p0 = boundaryParticles[boundaryParticles.Length/2].position;
        float minDist = float.MaxValue;

        for (int i = 1; i < boundaryParticles.Length; i++)
        {
            float d = Vector3.Distance(p0, boundaryParticles[i].position);
            if (d > 0f && d < minDist)
                minDist = d;
        }

        spacing = minDist;

        boundaryMass = 10f;
        fluidMass = 1f;

        spacingMultiplier = 1.45f;

        radius = spacing * spacingMultiplier;
        boundaryRadius = 1.6f;

        restDensity = 0.90f;

        wind = 0f;

        smoothingRadius = 1;

        pressureMin = 0;
        pressureMax = 150f;
    }

    void BufferAndDispatchSetup()
    {
        particleBuffer = new ComputeBuffer(Particles.Length, 72);
        boundaryBuffer = new ComputeBuffer(boundaryParticles.Length, 16);
        startingPosBuffer = new ComputeBuffer(startingPositions.Length, sizeof(float) * 4);

        particleBuffer.SetData(Particles);
        boundaryBuffer.SetData(boundaryParticles);
        startingPosBuffer.SetData(startingPositions);


        calcVariablesKernel = computeShader.FindKernel("CalculateVariables");
        calcForcesKernel = computeShader.FindKernel("CalculateForces");
        calcPositionKernel = computeShader.FindKernel("CalculatePosition");
        fluvHeightGainKernel = computeShader.FindKernel("FluvialHeightGain");
        fluvHeightLostKernel = computeShader.FindKernel("FluvialHeightLost");
        heightSmoothingKernel = computeShader.FindKernel("HeightSmoothing");

        computeShader.SetFloat("pi", 3.1415f);
        computeShader.SetFloat("mass", fluidMass);
        computeShader.SetFloat("boundaryMass", boundaryMass);
        computeShader.SetFloat("spacingMultiplier", spacingMultiplier);
        computeShader.SetFloat("radius", radius);
        computeShader.SetFloat("radius2", radius*radius);
        computeShader.SetFloat("depositionRadius", 4);
        computeShader.SetFloat("boundaryRadius", boundaryRadius);
        computeShader.SetFloat("boundaryRadius2", boundaryRadius * boundaryRadius);
        computeShader.SetFloat("erosionRadius", Mathf.Pow(radius,7));
        computeShader.SetFloat("smoothRadius", spacing/2);
        computeShader.SetFloat("gasConstant", 10f);
        computeShader.SetFloat("exponent", 7f);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("fluidResistance", 1f);
        computeShader.SetFloat("gravity", -9.81f);
        computeShader.SetFloat("timeStep", 0.008f);
        computeShader.SetFloat("friction", 1f);
        computeShader.SetFloat("wind", wind);


        computeShader.SetVector("boxPosition", new Vector4(transform.position.x, transform.position.y, transform.position.z));
        computeShader.SetVector("boxSize", new Vector4(wCountVector.x, wCountVector.y, wCountVector.z));
        computeShader.SetVector("planePosition", new Vector4(planeTransform.position.x, planeTransform.position.y, planeTransform.position.z));
        computeShader.SetVector("planeSize", new Vector4(60, 10, 200));

        computeShader.SetInt("wParticleCount", wCountInt);
        computeShader.SetInt("bParticleCount", bCountInt);
        computeShader.SetInt("allParticlesCount", Particles.Length + boundaryParticles.Length);

        computeShader.SetVectorArray("startingPositions", startingPositions);

        computeShader.SetMatrix("_WorldToLocal", planeTransform.worldToLocalMatrix);

        computeShader.SetBuffer(calcVariablesKernel, "Particles",particleBuffer);
        computeShader.SetBuffer(calcForcesKernel, "Particles", particleBuffer);
        computeShader.SetBuffer(calcPositionKernel, "Particles", particleBuffer);
        computeShader.SetBuffer(fluvHeightLostKernel, "Particles", particleBuffer);

        computeShader.SetBuffer(calcVariablesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcForcesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcPositionKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(fluvHeightGainKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(fluvHeightLostKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(heightSmoothingKernel, "boundaryParticles", boundaryBuffer);

        computeShader.SetBuffer(calcPositionKernel, "startingPositions", startingPosBuffer);

        particleMaterial.SetBuffer("Particles", particleBuffer);
        planeMaterial.SetBuffer("boundaryParticles", boundaryBuffer);
    }

    void RenderBoundaryParticlesUpdate()
    {
        for (int i = 0; i < boundaryParticles.Length; i++)
        {
            boundaryParticles[i].position = renderedBoundaryParticles[i].transform.position;
        }
    }
}
