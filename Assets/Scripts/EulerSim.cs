using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EulerSim : MonoBehaviour
{
    public ComputeShader EulerShader;
    public uint passes = 1;
    public uint rows = 10;
    public uint cols = 10;
    public float grav = 1f;
    public float drag = 0.2f;
    public float spacing = 0.1f;
    public float avg_height = 1.5f;
    public float slope = 0.2f;
    public Vector3 bottomLeft;
    public GameObject waterPtcl;

    private int numPtcls;
    private int threadGroupsX;
    private float[] h;
    private float[] hu;
    private float[] hv;
    private GameObject[] spheres;

    //all thems buffers
    private ComputeBuffer h_buff;
    private ComputeBuffer hu_buff;
    private ComputeBuffer hv_buff;

    // Start is called before the first frame update 
    void Start()
    {
        GenerateFluid();
        SetShaderParams();
    }

    uint ID(uint row, uint col)
    {
        return row * cols + col;
    }

    uint row(uint ind)
    {
        return ind / cols;
    }

    uint col(uint ind)
    {
        return ind % cols;
    }

    Vector3 CellPos(uint ind)
    {
        return new Vector3(bottomLeft.x + col(ind) * spacing, bottomLeft.y + h[ind], bottomLeft.z + row(ind) * spacing);
    }

    void GenerateFluid()
    {
        numPtcls = ((int) rows) * ((int) cols);
        print(numPtcls);

        spheres = new GameObject[numPtcls];

        //allocate 3x space necessary to store mid and new values, necessary since can only pass 8 buffers to gpu at a time
        h = new float[3 * numPtcls];
        hu = new float[3 * numPtcls];
        hv = new float[3 * numPtcls];

        GameObject tempPtcl;

        for (uint i = 0; i < numPtcls; i++)
        {
            h[i] = avg_height + col(i) * slope / cols + row(i) * slope / rows;
            h[i + numPtcls] = 0f;
            h[i + 2 * numPtcls] = 0f;
            hu[i] = 0f;
            hu[i + numPtcls] = 0f;
            hu[i + 2 * numPtcls] = 0f;
            hv[i] = 0f;
            hv[i + numPtcls] = 0f;
            hv[i + 2 * numPtcls] = 0f;
            tempPtcl = Instantiate(waterPtcl);
            tempPtcl.transform.position = CellPos(i);
            spheres[i] = tempPtcl;
        }
    }

    void SetShaderParams()
    {
        EulerShader.SetFloat("grav", grav);
        EulerShader.SetFloat("drag", drag);
        EulerShader.SetFloat("dx", spacing);
        EulerShader.SetInt("rows", (int) rows);
        EulerShader.SetInt("cols", (int) cols);
        EulerShader.SetInt("numPtcls", numPtcls);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        threadGroupsX = Mathf.CeilToInt(numPtcls / 64.0f);
        float dt = Time.fixedDeltaTime / passes;
        EulerShader.SetFloat("dt", dt);

        //PrintArray(h);
        SetBuffers();

        float timer = Time.time;
        uint i = 0;
        for (; i < passes; i++)
        {
            SinglePass(dt);
        }
        //print("GPU time after " + i + " passes: " + (Time.time - timer).ToString("E") + " seconds");

        ReleaseBuffers();
        //PrintArray(h);

        for (i = 0; i < numPtcls; i++)
        {
            spheres[i].transform.position = CellPos(i);
        }
    }
    
    void PrintPre()
    {
        print("Pre print:");
        for (int i = 0; i < numPtcls; i++)
        {
            print("  " + i + " pre-height: " + h[i]);
            print("  " + i + " pre-hu: " + hu[i]);
            print("  " + i + " pre-hv: " + hv[i]);
        }
    }

    void PrintPost()
    {
        print("Post print:");
        for (int i = 0; i < numPtcls; i++)
        {
            print("  " + i + " post-height: " + h[i]);
            print("  " + i + " post-hu: " + hu[i]);
            print("  " + i + " post-hv: " + hv[i]);
        }
    }

    void PrintArray(float[] arr)
    {
        string res = "Main: ";
        for (int i = 0; i < arr.Length / 3; i++)
        {
            res += arr[i].ToString("F2") + " ";
        }

        res += "  Mid: ";
        for (int i = arr.Length / 3; i < 2 * arr.Length / 3; i++)
        {
            res += arr[i].ToString("F2") + " ";
        }

        res += "  New: ";
        for (int i = 2 * arr.Length / 3; i < arr.Length; i++)
        {
            res += arr[i].ToString("F2") + " ";
        }

        print(res);
    }

    void SinglePass(float dt)
    {
        //calculate mid values
        BindBuffers(0);
        EulerShader.Dispatch(0, threadGroupsX, 1, 1);

        //calculate new values from mid
        BindBuffers(1);
        EulerShader.Dispatch(1, threadGroupsX, 1, 1);

        //set new values
        BindBuffers(2);
        EulerShader.Dispatch(2, threadGroupsX, 1, 1);
    }

    void SetBuffers()
    {
        h_buff = new ComputeBuffer(3 * numPtcls, 4);
        h_buff.SetData(h);

        hu_buff = new ComputeBuffer(3 * numPtcls, 4);
        hu_buff.SetData(hu);

        hv_buff = new ComputeBuffer(3 * numPtcls, 4);
        hv_buff.SetData(hv);
    }

    void BindBuffers(int kerID)
    {
        EulerShader.SetBuffer(kerID, "h", h_buff);
        EulerShader.SetBuffer(kerID, "hu", hu_buff);
        EulerShader.SetBuffer(kerID, "hv", hv_buff);
    }

    void ReleaseBuffers()
    {
        if (h_buff == null || h == null) 
        {
            print("None buffers left beef");
            return;
        }

        h_buff.GetData(h);
        hu_buff.GetData(hu);
        hv_buff.GetData(hv);

        h_buff.Release();
        hu_buff.Release();
        hv_buff.Release();
    }

    //avoid gpu memory leaks
    private void OnApplicationPause(bool pause)
    {
        ReleaseBuffers();
    }

    private void OnApplicationQuit()
    {
        ReleaseBuffers();
    }
}
