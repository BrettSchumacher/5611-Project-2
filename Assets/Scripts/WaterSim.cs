using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Sphere
{
    public Vector3 pos;
}

public class WaterSim : MonoBehaviour
{
    public ComputeShader waterRayTracer;
    public ComputeShader waterSimShader;
    public Light directionalLight;
    public int numSpheres;

    //ray tracer variables
    [Header("Ray Tracer Variables")]
    [Range(0.001f, 1f)]
    public float detail;
    public Color waterColor;
    public float water_iof;
    [Range(0.001f, 10f)]
    public float k_smooth;
    public float draw_radius;
    public Texture2D skybox;

    //simulation variables
    [Header("Simulation Variables")]
    public float influecne_radius;
    public float k_repel;
    public float grav;
    public BoxCollider box;

    private Camera cam;
    private List<Sphere> spheres;
    private RenderTexture target;
    private int tracerKernel;
    private int simulationKernel;
    private Vector3 minBounds;
    private Vector3 maxBounds;

    // Start is called before the first frame update
    void Start()
    {
        minBounds = box.transform.position + box.center - (box.size / 2.0f);
        maxBounds = box.transform.position + box.center + (box.size / 2.0f);
        cam = Camera.main;
        //tracerKernel = waterRayTracer.FindKernel("WaterRayTracer");
        waterRayTracer.SetTexture(0, "skybox", skybox);
        waterRayTracer.SetFloat("detail", detail);
        waterRayTracer.SetFloat("k_smooth", k_smooth);
        waterRayTracer.SetFloat("sphere_radius", draw_radius);
        waterRayTracer.SetFloat("water_iof", water_iof);
        waterRayTracer.SetVector("water_color", waterColor);
        waterRayTracer.SetVector("minBounds", box.transform.position - (new Vector3(box.size.x, box.size.y, box.size.z) / 2.0f));
        waterRayTracer.SetVector("maxBounds", box.transform.position + (new Vector3(box.size.x, box.size.y, box.size.z) / 2.0f));
        waterRayTracer.SetVector("boundsCenter", box.transform.position + box.center);
        Vector3 l = directionalLight.transform.forward;
        waterRayTracer.SetVector("directionalLight", new Vector4(l.x, l.y, l.z, directionalLight.intensity));

        GenerateSpheres();
    }

    void GenerateSpheres()
    {
        spheres = new List<Sphere>();

        Sphere sphere;

        for (int i = 0; i < numSpheres; i++)
        {
            sphere = new Sphere();
            sphere.pos = new Vector3(Random.Range(minBounds.x, maxBounds.x), Random.Range(minBounds.y, maxBounds.y), Random.Range(minBounds.z, maxBounds.z));
            spheres.Add(sphere);
        }

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        waterRayTracer.SetMatrix("CamToWorld", cam.cameraToWorldMatrix);
        waterRayTracer.SetMatrix("CamInvProj", cam.projectionMatrix.inverse);
        waterRayTracer.SetFloat("detail", detail);
        waterRayTracer.SetFloat("k_smooth", k_smooth);

        ComputeBuffer sphereBuffer = new ComputeBuffer(spheres.Count, 3 * 4);
        List<Vector3> posList = new List<Vector3>();
        for (int i = 0; i < spheres.Count; i++)
        {
            posList.Add(spheres[i].pos);
        }
        sphereBuffer.SetData(posList);
        waterRayTracer.SetBuffer(0, "spheres", sphereBuffer);

        InitRenderTexture();

        waterRayTracer.SetTexture(0, "Result", target);
        int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
        waterRayTracer.Dispatch(0, threadGroupsX, threadGroupsY, 1);

        Graphics.Blit(target, destination);
        sphereBuffer.Release();
    }

    void InitRenderTexture()
    {
        if (target == null || target.width != Screen.width || target.height != Screen.height)
        {
            // Release render texture if we have one
            if (target != null)
            {
                target.Release();
            }

            // Get a render target for Ray Tracing
            target = new RenderTexture(Screen.width, Screen.height, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            target.enableRandomWrite = true;
            target.Create();
        }
    }
}
