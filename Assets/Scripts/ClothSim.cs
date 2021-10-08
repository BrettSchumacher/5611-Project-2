using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct Node
{
    public Vector3 oldPos;
    public Vector3 oldVel;
    public Vector3 pos;
    public Vector3 vel;
    public int row;
    public int col;
    public int up;
    public int right;
    public int down;
    public int left;
    public int anchor;
}

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
    public float wind = 1.0f;
    public float mass = 1.0f;
    public float absorbtion = 0.5f;
    public GameObject sphere;
    public GameObject spring;
    public ComputeShader ClothSimShader;
    public Movement move;

    private Node[,] nodes;
    private GameObject[,] spheres;
    private List<GameObject> springs;
    private Dictionary<GameObject, (Node, Node)> springMap;
    private Transform mainCam;
    private GameObject selectedSpring;
    private GameObject obstacle;

    // Start is called before the first frame update
    void Start()
    {
        mainCam = Camera.main.transform;
        obstacle = move.obstacle;

        nodes = new Node[rows, cols];
        spheres = new GameObject[rows, cols];
        springs = new List<GameObject>();
        Node temp;

        //generate nodes
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                temp = new Node();
                temp.pos = upperLeft + new Vector3(j * start_offset, -i * start_offset, Random.Range(-z_noise, z_noise));
                temp.vel = Vector3.zero;
                temp.oldPos = temp.pos;
                temp.oldVel = temp.vel;
                temp.anchor = 0;
                temp.row = i;
                temp.col = j;
                temp.up = i > 0 ? 1 : 0;
                if (j < cols - 1)
                {
                    temp.right = 1;
                    springs.Add(Instantiate(spring));   
                }
                else
                {
                    temp.right = 0;
                }
                if (i < rows - 1)
                {
                    temp.down = 1;
                    springs.Add(Instantiate(spring));
                }
                else
                {
                    temp.down = 0;
                }
                temp.left = j > 0 ? 1 : 0;
                nodes[i, j] = temp;
                spheres[i, j] = Instantiate(sphere);
                spheres[i, j].transform.parent = transform;
            }
        }

        //make top corners fixed
        nodes[0, 0].anchor = 1;
        nodes[0, cols - 1].anchor = 1;

        //for (int i = 0; i < cols; i += 5)
        //{
        //    nodes[0, i].anchor = 1;
        //}

        ClothSimShader.SetFloat("l0", l0);
        ClothSimShader.SetFloat("k_spring", k_spring / mass);
        ClothSimShader.SetFloat("k_sdrag", k_sdrag / mass);
        ClothSimShader.SetFloat("k_adrag", k_adrag / mass);
        ClothSimShader.SetFloat("grav", grav);
        ClothSimShader.SetInt("cols", cols);
        ClothSimShader.SetFloat("obstRadius", obstacle.GetComponent<SphereCollider>().radius);
        ClothSimShader.SetVector("obstVel", move.getObstVel());
        ClothSimShader.SetFloat("absorbtion", absorbtion);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (Input.GetMouseButton(0))
        {
            ClothSimShader.SetFloat("wind", wind / mass);
        }
        else
        {
            ClothSimShader.SetFloat("wind", 0f);
        }

        float dt = Time.fixedDeltaTime / passes;

        printNodes();
        ClothSimShader.SetFloat("dt", dt);
        ComputeBuffer nodeBuffer = new ComputeBuffer(rows * cols, 19 * 4);
        nodeBuffer.SetData(nodes);
        ClothSimShader.SetBuffer(0, "nodes", nodeBuffer);

        for (int i = 0; i < passes; i++)
        {
            //nodesUpdate(Time.deltaTime / runs);
            ClothSimShader.SetVector("obstPos", obstacle.transform.position + move.getObstVel() * i * dt);
            ClothSimShader.Dispatch(0, Mathf.CeilToInt(rows / 8.0f), Mathf.CeilToInt(cols / 8.0f), 1);
        }

        nodeBuffer.GetData(nodes);
        nodeBuffer.Release();

        printNodes();
        int num = 0;
        Vector3 dist;
        springMap = new Dictionary<GameObject, (Node, Node)>();

        //draw nodes and springs
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                spheres[i, j].transform.position = nodes[i, j].pos;
                if (nodes[i, j].right > 0)
                {
                    dist = nodes[i, j + 1].pos - nodes[i, j].pos;
                    springs[num].transform.position = nodes[i, j].pos + 0.5f * dist;
                    springs[num].transform.LookAt(nodes[i, j + 1].pos, Vector3.up);
                    springs[num].transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                    springMap.Add(springs[num], (nodes[i, j], nodes[i, j + 1]));
                    num++;
                }
                if (nodes[i, j].down > 0)
                {
                    dist = nodes[i + 1, j].pos - nodes[i, j].pos;
                    springs[num].transform.position = nodes[i, j].pos + 0.5f * dist;
                    springs[num].transform.LookAt(nodes[i + 1, j].pos, Vector3.up);
                    springs[num].transform.localScale = new Vector3(0.001f, 0.001f, dist.magnitude);
                    springMap.Add(springs[num], (nodes[i, j], nodes[i + 1, j]));
                    num++;
                }
            }
        }

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

        Node node1;
        Node node2;
        //delete spring
        if (Input.GetMouseButton(1) && selectedSpring != null)
        {
            node1 = springMap[selectedSpring].Item1;
            node2 = springMap[selectedSpring].Item2;
            //break right/left connection
            if (node2.col > node1.col)
            {
                nodes[node1.row, node1.col].right = 0;
                nodes[node2.row, node2.col].left = 0;
            }
            else
            {
                nodes[node1.row, node1.col].down = 0;
                nodes[node2.row, node2.col].up = 0;
            }
            springs.Remove(selectedSpring);
            Destroy(selectedSpring);
            selectedSpring = null;
        }
    }

    void printNodes()
    {
        string result = "\nNodes: ";
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                result += nodes[i, j].pos + ", ";
            }
        }
        //print(result);
    }

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
        if (nodes == null)
        {
            return;
        }
        Node temp;
        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < cols; j++)
            {
                temp = nodes[i, j];
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
}
