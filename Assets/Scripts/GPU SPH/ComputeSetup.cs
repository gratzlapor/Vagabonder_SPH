using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.VisualScripting;
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
[StructLayout(LayoutKind.Sequential, Size = 12)]
public struct BoundaryParticle // 16bytes
{
    public float3 position;
}
public class ComputeSetup : MonoBehaviour
{
    ComputeBuffer particleBuffer;
    ComputeBuffer boundaryBuffer;
    ComputeBuffer startingPosBuffer;
    public ComputeShader computeShader;
    int calcVariablesKernel;
    int calcForcesKernel;
    int calcPosition;
    Particle[] Particles;
    public BoundaryParticle[] boundaryParticles;
    Vector4[] startingPositions;
    GameObject[] renderedBoundaryParticles;
    Vector3 wCountVector = new Vector3(10,32,32); 
    int wCountInt;
    int bCountInt;
    int threads = 256; // Frissítsd az gpu oldalt is

    [Header("Render")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Material particleMaterial;
    Bounds renderBounds;


    [Header("Variables")]
    public float spacing;
    public float radius;

    [Header("Adjustable Variables")]
    public float spacingMultiplier;
    public float restDensity;
    public float boundaryMass;
    public float wind;
    public float pressureMin;
    public float pressureMax;

    [Header("Plane")]
    [SerializeField] Mesh planeMesh;
    [SerializeField] Transform planeTransform;

    private void Awake()
    {
        ParticleSetup();
        CalculateSpacing();
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

        particleMaterial.SetFloat("_PressureMin", pressureMin);
        particleMaterial.SetFloat("_PressureMax", pressureMax);
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, renderBounds, Particles.Length);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        computeShader.Dispatch(calcVariablesKernel, Particles.Length / threads, 1, 1);
        computeShader.Dispatch(calcForcesKernel, Particles.Length / threads, 1, 1);
        computeShader.Dispatch(calcPosition, Particles.Length / threads, 1, 1);

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
        computeShader.SetFloat("boundaryMass", boundaryMass);
        computeShader.SetFloat("wind", wind);
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
        Gizmos.DrawWireCube(new Vector3(transform.position.x,transform.position.y,transform.position.z+16), new Vector3(wCountVector.x,wCountVector.y,wCountVector.z+32));
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
                    p.position = new Vector3(transform.position.x+i - adjustx, transform.position.y+j- adjusty, transform.position.z+k - adjustz);
                    fluidList.Add(p);
                    startingPositionsList.Add(new Vector4(p.position.x, p.position.y, p.position.z, 0f));
                }
            }
        }
        Particles = fluidList.ToArray();
        startingPositions = startingPositionsList.ToArray();
        wCountInt = Particles.Length;


        List<BoundaryParticle> boundaryList = new List<BoundaryParticle>();
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
        boundaryParticles = new BoundaryParticle[Vertices.Length];
        for (int i =0; i < Vertices.Length; i++)
        {
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

    void CalculateSpacing()
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

        boundaryMass = 4.5f;

        spacingMultiplier = 1.45f;

        radius = spacing * spacingMultiplier;

        restDensity = 0.90f;

        wind = 5.5f;
    }

    void BufferAndDispatchSetup()
    {
        particleBuffer = new ComputeBuffer(Particles.Length, 68);
        boundaryBuffer = new ComputeBuffer(boundaryParticles.Length, 12);
        startingPosBuffer = new ComputeBuffer(startingPositions.Length, sizeof(float) * 4);

        particleBuffer.SetData(Particles);
        boundaryBuffer.SetData(boundaryParticles);
        startingPosBuffer.SetData(startingPositions);


        calcVariablesKernel = computeShader.FindKernel("CalculateVariables");
        calcForcesKernel = computeShader.FindKernel("CalculateForces");
        calcPosition = computeShader.FindKernel("CalculatePosition");

        computeShader.SetFloat("pi", 3.1415f);
        computeShader.SetFloat("mass", 1f);
        computeShader.SetFloat("boundaryMass", boundaryMass);
        computeShader.SetFloat("spacingMultiplier", spacingMultiplier);
        computeShader.SetFloat("radius", radius);
        computeShader.SetFloat("radius2", radius*radius); 
        computeShader.SetFloat("gasConstant", 80f);
        computeShader.SetFloat("exponent", 7f);
        computeShader.SetFloat("restDensity", restDensity);
        computeShader.SetFloat("fluidResistance", 0.1f);
        computeShader.SetFloat("gravity", -9.81f);
        computeShader.SetFloat("timeStep", 0.008f);
        computeShader.SetFloat("friction", 0.997f);
        computeShader.SetFloat("wind", wind);


        computeShader.SetVector("boxPosition", new Vector4(transform.position.x, transform.position.y, transform.position.z));
        computeShader.SetVector("boxSize", new Vector4(wCountVector.x, wCountVector.y, wCountVector.z));

        computeShader.SetInt("wParticleCount", wCountInt);
        computeShader.SetInt("bParticleCount", bCountInt);
        computeShader.SetInt("allParticlesCount", Particles.Length + boundaryParticles.Length);

        computeShader.SetVectorArray("startingPositions", startingPositions);

        computeShader.SetBuffer(calcVariablesKernel, "Particles",particleBuffer);
        computeShader.SetBuffer(calcForcesKernel, "Particles", particleBuffer);
        computeShader.SetBuffer(calcPosition, "Particles", particleBuffer);

        computeShader.SetBuffer(calcVariablesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcForcesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcPosition, "boundaryParticles", boundaryBuffer);

        computeShader.SetBuffer(calcPosition, "startingPositions", startingPosBuffer);

        particleMaterial.SetBuffer("Particles", particleBuffer);
    }

    void RenderBoundaryParticlesUpdate()
    {
        for (int i = 0; i < boundaryParticles.Length; i++)
        {
            boundaryParticles[i].position = renderedBoundaryParticles[i].transform.position;
        }
    }
}
