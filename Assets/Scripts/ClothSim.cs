using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;


public class ClothSim : MonoBehaviour
{
    public Vector3 upperLeft;
    [Range (1, 1000)]
    public int rows = 10;
    [Range(1, 1000)]
    public int cols = 10;
    public int passes = 10;
    public float l0 = 1f;
    public float start_offset = 0.5f;
    public float z_noise = 0.05f;
    public float k_spring = 10f;
    public float k_sdrag = 10f;
    public float k_adrag = 10f;
    public float grav = 1.0f;
    public float windSpeed = 5.0f;
    public float windNoise;
    public float mass = 1.0f;
    public float absorbtion = 0.5f;
    public float nodeRadius = 0.05f;
    public float tearThresh = 0.5f;
    public float fanSpeed = 1f;
    public GameObject sphere;
    public GameObject spring;
    public ComputeShader ClothSimShader;
    public Movement move;
    public TextMeshProUGUI pauseText;
    public TextMeshProUGUI windText;
    public TextMeshProUGUI fpsText;

    private bool paused = true;
    private Transform mainCam;
    private GameObject selectedSpring;
    private GameObject obstacle;
    private Transform fanBlades;
    private float fpsTimer = 0f;
    private int fpsCounter = 0;
    
    private Vector3[] oldPos;
    private Vector3[] pos;
    private Vector3[] oldVel;
    private Vector3[] vel;

    // the way I store neighbor information is in the bit representation of an int
    // if a node can be connected along the 4 cardinal directions as well as the diagonals
    // the first bit of the integer refers to the right connection, it is 1 if connected, 0 otherwise
    // then as you go around clockwise it goes to later bits in the integer
    // the 9th bit will also refer to whether or not the node is anchored
    private int[] neighbors;
    private int numNodes;

    private MeshFilter meshFilter;
    private Mesh clothMesh;
    private List<Vector3> verts;
    private List<Vector2> uvs;
    private List<Vector3> norms;
    private List<int> tris;

    // get index in list from row and column
    int index(int row, int col)
    {
        return row * cols + col;
    }

    int row(int ind)
    {
        return ind / cols;
    }

    int col(int ind)
    {
        return ind % cols;
    }

    int get_neighbors(int row, int col)
    {
        int result = 0;
        //check right
        if (col < cols - 1)
        {
            result |= 1;
        }
        //check down right
        if (col < cols - 1 && row < rows - 1)
        {
            result |= 1 << 1;
        }
        //check down
        if (row < rows - 1)
        {
            result |= 1 << 2;
        }
        //check down left
        if (col > 0 && row < rows - 1)
        {
            result |= 1 << 3;
        }
        //check left
        if (col > 0)
        {
            result |= 1 << 4;
        }
        //check up left
        if (col > 0 && row > 0)
        {
            result |= 1 << 5;
        }
        //check up
        if (row > 0)
        {
            result |= 1 << 6;
        }
        //check up right
        if (col < cols - 1 && row > 0)
        {
            result |= 1 << 7;
        }

        //right now only the top corners are anchored
        if (row == 0)
        {
            result |= 1 << 8;
        }

        return result;
    }

    //these functions use bit masks to see if the specified bit for that connection
    //inside neighborVal is set to a 1 or not
    bool right(int neighborVal)
    {
        return (neighborVal & 1) != 0;
    }

    bool down_right(int neighborVal)
    {
        return (neighborVal & (1 << 1)) != 0;
    }

    bool down(int neighborVal)
    {
        return (neighborVal & (1 << 2)) != 0;
    }

    bool down_left(int neighborVal)
    {
        return (neighborVal & (1 << 3)) != 0;
    }

    bool left(int neighborVal)
    {
        return (neighborVal & (1 << 4)) != 0;
    }

    bool up(int neighborVal)
    {
        return (neighborVal & (1 << 6)) != 0;
    }

    void Start()
    {
        mainCam = Camera.main.transform;
        obstacle = move.GetObstacle();
        fanBlades = GameObject.FindWithTag("FanBlades").transform;
        pauseText.enabled = paused;

        numNodes = rows * cols;
        oldPos = new Vector3[numNodes];
        pos = new Vector3[numNodes];
        oldVel = new Vector3[numNodes];
        vel = new Vector3[numNodes];
        neighbors = new int[numNodes];

        InitializeLists();

        ClothSimShader.SetFloat("l0", l0);
        ClothSimShader.SetFloat("k_spring", k_spring / mass);
        ClothSimShader.SetFloat("k_sdrag", k_sdrag / mass);
        ClothSimShader.SetFloat("k_adrag", k_adrag / mass);
        ClothSimShader.SetFloat("grav", grav);
        ClothSimShader.SetInt("cols", cols);
        ClothSimShader.SetFloat("obstRadius", obstacle.GetComponent<SphereCollider>().radius * obstacle.transform.lossyScale.x);
        ClothSimShader.SetVector("obstVel", move.getObstVel());
        ClothSimShader.SetFloat("nodeRadius", nodeRadius);
        ClothSimShader.SetFloat("absorbtion", absorbtion);
        ClothSimShader.SetFloat("tearThresh", tearThresh);

        RenderMesh();
    }

    void InitializeLists()
    {
        int neighborVal;
        int i, j;

        //generate nodes
        for (int ind = 0; ind < numNodes; ind++)
        {
            i = row(ind);
            j = col(ind);
            pos[ind] = upperLeft + new Vector3(j * start_offset, -i * start_offset, Random.Range(-z_noise, z_noise));
            vel[ind] = Vector3.zero;
            oldPos[ind] = pos[ind];
            oldVel[ind] = vel[ind];
            neighborVal = get_neighbors(i, j);
            neighbors[ind] = neighborVal;
        }
    }

    void Update()
    {
        // only inputs and movement handled on update, simulation updated on fixed update
        HandleInputs(Time.deltaTime);

        move.UpdateMove(paused, Time.deltaTime);
    }

    void FixedUpdate()
    {
        if (paused)
        {
            return;
        }

        Vector3 wind = Vector3.zero;

        //if player is left-clicking, set the wind speed and spin the fan blades
        if (Input.GetMouseButton(0))
        {
            wind = mainCam.forward * windSpeed;
            wind += Vector3.one * windNoise * (2f * Mathf.PerlinNoise(3f * Time.time, Time.time) - 1f);

            fanBlades.localEulerAngles += new Vector3(0f, fanSpeed * Time.fixedDeltaTime * windSpeed / 50f);
        }

        ClothSimShader.SetVector("wind", wind);

        float dt = Time.fixedDeltaTime / passes;

        //deal with buffers to send to GPU
        printNodes();
        ClothSimShader.SetFloat("dt", dt);
        ComputeBuffer oldPosBuffer = new ComputeBuffer(numNodes, 4 * 3);
        ComputeBuffer posBuffer = new ComputeBuffer(numNodes, 4 * 3);
        ComputeBuffer oldVelBuffer = new ComputeBuffer(numNodes, 4 * 3);
        ComputeBuffer velBuffer = new ComputeBuffer(numNodes, 4 * 3);
        ComputeBuffer neighborsBuffer = new ComputeBuffer(numNodes, 4);
        oldPosBuffer.SetData(oldPos);
        posBuffer.SetData(pos);
        oldVelBuffer.SetData(oldVel);
        velBuffer.SetData(vel);
        neighborsBuffer.SetData(neighbors);
        ClothSimShader.SetBuffer(0, "oldPos", oldPosBuffer);
        ClothSimShader.SetBuffer(0, "pos", posBuffer);
        ClothSimShader.SetBuffer(0, "oldVel", oldVelBuffer);
        ClothSimShader.SetBuffer(0, "vel", velBuffer);
        ClothSimShader.SetBuffer(0, "neighbors", neighborsBuffer);

        //run the simulation
        for (int i = 0; i < passes; i++)
        {
            //nodesUpdate(Time.deltaTime / runs);;
            ClothSimShader.SetVector("obstPos", obstacle.transform.position + move.getObstVel() * i * dt);
            ClothSimShader.Dispatch(0, Mathf.CeilToInt(rows / 8.0f), Mathf.CeilToInt(cols / 8.0f), 1);
        }

        //collect data from the GPU
        oldPosBuffer.GetData(oldPos);
        posBuffer.GetData(pos);
        oldVelBuffer.GetData(oldVel);
        velBuffer.GetData(vel);
        neighborsBuffer.GetData(neighbors);

        oldPosBuffer.Release();
        posBuffer.Release();
        oldVelBuffer.Release();
        velBuffer.Release();
        neighborsBuffer.Release();

        RenderMesh();
    }

    void HandleInputs(float dt)
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
            Debug.Break();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SceneManager.LoadScene(1);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            paused = !paused;
            pauseText.enabled = paused;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            InitializeLists();
        }

        windSpeed += Input.mouseScrollDelta.y;
        windSpeed = Mathf.Max(0f, windSpeed);
        windText.text = "Wind Speed: " + (windSpeed / 10f).ToString("F2");

        if (fpsTimer < 0.5f)
        {
            fpsTimer += dt;
            fpsCounter++;
        }
        else
        {
            fpsText.text = "FPS: " + (fpsCounter / fpsTimer).ToString("F0");
            fpsTimer = 0f;
            fpsCounter = 0;
        }
    }

    //debug function for printing node positions
    void printNodes()
    {
        string result = "\nNodes: ";
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result += pos[index(i, j)] + ", ";
            }
        }
        //print(result);
    }

    void RenderMesh()
    {
        if (clothMesh == null)
        {
            clothMesh = new Mesh();
            meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = clothMesh;
            verts = new List<Vector3>();
            uvs = new List<Vector2>();
            norms = new List<Vector3>();
            tris = new List<int>();
        }

        //clear mesh data
        norms.Clear();
        verts.Clear();
        uvs.Clear();
        tris.Clear();

        int neighborVal;

        //add positions to verts
        verts.AddRange(pos);

        Vector3 norm1, norm2 , norm3, norm4;

        //go through and make triangles, normals are calculated as the average of the normals
        //around that vertex, resulting in smooth shading for the cloth
        for (int i = 0; i < numNodes; i++)
        {
            neighborVal = neighbors[i];
            norm1 = norm2 = norm3 = norm4 = Vector3.zero;

            //draw lower right tri if there are springs there
            //for all triangles we draw 2 that are exactly the same except
            //with opposite normals so that it renders on both sides
            if (right(neighborVal) && down(neighborVal))
            {
                tris.Add(i); tris.Add(i + 1); tris.Add(i + cols);
                tris.Add(i + numNodes); tris.Add(i + cols + numNodes); tris.Add(i + 1 + numNodes);
                norm1 = Vector3.Cross(pos[i + cols] - pos[i], pos[i + 1] - pos[i]).normalized;
            }

            //draw upper left tri if there's springs
            if (up(neighborVal) && left(neighborVal))
            {
                tris.Add(i); tris.Add(i - 1); tris.Add(i - cols);
                tris.Add(i + numNodes); tris.Add(i - cols + numNodes); tris.Add(i - 1 + numNodes);
                norm2 = Vector3.Cross(pos[i - cols] - pos[i], pos[i - 1] - pos[i]).normalized;
            }

            //go through other 2 triangles just for normals
            if (right(neighborVal) && up(neighborVal))
            {
                norm3 = Vector3.Cross(pos[i + 1] - pos[i], pos[i - cols] - pos[i]).normalized;
            }

            if (left(neighborVal) && down(neighborVal))
            {
                norm3 = Vector3.Cross(pos[i - 1] - pos[i], pos[i + cols] - pos[i]).normalized;
            }

            norms.Add(-(norm1 + norm2 + norm3 + norm4).normalized);
            uvs.Add(UV(i));
        }

        //add pos again to represent the back side of the mesh, and copy over normals (but negative) and UVs
        verts.AddRange(pos);
        for (int i = 0; i < numNodes; i++)
        {
            norms.Add(-norms[i]);
            uvs.Add(UV(i));
        }


        //apply data to mesh
        clothMesh.Clear();
        clothMesh.vertices = verts.ToArray();
        clothMesh.normals = norms.ToArray();
        clothMesh.uv = uvs.ToArray();
        clothMesh.triangles = tris.ToArray();
    }

    Vector2 UV(int ind)
    {
        Vector2 uv = new Vector2();
        uv.x = col(ind) / (rows - 1f);
        uv.y = -row(ind) / (cols - 1f);
        return uv;
    }
}
