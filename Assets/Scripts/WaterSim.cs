using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterSim : MonoBehaviour
{
    public ComputeShader EulerShader;
    public int numPtcls;
    public int passes = 1;
    public int solverIters = 10;
    public float influence_radius;
    public float rest_density = 1000f;
    public float grav = 1f;
    public float eps = 600f;
    public float dq = 0.3f;
    public float k_corr = 0.0001f;
    public float spacing = 0.05f;
    public GameObject waterPtcl;

    private int threadGroupsX;
    private List<Vector3> pos;
    private List<Vector3> newPos;
    private List<Vector3> vel;
    private BoxCollider box;
    private Vector3 minBounds;
    private Vector3 maxBounds;
    private List<GameObject> spheres;

    //all thems buffers
    private ComputeBuffer posBuff;
    private ComputeBuffer newPosBuff;
    private ComputeBuffer velBuff;
    private ComputeBuffer scaleFacBuff;
    private ComputeBuffer adjListsBuff;


    // Start is called before the first frame update
    void Start()
    {
        box = GetComponent<BoxCollider>();
        minBounds = box.transform.position + box.center - (box.size / 2.0f);
        maxBounds = box.transform.position + box.center + (box.size / 2.0f);
        GenerateFluid();
        SetShaderParams();
    }

    void GenerateFluid()
    {
        spheres = new List<GameObject>();
        pos = new List<Vector3>();
        vel = new List<Vector3>();
        newPos = new List<Vector3>();

        GameObject tempPtcl;
        Vector3 temp;
        int xNum = Mathf.FloorToInt((maxBounds.x - minBounds.x) / spacing) - 2;
        int zNum = Mathf.FloorToInt((maxBounds.z - minBounds.z) / spacing) - 2;

        for (int i = 0; i < numPtcls; i++)
        {
            temp = new Vector3((i % xNum + 1) * spacing + minBounds.x, (i / (xNum * zNum) + 1) * spacing + minBounds.y, ((i / xNum) % zNum + 1) * spacing + minBounds.z);
            pos.Add(temp);
            vel.Add(Vector3.zero);
            newPos.Add(Vector3.zero);
            tempPtcl = Instantiate(waterPtcl);
            spheres.Add(tempPtcl);
        }
    }

    void SetShaderParams()
    {
        EulerShader.SetVector("minBounds", minBounds);
        EulerShader.SetVector("maxBounds", maxBounds);
        EulerShader.SetFloat("rest_density", rest_density);
        EulerShader.SetFloat("influence_radius", influence_radius);
        EulerShader.SetFloat("grav", grav);
        EulerShader.SetFloat("k_corr", k_corr);
        EulerShader.SetFloat("eps", eps);
        EulerShader.SetFloat("dq", dq);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        EulerShader.SetInt("numPoints", numPtcls);
        threadGroupsX = Mathf.CeilToInt(numPtcls / 64.0f);
        float dt = Time.fixedDeltaTime / passes;
        EulerShader.SetFloat("dt", dt);
        EulerShader.SetFloat("solver_dt", dt / solverIters);

        SetBuffers();

        float timer = Time.time;
        int i = 0;
        for (; i < passes; i++)
        {
            SinglePass(dt);
        }
        //print("GPU time after " + i + " passes: " + (Time.time - timer).ToString("E") + " seconds");

        ReleaseBuffers();

        for (i = 0; i < numPtcls; i++)
        {
            spheres[i].transform.position = pos[i];
        }
    }

    void PrintPre()
    {
        for (int i = 0; i < numPtcls; i++)
        {
            print(i + " pre-pos: " + pos[i]);
            print(i + " pre-vel: " + vel[i]);
        }
    }

    void PrintPost()
    {
        for (int i = 0; i < numPtcls; i++)
        {
            print(i + " post-pos: " + pos[i]);
            print(i + " post-vel: " + vel[i]);
        }
    }

    void PrintMid(int ker)
    {
        Vector3[] temp = new Vector3[numPtcls];
        newPosBuff.GetData(temp);

        print("ker " + ker + ":");
        for (int i = 0; i < numPtcls; i++)
        {
            print(i + " newPos: " + temp[i]);
        }
    }

    void SinglePass(float dt)
    {
        BindBuffers(0);
        //position initialization
        EulerShader.Dispatch(0, threadGroupsX, 1, 1);

        BindBuffers(1);
        //adjacency list initilization
        EulerShader.Dispatch(1, threadGroupsX, 1, 1);

        //do core loop
        for (int i = 0; i < solverIters; i++)
        {
            BindBuffers(2);
            //calculate correction values and gradients
            EulerShader.Dispatch(2, threadGroupsX, 1, 1);

            BindBuffers(3);
            //set correction factor
            EulerShader.Dispatch(3, threadGroupsX, 1, 1);

            BindBuffers(4);
            //update position
            EulerShader.Dispatch(4, threadGroupsX, 1, 1);
        }

        BindBuffers(5);
        //finalize and apply viscosity correction
        EulerShader.Dispatch(5, threadGroupsX, 1, 1);
    }

    void SetBuffers()
    {
        posBuff = new ComputeBuffer(numPtcls, 12);
        posBuff.SetData(pos);
        newPosBuff = new ComputeBuffer(numPtcls, 12);
        velBuff = new ComputeBuffer(numPtcls, 12);
        velBuff.SetData(vel);
        scaleFacBuff = new ComputeBuffer(numPtcls, 4);
        adjListsBuff = new ComputeBuffer(numPtcls, 4 * (20 + 20 + 3 * 20 + 1));
    }

    void BindBuffers(int kerID)
    {
        EulerShader.SetBuffer(kerID, "pos", posBuff);
        EulerShader.SetBuffer(kerID, "newPos", newPosBuff);
        EulerShader.SetBuffer(kerID, "vel", velBuff);
        EulerShader.SetBuffer(kerID, "scaleFactors", scaleFacBuff);
        EulerShader.SetBuffer(kerID, "adjLists", adjListsBuff);
    }

    void ReleaseBuffers()
    {
        Vector3[] temp = new Vector3[numPtcls];

        posBuff.GetData(temp);
        pos.Clear();
        pos.AddRange(temp);
        posBuff.Release();

        newPosBuff.GetData(temp);
        newPos.Clear();
        newPos.AddRange(temp);
        newPosBuff.Release();

        velBuff.GetData(temp);
        vel.Clear();
        vel.AddRange(temp);
        velBuff.Release();

        scaleFacBuff.Release();
        adjListsBuff.Release();
    }

    private void OnApplicationPause(bool pause)
    {
        ReleaseBuffers();
    }

    private void OnApplicationQuit()
    {
        ReleaseBuffers();
    }
}
