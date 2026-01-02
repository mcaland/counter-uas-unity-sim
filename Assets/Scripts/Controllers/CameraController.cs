using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.iOS;

public class CameraController : MonoBehaviour
{
    public float sensitivity;
    public float slowSpeed, normalSpeed, sprintSpeed;
    private float currentSpeed;

    void Awake()
    {
        // workaround for touchpad compatibility
        foreach (var device in InputSystem.devices)
        {
            if (device is Mouse && !device.enabled)
            {
                InputSystem.EnableDevice(device);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        // only move the camera if we are running the simulation and we are paused
        if (GameManager.instance.runningSim && Time.timeScale == 0f)
        {
            if (Input.GetMouseButton(0))
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
                Movement();
                Rotation();
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    void Movement()
    {
        Vector3 input = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")); // axis raw unaffected by timescale

        if (Input.GetKey(KeyCode.LeftShift)) // sprint key
        {
            currentSpeed = sprintSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftAlt)) // slow key
        {
            currentSpeed = slowSpeed;
        }
        else
        {
            currentSpeed = normalSpeed;
        }

        // use unscaled delta time so pause doesn't impact our ability to move
        transform.Translate(input * currentSpeed * Time.unscaledDeltaTime);
    }

    void Rotation()
    {
        Vector3 mouseInput = new Vector3(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"), 0);
        transform.Rotate(mouseInput * sensitivity * Time.unscaledDeltaTime * 50);
        Vector3 eulerRotation = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, 0);
    }
}
