using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Movement : MonoBehaviour
{
    public float moveSpeed = 1f;
    public float sensitivity = 100.0f;
    public float obstSpeed = 1f;

    private GameObject obstacle;

    private float rotY = 0f;
    private Vector3 obstVel;
    private Vector3 camVel;
    private float t_obst;
    private Animation anim;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;
        obstacle = GameObject.FindWithTag("Obstacle");
        anim = GetComponentInChildren<Animation>();
        anim.Play();
        anim["Scene"].speed = 0f;
        t_obst = 0f;
    }

    // Update is called once per frame
    public void UpdateMove(bool paused, float dt)
    {
        HandleMovement(dt);
        HandleObstacle(dt);
        HandleLook();
    }

    void HandleMovement(float dt)
    {
        //set camera velocity
        Vector3 camVel = Vector3.zero;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        if (Input.GetKey(KeyCode.W))
        {
            camVel += forward;
        }
        if (Input.GetKey(KeyCode.A))
        {
            camVel -= right;
        }
        if (Input.GetKey(KeyCode.S))
        {
            camVel -= forward;
        }
        if (Input.GetKey(KeyCode.D))
        {
            camVel += right;
        }

        camVel.Normalize();

        camVel *= moveSpeed;

        if (Input.GetKey(KeyCode.LeftShift))
        {
            camVel -= Vector3.up;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            camVel += Vector3.up;
        }
        transform.position += camVel * dt;
    }

    void HandleObstacle(float dt)
    {
        //t_obst will be between [0,1] and determines where in the extend animation we are
        if (Input.GetMouseButton(1))
        {
            t_obst += dt * obstSpeed;
        }
        else
        {
            t_obst -= dt * obstSpeed;
        }

        t_obst = Mathf.Clamp01(t_obst);

        //record current position to be able to calculate velocity after animation update
        Vector3 prev = obstacle.transform.position;

        //set animation based on t_obst
        anim["Scene"].normalizedTime = t_obst;

        obstVel = (obstacle.transform.position - prev) / dt;
    }

    void HandleLook()
    {
        //basic first person camera rotation using mouse
        Vector3 mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
        rotY -= mouseDelta.y * sensitivity;
        rotY = Mathf.Clamp(rotY, -90f, 90f);
        float rotX = transform.eulerAngles.y + mouseDelta.x * sensitivity;
        transform.eulerAngles = new Vector3(rotY, rotX, 0f);
    }

    public Vector3 getObstVel()
    {
        //since obstacle is attached to camera, add their velocities together
        return obstVel + camVel;
    }

    public GameObject GetObstacle()
    {
        return obstacle;
    }
}
