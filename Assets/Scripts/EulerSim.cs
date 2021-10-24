using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class EulerSim : MonoBehaviour
{
    public ComputeShader EulerShader;
    public uint passes = 1;
    public uint rows = 10;
    public uint cols = 10;
    public float grav = 1f;
    public float drag = 0.2f;
    public float density = 1f;
    public float visc = 1f;
    public float spacing = 0.1f;
    public float avg_height = 1.5f;
    public float max_height = 5f;
    public float slope = 0.2f;
    public float rainDrops = 15f;
    public float rainPower = 0.1f;
    public Vector3 bottomLeft;
    public GameObject waterPtcl;
    public TextMeshProUGUI pauseText;
    public TextMeshProUGUI rainText;
    public TextMeshProUGUI fpsText;

    private int numPtcls;
    private int threadGroupsX;
    private float[] h;
    private float[] hu;
    private float[] hv;
    //private GameObject[] spheres;

    //all thems buffers
    private ComputeBuffer h_buff;
    private ComputeBuffer hu_buff;
    private ComputeBuffer hv_buff;

    private MeshFilter meshFilter;
    private Mesh waterMesh;
    private Vector3[] verts;
    private Vector3[] norms;
    private int[] tris;

    private bool paused = true;
    private float fpsTimer = 0f;
    private int fpsCounter = 0;

    // Start is called before the first frame update 
    void Start()
    {
        meshFilter = GetComponent<MeshFilter>();
        numPtcls = ((int)rows) * ((int)cols);
        pauseText.enabled = paused;

        //allocate 3x space necessary to store mid and new values, necessary since can only pass 8 buffers to gpu at a time
        h = new float[3 * numPtcls];
        hu = new float[3 * numPtcls];
        hv = new float[3 * numPtcls];

        SetShaderParams();
        GenerateFluid();
        GenerateMesh();
    }

    void Restart()
    {
        GenerateFluid();
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

    Vector3 GroundPos(uint ind)
    {
        return new Vector3(bottomLeft.x + col(ind) * spacing, bottomLeft.y, bottomLeft.z + row(ind) * spacing);
    }

    void GenerateFluid()
    {
        threadGroupsX = Mathf.CeilToInt(numPtcls / 64.0f);

        for (uint i = 0; i < numPtcls; i++)
        {
            h[i] = avg_height;
            h[i + numPtcls] = 0f;
            h[i + 2 * numPtcls] = 0f;
            hu[i] = 0f;
            hu[i + numPtcls] = 0f;
            hu[i + 2 * numPtcls] = 0f;
            hv[i] = 0f;
            hv[i + numPtcls] = 0f;
            hv[i + 2 * numPtcls] = 0f;
        }
    }

    void CreateWave()
    {
        SetBuffers();
        BindBuffers(4);

        EulerShader.Dispatch(4, threadGroupsX, 1, 1);

        ReleaseBuffers();
    }

    void SetShaderParams()
    {
        EulerShader.SetFloat("grav", grav);
        EulerShader.SetFloat("drag", drag);
        EulerShader.SetFloat("density", density);
        EulerShader.SetFloat("visc", visc);
        EulerShader.SetFloat("dx", spacing);
        EulerShader.SetInt("rows", (int) rows);
        EulerShader.SetInt("cols", (int) cols);
        EulerShader.SetInt("numPtcls", numPtcls);
        EulerShader.SetVector("bottomLeft", bottomLeft);
        EulerShader.SetFloat("spacing", spacing);
        EulerShader.SetFloat("avg_height", avg_height);
        EulerShader.SetFloat("max_height", max_height);
        EulerShader.SetFloat("slope", slope);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            ReleaseBuffers(); //avoid gpu memory leak (has caused crashes before)
            SceneManager.LoadScene(0);
        }
        if (Input.GetKey(KeyCode.Escape))
        {
            ReleaseBuffers(); //avoid gpu memory leak (has caused crashes before)
            Application.Quit();
            Debug.Break();
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            paused = !paused;
            pauseText.enabled = paused;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            GenerateFluid();
        }

        if (Input.GetMouseButtonDown(0))
        {
            CreateWave();
        }

        rainDrops += Input.mouseScrollDelta.y;
        rainDrops = Mathf.Max(0f, rainDrops);

        rainText.text = "Rain Drops (per sec): " + rainDrops.ToString("F3");

        if (fpsTimer < 0.5f)
        {
            fpsTimer += Time.deltaTime;
            fpsCounter++;
        }
        else
        {
            fpsText.text = "FPS: " + (fpsCounter / fpsTimer).ToString("F0");
            fpsTimer = 0f;
            fpsCounter = 0;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (h == null || h[0] == 0)
        {
            GenerateFluid();
        }

        if (paused)
        {
            return;
        }

        threadGroupsX = Mathf.CeilToInt(numPtcls / 64.0f);
        float dt = Time.fixedDeltaTime / passes;
        EulerShader.SetFloat("dt", dt);
        AddRain(Time.fixedDeltaTime);

        //PrintArray(h);
        //PrintArray(hu);
        //PrintArray(hv);
        SetBuffers();

        for (uint i = 0; i < passes; i++)
        {
            SinglePass(dt);
        }

        ReleaseBuffers();


        UpdateMesh();
    }

    void AddRain(float dt)
    {
        int ind;
        int num = Mathf.FloorToInt(rainDrops * dt);
        num += Random.value < rainDrops * dt % 1.0f ? 1 : 0;
        for (int i = 0; i < num; i++)
        {
            ind = Mathf.RoundToInt(Random.Range(cols + 1, numPtcls - cols - 1));
            h[ind - 1 - cols] += rainPower; h[ind - cols] += rainPower; h[ind + 1 - cols] += rainPower;
            h[ind - 1] += rainPower; h[ind] += 0; h[ind + 1] += rainPower;
            h[ind - 1 + cols] += rainPower; h[ind + cols] += rainPower; h[ind + 1 + cols] += rainPower;
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
        uint j = 0;
        for (int i = 0; i < arr.Length / 3; i++)
        {
            j = (uint) i;
            if (row(j) == 0 || row(j) == rows - 1 || col(j) == 0 || col(j) == cols - 1) continue;
            res += arr[i].ToString("F2") + " ";
        }

        res += "  Mid: ";
        for (int i = arr.Length / 3; i < 2 * arr.Length / 3; i++)
        {
            j = (uint) (i - arr.Length / 3);
            if (row(j) == 0 || row(j) == rows - 1 || col(j) == 0 || col(j) == cols - 1) continue;
            res += arr[i].ToString("F2") + " ";
        }

        res += "  New: ";
        for (int i = 2 * arr.Length / 3; i < arr.Length; i++)
        {
            j = (uint)(i - 2 * arr.Length / 3);
            if (row(j) == 0 || row(j) == rows - 1 || col(j) == 0 || col(j) == cols - 1) continue;
            res += arr[i].ToString("F2") + " ";
        }

        print(res);
    }

    void SinglePass(float dt)
    {
        //update mids
        BindBuffers(0);
        EulerShader.Dispatch(0, threadGroupsX, 1, 1);

        //update new
        BindBuffers(1);
        EulerShader.Dispatch(1, threadGroupsX, 1, 1);

        //set new
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
        if (h_buff == null || !h_buff.IsValid() || h == null) 
        {
            //print("None buffers left beef");
            return;
        }

        h_buff.GetData(h);
        hu_buff.GetData(hu);
        hv_buff.GetData(hv);

        h_buff.Release();
        hu_buff.Release();
        hv_buff.Release();
    }

    void GenerateMesh()
    {
        waterMesh = new Mesh();
        waterMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        uint num = 2 * (rows - 1) * (cols - 1) + 4 * (rows - 1) + 4 * (cols - 1);
        num *= 3;
        verts = new Vector3[num];
        norms = new Vector3[num];
        tris = new int[num];

        SetVerts();

        waterMesh.vertices = verts;
        waterMesh.normals = norms;
        waterMesh.triangles = tris;
        meshFilter.mesh = waterMesh;
    }

    void UpdateMesh()
    {
        SetVerts();

        waterMesh.vertices = verts;
        waterMesh.normals = norms;
        waterMesh.triangles = tris;
    }

    void SetVerts()
    {
        //uint ind = 0;
        //int tri = 0;
        //Vector3 v1, v2, v3, norm;
        ComputeBuffer vertsBuff = new ComputeBuffer(verts.Length, 12);
        ComputeBuffer normsBuff = new ComputeBuffer(norms.Length, 12);
        ComputeBuffer trisBuff = new ComputeBuffer(tris.Length, 4);
        h_buff = new ComputeBuffer(h.Length, 4);

        vertsBuff.SetData(verts);
        normsBuff.SetData(norms);
        trisBuff.SetData(tris);
        h_buff.SetData(h);

        EulerShader.SetBuffer(3, "verts", vertsBuff);
        EulerShader.SetBuffer(3, "norms", normsBuff);
        EulerShader.SetBuffer(3, "tris", trisBuff);
        EulerShader.SetBuffer(3, "h", h_buff);

        int groups = Mathf.CeilToInt(verts.Length / 64.0f / 3.0f);

        EulerShader.Dispatch(3, groups, 1, 1);

        vertsBuff.GetData(verts);
        normsBuff.GetData(norms);
        trisBuff.GetData(tris);

        vertsBuff.Release();
        normsBuff.Release();
        trisBuff.Release();
        h_buff.Release();

        /*//upper left tris
        for (uint i = 1; i < rows; i++)
        {
            for (uint j = 1; j < cols; j++)
            {
                ind = j + i * cols;

                v1 = CellPos(ind);  v2 = CellPos(ind - 1); v3 = CellPos(ind - cols);
                verts.Add(v1); verts.Add(v3); verts.Add(v2);

                norm = Vector3.Cross(v3 - v1, v2 - v1).normalized;
                norms.Add(norm); norms.Add(norm); norms.Add(norm);

                tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            }
        }

        //lower right tris
        for (uint i = 0; i < rows - 1; i++)
        {
            for (uint j = 0; j < cols - 1; j++)
            {
                ind = j + i * cols;
                v1 = CellPos(ind); v2 = CellPos(ind + 1); v3 = CellPos(ind + cols);
                verts.Add(v1); verts.Add(v3); verts.Add(v2);

                norm = Vector3.Cross(v3 - v1, v2 - v1).normalized;
                norms.Add(norm); norms.Add(norm); norms.Add(norm);

                tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            }
        }

        //front side
        for (uint i = 0; i < cols - 1; i++)
        {
            verts.Add(CellPos(i)); verts.Add(CellPos(i + 1)); verts.Add(GroundPos(i));
            verts.Add(GroundPos(i)); verts.Add(CellPos(i + 1)); verts.Add(GroundPos(i + 1));

            norms.Add(-Vector3.forward); norms.Add(-Vector3.forward); norms.Add(-Vector3.forward);
            norms.Add(-Vector3.forward); norms.Add(-Vector3.forward); norms.Add(-Vector3.forward);

            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
        }

        //right side
        for (uint i = 0; i < rows - 1; i++)
        {
            ind = (i + 1) * cols - 1;
            verts.Add(CellPos(ind)); verts.Add(CellPos(ind + cols)); verts.Add(GroundPos(ind));
            verts.Add(GroundPos(ind)); verts.Add(CellPos(ind + cols)); verts.Add(GroundPos(ind + cols));

            norms.Add(Vector3.right); norms.Add(Vector3.right); norms.Add(Vector3.right);
            norms.Add(Vector3.right); norms.Add(Vector3.right); norms.Add(Vector3.right);

            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
        }

        //back side
        for (uint i = 0; i < cols - 1; i++)
        {
            ind = (rows - 1) * cols + i;
            verts.Add(CellPos(ind + 1)); verts.Add(CellPos(ind)); verts.Add(GroundPos(ind));
            verts.Add(CellPos(ind + 1)); verts.Add(GroundPos(ind)); verts.Add(GroundPos(ind + 1));

            norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward);
            norms.Add(Vector3.forward); norms.Add(Vector3.forward); norms.Add(Vector3.forward);

            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
        }

        //left side
        for (uint i = 0; i < cols - 1; i++)
        {
            ind = i * cols;
            verts.Add(CellPos(ind + cols)); verts.Add(CellPos(ind)); verts.Add(GroundPos(ind));
            verts.Add(CellPos(ind + cols)); verts.Add(GroundPos(ind)); verts.Add(GroundPos(ind + cols));

            norms.Add(-Vector3.right); norms.Add(-Vector3.right); norms.Add(-Vector3.right);
            norms.Add(-Vector3.right); norms.Add(-Vector3.right); norms.Add(-Vector3.right);

            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
            tris.Add(tri++); tris.Add(tri++); tris.Add(tri++);
        }*/
    }
}
