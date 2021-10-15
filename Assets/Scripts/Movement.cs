using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public float moveSpeed = 1f;
    public float sensitivity = 100.0f;
    public GameObject obstacle;

    private Vector3 prev_mouse = Vector3.zero;
    private float rotY = 0f;
    private GameObject moveTarget;
    private GameObject lookTarget = null;
    private Vector3 obstVel;

    // Start is called before the first frame update
    void Start()
    {
        //Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
        moveTarget = gameObject;
    }

    // Update is called once per frame
    public void UpdateMove(bool paused, float dt)
    {
        if (paused && moveTarget != gameObject)
        {
            moveTarget = gameObject;
        }

        HandleMovement(moveTarget, dt);
        HandleLook();

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
            Debug.Break();
        }

        Ray look = new Ray(transform.position, transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(look, out hit) && hit.collider.gameObject == obstacle)
        {
            if (moveTarget != obstacle)
            {
                lookTarget = obstacle;
                obstacle.GetComponent<MeshRenderer>().material.color = Color.black;
            }
            else
            {
                lookTarget = null;
                obstacle.GetComponent<MeshRenderer>().material.color = Color.white;
            }
        }
        else
        {
            lookTarget = null;
            if (moveTarget == obstacle)
            {
                obstacle.GetComponent<MeshRenderer>().material.color = Color.white;
            }
            else
            {
                obstacle.GetComponent<MeshRenderer>().material.color = Color.black;
            }
        }

        if (Input.GetMouseButtonUp(1))
        {
            print("click");
            if (moveTarget != gameObject)
            {
                print("deselect");
                moveTarget = gameObject;
            }
            else if (lookTarget != null)
            {
                moveTarget = obstacle;
                print("select");
            }
        }
    }

    void HandleMovement(GameObject target, float dt)
    {
        Vector3 vel = Vector3.zero;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (Input.GetKey(KeyCode.W))
        {
            vel += forward;
        }
        if (Input.GetKey(KeyCode.A))
        {
            vel -= right;
        }
        if (Input.GetKey(KeyCode.S))
        {
            vel -= forward;
        }
        if (Input.GetKey(KeyCode.D))
        {
            vel += right;
        }

        vel.Normalize();

        vel *= moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            vel -= Vector3.up;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            vel += Vector3.up;
        }

        if (moveTarget == obstacle)
        {
            obstVel = vel;
        }
        else
        {
            obstVel = Vector3.zero;
        }

        moveTarget.transform.position += vel * dt;
    }

    void HandleLook()
    {
        if (prev_mouse.sqrMagnitude < 0.01f)
        {
            prev_mouse = Input.mousePosition;
        }
        Vector3 mouseDelta = Input.mousePosition - prev_mouse;
        rotY -= mouseDelta.y * sensitivity;
        rotY = Mathf.Clamp(rotY, -90f, 90f);
        float rotX = transform.eulerAngles.y + mouseDelta.x * sensitivity;
        transform.eulerAngles = new Vector3(rotY, rotX, 0f);
        prev_mouse = Input.mousePosition;
    }

    public Vector3 getObstVel()
    {
        return obstVel;
    }
}
