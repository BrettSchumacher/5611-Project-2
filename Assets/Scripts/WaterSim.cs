using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSim : MonoBehaviour
{
    public ComputeShader waterSimShader;
    public Light directionalLight;
    public int numSpheres;

    //ray tracer variables
    public Color waterColor;

    //simulation variables
    public int passes = 1;
    public float influence_radius;
    public float k_cohesion;
    public float k_surf;
    public float cohesion_falloff;
    public float surf_falloff;
    public float goal_pressure;
    public float drag;
    public float grav;
    public float boundaryAbs = 0.5f;
    public BoxCollider box;
    public GameObject sphere;

    private Camera cam;
    private Vector3 minBounds;
    private Vector3 maxBounds;

    private List<Vector3> spherePos;
    private List<Vector3> oldSpherePos;
    private List<Vector3> sphereVel;
    private List<Vector3> oldSphereVel;
    private List<GameObject> spheres;

    // Start is called before the first frame update
    void Start()
    {
        minBounds = box.transform.position + box.center - (box.size / 2.0f);
        maxBounds = box.transform.position + box.center + (box.size / 2.0f);
        cam = Camera.main;
        GenerateSpheres();
        SetShaderParams();
    }

    void GenerateSpheres()
    {
        spherePos = new List<Vector3>();
        oldSpherePos = new List<Vector3>();
        sphereVel = new List<Vector3>();
        oldSphereVel = new List<Vector3>();
        spheres = new List<GameObject>();

        GameObject tempSphere;
        Vector3 temp;
        float spacing = 0.005f;
        int xNum = Mathf.FloorToInt((maxBounds.x - minBounds.x) / spacing) - 2;
        int zNum = Mathf.FloorToInt((maxBounds.z - minBounds.z) / spacing) - 2;

        for (int i = 0; i < numSpheres; i++)
        {
            temp = new Vector3((i % xNum + 1) * spacing + minBounds.x, (i / (xNum * zNum) + 1) * spacing + minBounds.y, ((i / xNum) % zNum + 1) * spacing + minBounds.z);
            spherePos.Add(temp);
            oldSpherePos.Add(spherePos[i]);
            sphereVel.Add(Vector3.zero);
            oldSphereVel.Add(Vector3.zero);
            tempSphere = Instantiate(sphere);
            spheres.Add(tempSphere);
        }

        float pressure;
        float dist;
        //calc initial pressures
        for (int i = 0; i < numSpheres; i++)
        {
            pressure = 0f;
            for (int j = 0; j < numSpheres; j++)
            {
                if (i == j) continue;
                dist = (spherePos[i] - spherePos[j]).magnitude / influence_radius;
                if (dist < 1f)
                {
                    pressure += 1f / (cohesion_falloff * dist * dist + 0.5f);
                }
            }

            print("Starting pressure " + i + ": " + pressure);
        }
    }

    void SetShaderParams()
    {
        waterSimShader.SetFloat("influenceRad", influence_radius);
        waterSimShader.SetFloat("k_cohesion", k_cohesion);
        waterSimShader.SetFloat("k_surf", k_surf);
        waterSimShader.SetFloat("cohesion_falloff", cohesion_falloff);
        waterSimShader.SetFloat("surf_falloff", surf_falloff);
        waterSimShader.SetFloat("goal_pressure", goal_pressure);
        waterSimShader.SetFloat("drag", drag);
        waterSimShader.SetFloat("grav", grav);
        waterSimShader.SetFloat("boundaryAbs", boundaryAbs);
        waterSimShader.SetVector("minBounds", minBounds);
        waterSimShader.SetVector("maxBounds", maxBounds);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void FixedUpdate()
    {
        ComputeBuffer spherePosBuffer = new ComputeBuffer(spherePos.Count, 12);
        ComputeBuffer oldSpherePosBuffer = new ComputeBuffer(spherePos.Count, 12);
        ComputeBuffer sphereVelBuffer = new ComputeBuffer(sphereVel.Count, 12);
        ComputeBuffer oldSphereVelBuffer = new ComputeBuffer(sphereVel.Count, 12);
        spherePosBuffer.SetData(spherePos);
        oldSpherePosBuffer.SetData(oldSpherePos);
        sphereVelBuffer.SetData(sphereVel);
        oldSphereVelBuffer.SetData(oldSphereVel);
        waterSimShader.SetBuffer(0, "spherePos", spherePosBuffer);
        waterSimShader.SetBuffer(0, "oldSpherePos", oldSpherePosBuffer);
        waterSimShader.SetBuffer(0, "sphereVel", sphereVelBuffer);
        waterSimShader.SetBuffer(0, "oldSphereVel", oldSphereVelBuffer);
        int threadGroups = Mathf.CeilToInt(numSpheres / 64.0f);

        float dt = Time.fixedDeltaTime / passes;
        waterSimShader.SetFloat("dt", dt);
        waterSimShader.SetInt("numSpheres", numSpheres);

        for (int i = 0; i < passes; i++)
        {
            waterSimShader.Dispatch(0, threadGroups, 1, 1);
        }

        Vector3[] temp = new Vector3[numSpheres];
        spherePosBuffer.GetData(temp);
        spherePos.Clear();
        spherePos.AddRange(temp);

        oldSpherePosBuffer.GetData(temp);
        oldSpherePos.Clear();
        oldSpherePos.AddRange(temp);

        sphereVelBuffer.GetData(temp);
        sphereVel.Clear();
        sphereVel.AddRange(temp);

        oldSphereVelBuffer.GetData(temp);
        oldSphereVel.Clear();
        oldSphereVel.AddRange(temp);

        spherePosBuffer.Release();
        oldSpherePosBuffer.Release();
        sphereVelBuffer.Release();
        oldSphereVelBuffer.Release();

        for (int i = 0; i < numSpheres; i++)
        {
            spheres[i].transform.position = spherePos[i];
        }
    }
}
