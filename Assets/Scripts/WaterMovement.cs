using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterMovement : MonoBehaviour
{
    public float moveSpeed = 1f;
    public float sensitivity = 100.0f;

    private Vector3 prev_mouse = Vector3.zero;
    private float rotY = 0f;

    // Start is called before the first frame update
    void Start()
    {
        //Cursor.lockState = CursorLockMode.Confined;
        Cursor.visible = false;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovement();
        HandleLook();

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
            Debug.Break();
        }
    }

    void HandleMovement()
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

        transform.transform.position += vel * Time.deltaTime;
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
}
