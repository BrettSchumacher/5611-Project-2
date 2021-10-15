using System.Collections;
using System.Collections.Generic;
using UnityEngine;



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
    public GameObject sphere;
    public GameObject spring;
    public ComputeShader ClothSimShader;
    public Movement move;

    private bool paused = false;
    //private GameObject[] spheres;
    //private Dictionary<GameObject, (int, int)> springToInd;
    //private Dictionary<(int, int), GameObject> indToSpring;
    private Transform mainCam;
    private GameObject selectedSpring;
    private GameObject obstacle;
    
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


    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main.transform;
        obstacle = move.obstacle;

        numNodes = rows * cols;
        oldPos = new Vector3[numNodes];
        pos = new Vector3[numNodes];
        oldVel = new Vector3[numNodes];
        vel = new Vector3[numNodes];
        neighbors = new int[numNodes];

        InitializeLists();

        //spheres = new GameObject[numNodes];
        //springToInd = new Dictionary<GameObject, (int, int)>();
        //indToSpring = new Dictionary<(int, int), GameObject>();

        ClothSimShader.SetFloat("l0", l0);
        ClothSimShader.SetFloat("k_spring", k_spring / mass);
        ClothSimShader.SetFloat("k_sdrag", k_sdrag / mass);
        ClothSimShader.SetFloat("k_adrag", k_adrag / mass);
        ClothSimShader.SetFloat("grav", grav);
        ClothSimShader.SetInt("cols", cols);
        ClothSimShader.SetFloat("obstRadius", obstacle.GetComponent<SphereCollider>().radius);
        ClothSimShader.SetVector("obstVel", move.getObstVel());
        ClothSimShader.SetFloat("nodeRadius", nodeRadius);
        ClothSimShader.SetFloat("absorbtion", absorbtion);
        ClothSimShader.SetFloat("tearThresh", tearThresh);
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
            //print(ind + ": " + right(neighborVal) + ", " + down_right(neighborVal) + ", " + down(neighborVal) + ", " + down_left(neighborVal));
            /*
            if (right(neighborVal))
            {
                tempSpring = Instantiate(spring);
                springToInd[tempSpring] = (ind, ind + 1);
                indToSpring[(ind, ind + 1)] = tempSpring;
            }
            if (down_right(neighborVal))
            {
                tempSpring = Instantiate(spring);
                springToInd[tempSpring] = (ind, ind + cols + 1);
                indToSpring[(ind, ind + cols + 1)] = tempSpring;
            }
            if (down(neighborVal))
            {
                tempSpring = Instantiate(spring);
                springToInd[tempSpring] = (ind, ind + cols);
                indToSpring[(ind, ind + cols)] = tempSpring;
            }
            if (down_left(neighborVal))
            {
                tempSpring = Instantiate(spring);
                springToInd[tempSpring] = (ind, ind + cols - 1);
                indToSpring[(ind, ind + cols - 1)] = tempSpring;
            }
            spheres[ind] = Instantiate(sphere);
            spheres[ind].transform.parent = transform;
            */
        }
    }

    void Update()
    {
        HandleInputs();

        move.UpdateMove(paused, Time.deltaTime);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (paused)
        {
            return;
        }

        Vector3 wind = Vector3.zero;

        if (Input.GetMouseButton(0))
        {
            wind = mainCam.forward * windSpeed;
            wind += Vector3.one * windNoise * (2f * Mathf.PerlinNoise(3f * Time.time, Time.time) - 1f);
        }


        ClothSimShader.SetVector("wind", wind);

        float dt = Time.fixedDeltaTime / passes;

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

        for (int i = 0; i < passes; i++)
        {
            //nodesUpdate(Time.deltaTime / runs);
            ClothSimShader.SetVector("obstPos", obstacle.transform.position + move.getObstVel() * i * dt);
            ClothSimShader.Dispatch(0, Mathf.CeilToInt(rows / 8.0f), Mathf.CeilToInt(cols / 8.0f), 1);
        }

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

        Ray look = new Ray(mainCam.position, mainCam.forward);
        RaycastHit hit;
        if (Physics.Raycast(look, out hit) && hit.collider.gameObject.tag == "Spring")
        {
            if (selectedSpring != null)
            {
                selectedSpring.GetComponent<MeshRenderer>().material.color = Color.white;
            }
            selectedSpring = hit.collider.gameObject;
            selectedSpring.GetComponent<MeshRenderer>().material.color = Color.red;
        }
        else
        {
            if (selectedSpring != null)
            {
                selectedSpring.GetComponent<MeshRenderer>().material.color = Color.white;
            }
            selectedSpring = null;
        }

        /*
        int ind1;
        int ind2;
        //delete spring
        if (Input.GetMouseButton(1) && selectedSpring != null)
        {
            ind1 = springToInd[selectedSpring].Item1;
            ind2 = springToInd[selectedSpring].Item2;
            //first check if right or down-right
            if (col(ind1) < col(ind2))
            {
                if (row(ind1) == row(ind2)) //same row means it's the right connection
                {
                    neighbors[ind1] &= ~(1); //set right bit to be 0
                    neighbors[ind2] &= ~(1 << 4); 
                }
                else
                {
                    neighbors[ind1] &= ~(1 << 1); //set down right bit to 0
                    neighbors[ind2] &= ~(1 << 5);
                }
            }
            else //else it's down/down-left
            {
                if (col(ind1) == col(ind2)) //down case
                {
                    neighbors[ind1] &= ~(1 << 2);
                    neighbors[ind2] &= ~(1 << 6);
                }
                else
                {
                    neighbors[ind1] &= ~(1 << 3);
                    neighbors[ind2] &= ~(1 << 7);
                }
            }
            indToSpring.Remove((ind1, ind2));
            springToInd.Remove(selectedSpring);
            Destroy(selectedSpring);
            selectedSpring = null;
        }
        */
    }

    void HandleInputs()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            paused = !paused;
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            InitializeLists();
        }
    }

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

        norms.Clear();
        verts.Clear();
        uvs.Clear();
        tris.Clear();

        int neighborVal;

        verts.AddRange(pos);
        verts.AddRange(pos);

        Vector3 norm1, norm2 , norm3, norm4;

        //final pass, compute triangles
        for (int i = 0; i < numNodes; i++)
        {
            neighborVal = neighbors[i];
            norm1 = norm2 = norm3 = norm4 =Vector3.zero;

            //draw lower right tri if there are springs there
            //for all triangles we draw 2 that are exactly the same except
            //with opposite normals since unity will cull the opposite side
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

        for (int i = 0; i < numNodes; i++)
        {
            norms.Add(-norms[i]);
            uvs.Add(UV(i));
        }

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



    /*
    void DrawWireMesh()
    {
        int ind;
        int neighbor_val;
        GameObject tempSpring;
        Vector3 dist;

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                ind = index(i, j);
                neighbor_val = neighbors[ind];
                spheres[ind].transform.position = pos[ind];
                if (right(neighbor_val))
                {
                    dist = pos[ind + 1] - pos[ind];
                    tempSpring = indToSpring[(ind, ind + 1)];
                    tempSpring.transform.position = pos[ind] + 0.5f * dist;
                    tempSpring.transform.LookAt(pos[ind + 1], Vector3.up);
                    tempSpring.transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                }
                if (down_right(neighbor_val))
                {
                    dist = pos[ind + cols + 1] - pos[ind];
                    tempSpring = indToSpring[(ind, ind + cols + 1)];
                    tempSpring.transform.position = pos[ind] + 0.5f * dist;
                    tempSpring.transform.LookAt(pos[ind + cols + 1], Vector3.up);
                    tempSpring.transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                }
                if (down(neighbor_val))
                {
                    dist = pos[ind + cols] - pos[ind];
                    tempSpring = indToSpring[(ind, ind + cols)];
                    tempSpring.transform.position = pos[ind] + 0.5f * dist;
                    tempSpring.transform.LookAt(pos[ind + cols], Vector3.up);
                    tempSpring.transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                }
                if (down_left(neighbor_val))
                {
                    dist = pos[ind + cols - 1] - pos[ind];
                    tempSpring = indToSpring[(ind, ind + cols - 1)];
                    tempSpring.transform.position = pos[ind] + 0.5f * dist;
                    tempSpring.transform.LookAt(pos[ind + cols - 1], Vector3.up);
                    tempSpring.transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                }
            }
        }
    }

    /*
    void nodesUpdate(float dt)
    {
        //run rk4
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                if (nodes[i, j].anchor == 0) RK4(ref nodes[i, j], dt);
            }
        }

        //set oldPos and draw nodes
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                nodes[i, j].oldPos = nodes[i, j].pos;
                nodes[i, j].oldVel = nodes[i, j].vel;
            }
        }
    }

    void RK4(ref Node node, float dt)
    {
        Vector3 newVel;
        Vector3 k1 = GetAcc(ref node, node.pos, node.vel);
        newVel = node.vel + k1 * dt / 2.0f;
        Vector3 k2 = GetAcc(ref node, node.pos + newVel * dt / 2.0f, newVel);
        newVel = node.vel + k2 * dt / 2.0f;
        Vector3 k3 = GetAcc(ref node, node.pos + newVel * dt / 2.0f, newVel);
        newVel = node.vel + k3 * dt;
        Vector3 k4 = GetAcc(ref node, node.pos + newVel * dt, newVel);
        node.vel += dt / 6.0f * (k1 + 2.0f * k2 + 2.0f * k3 + k4);
        node.pos += dt * node.vel;
    }

    Vector3 GetAcc(ref Node node, Vector3 newPos, Vector3 newVel)
    {
        Vector3 acc = Vector3.zero;
        Vector3 l;
        List<Node> neighbors = new List<Node>();
        if (node.up != 0) neighbors.Add(nodes[node.row, node.col - 1]);
        if (node.right != 0) neighbors.Add(nodes[node.row + 1, node.col]);
        if (node.down != 0) neighbors.Add(nodes[node.row, node.col + 1]);
        if (node.left != 0) neighbors.Add(nodes[node.row - 1, node.col]);

        foreach (Node other in neighbors)
        {
            l = newPos - other.oldPos;
            acc -= k_spring * (l - l0 * l.normalized);
            acc -= k_sdrag * (newVel - other.oldVel).magnitude * newVel.normalized;
            acc -= k_adrag * newVel;
            acc -= Vector3.up * grav;
        }

        return acc;
    }

    private void OnDrawGizmos()
    {
        if (indToSpring == null)
        {
            return;
        }
        int ind;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                ind = index(i, j);
                //print("(" + i + ", " + j + "): right -> " + temp.right + ", down -> " + temp.down);
                if (temp.right > (byte) 0)
                {
                    Gizmos.DrawLine(temp.pos, nodes[temp.row, temp.col + 1].pos);
                }
                if (temp.down > (byte) 0)
                {
                    Gizmos.DrawLine(temp.pos, nodes[temp.row + 1, temp.col].pos);
                }
            }
        }
    }
    */
}
