using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

// Fluvial plane 7 alt:
//      SPH: 67, 43.25, -23.5
//      Lassítás: 0.5
//      Fluid: 12,16,32

[System.Serializable]
[StructLayout(LayoutKind.Sequential, Size = 72)]
public struct MovingParticle // 68bytes
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
[StructLayout(LayoutKind.Sequential, Size = 20)]
public struct BoundaryParticle // 20bytes
{
    public float3 position;
    public float erodable;
    public float isEroded;
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
    int fluvLandFormingKernel;


    MovingParticle[] Particles;
    public BoundaryParticle[] boundaryParticles;
    Vector4[] startingPositions;
    Vector3 wCountVector = new Vector3(4,8,32); 
    int wCountInt;
    int bCountInt;
    int threads = 256; // Frissítsd az gpu oldalt is

    [Header("Render")]
    [SerializeField] Mesh particleMesh;
    [SerializeField] Material particleMaterial;
    [SerializeField] Material planeMaterial;
    Bounds renderBounds;


    [Header("Variables")]
    float spacing;
    float radius;
    float boundaryRadius;
    float spacingMultiplier;
    float restDensity;
    float fluidMass;
    float boundaryMass;

    [Header("Adjustable Variables")]
    public bool fastForward = true;
    public float pressureMin;
    public float pressureMax;
    public int accelerate;

    [Header("Plane")]
    [SerializeField] Mesh planeMesh;
    [SerializeField] Transform planeTransform;

    [Header("UI")]
    public Slider accelerator;

    private void Awake()
    {
        ParticleSetup();
        VariablesSetup();
        BufferAndDispatchSetup();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        renderBounds = new Bounds(transform.position, Vector3.one * 1000f);
        planeTransform.GetComponent<MeshRenderer>().material.SetBuffer("boundaryParticles", boundaryBuffer);
    }

    void Update()
    {
        particleMaterial.SetBuffer("Particles", particleBuffer);
        planeMaterial.SetBuffer("boundaryParticles", boundaryBuffer);

        particleMaterial.SetFloat("_PressureMin", pressureMin);
        particleMaterial.SetFloat("_PressureMax", pressureMax);
        Graphics.DrawMeshInstancedProcedural(particleMesh, 0, particleMaterial, renderBounds, Particles.Length);

        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogError("Compute shaders not supported!");
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        int temp = accelerate;

        for (int i = 0; i < temp; i++)
        {
            computeShader.Dispatch(calcVariablesKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(calcForcesKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(calcPositionKernel, Particles.Length / threads, 1, 1);
            computeShader.Dispatch(fluvLandFormingKernel, boundaryParticles.Length / 203, 1, 1);
        }

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
        List<MovingParticle> fluidList = new List<MovingParticle>();
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
                    MovingParticle p = new MovingParticle();
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
            boundaryParticles[i].isEroded = 0;
        }

        bCountInt = boundaryParticles.Length;
    }

    void VariablesSetup()
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

        pressureMin = 0;
        pressureMax = 50f;

        accelerate = (int)accelerator.value;
    }

    void BufferAndDispatchSetup()
    {
        particleBuffer = new ComputeBuffer(Particles.Length, 72);
        boundaryBuffer = new ComputeBuffer(boundaryParticles.Length, 20);
        startingPosBuffer = new ComputeBuffer(startingPositions.Length, sizeof(float) * 4);

        particleBuffer.SetData(Particles);
        boundaryBuffer.SetData(boundaryParticles);
        startingPosBuffer.SetData(startingPositions);


        calcVariablesKernel = computeShader.FindKernel("CalculateVariables");
        calcForcesKernel = computeShader.FindKernel("CalculateForces");
        calcPositionKernel = computeShader.FindKernel("CalculatePosition");
        fluvLandFormingKernel = computeShader.FindKernel("FluvialLandforming");

        computeShader.SetFloat("pi", 3.1415f);
        computeShader.SetFloat("mass", fluidMass);
        computeShader.SetFloat("boundaryMass", boundaryMass);
        computeShader.SetFloat("spacingMultiplier", spacingMultiplier);
        computeShader.SetFloat("radius", radius);
        computeShader.SetFloat("radius2", radius*radius);
        computeShader.SetFloat("depositionRadius", 3f);
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
        computeShader.SetBuffer(fluvLandFormingKernel, "Particles", particleBuffer);

        computeShader.SetBuffer(calcVariablesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcForcesKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(calcPositionKernel, "boundaryParticles", boundaryBuffer);
        computeShader.SetBuffer(fluvLandFormingKernel, "boundaryParticles", boundaryBuffer);

        computeShader.SetBuffer(calcPositionKernel, "startingPositions", startingPosBuffer);

        particleMaterial.SetBuffer("Particles", particleBuffer);
        planeMaterial.SetBuffer("boundaryParticles", boundaryBuffer);

    }

    public void UpdateAccelerator()
    {
        accelerate = (int)accelerator.value;
        Debug.Log("Acceleration set to: " + accelerate);
    }

    public void ExitGame()
    {
        Application.Quit();
    }
}
