using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class DroneController : MonoBehaviour
{
    public bool manualMovement = true;

    private Vector3 target = Vector3.zero;
    private Vector3 endpoint = Vector3.zero;
    public bool reached = true;
    private float distanceMarginOfError = 2f;
    public float lookaheadDistance = 10f;
    private List<Vector3> checkedAngles = new List<Vector3>();

    [SerializeField]
    private Rigidbody rb;
    
    [SerializeField]
    private GameObject FLmotor;
    private MotorController FLmotorController;
    [SerializeField]
    private GameObject FRmotor;
    private MotorController FRmotorController;
    [SerializeField]
    private GameObject BLmotor;
    private MotorController BLmotorController;
    [SerializeField]
    private GameObject BRmotor;
    private MotorController BRmotorController;

    private float maximumForceLift = 1f;
    private Vector3 direction = Vector3.zero;

    private Vector4 throttleMapping = Vector4.zero;

    public bool useRealMovement = false;


    private float upForce = 0f;
    private float horizontalForce = 10f;

    private PIDController controller = new PIDController();

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        controller.proportionalGain = 0.5f;
        controller.integralGain = 0.3f;
        controller.derivativeGain = 0.5f;
        FLmotorController = FLmotor.GetComponent<MotorController>();
        FRmotorController = FRmotor.GetComponent<MotorController>();
        BLmotorController = BLmotor.GetComponent<MotorController>();
        BRmotorController = BRmotor.GetComponent<MotorController>();
    }

    void Update()
    {
        if (manualMovement)
        {
            direction = Vector3.zero;

            // if (Input.GetKey(KeyCode.UpArrow))
            // {
            //     direction.y += 1f;
            // }
            // if (Input.GetKey(KeyCode.DownArrow))
            // {
            //     direction.y -= 1f;
            // }

            if (Input.GetKey(KeyCode.W))
            {
                direction.x += 1f;
            }
            if (Input.GetKey(KeyCode.A))
            {
                direction.z += 1f;
            }
            if (Input.GetKey(KeyCode.S))
            {
                direction.x -= 1f;
            }
            if (Input.GetKey(KeyCode.D))
            {
                direction.z -= 1f;
            }
            
            direction = direction.normalized;
        }
        else
        {
            direction = (target - transform.position).normalized;
        }

        if (useRealMovement)
        {
            maximumForceLift = 1f + Vector3.Dot(direction, Vector3.up);
            print(maximumForceLift);

            throttleMapping = CalculateThrottle(direction);

            FLmotorController.relativeStrength = 0.25f + throttleMapping.x;
            FRmotorController.relativeStrength = 0.25f + throttleMapping.y;
            BLmotorController.relativeStrength = 0.25f + throttleMapping.z;
            BRmotorController.relativeStrength = 0.25f + throttleMapping.w;

            FLmotorController.maximumStrength = maximumForceLift;
            FRmotorController.maximumStrength = maximumForceLift;
            BLmotorController.maximumStrength = maximumForceLift;
            BRmotorController.maximumStrength = maximumForceLift;

            // normalize the strengths based on our current maximum lift and values for each motor
            float scaleFactor = (FLmotorController.relativeStrength + FRmotorController.relativeStrength + BLmotorController.relativeStrength + BRmotorController.relativeStrength) / maximumForceLift;

            print(scaleFactor);

            FLmotorController.relativeStrength /= scaleFactor;
            FRmotorController.relativeStrength /= scaleFactor;
            BLmotorController.relativeStrength /= scaleFactor;
            BRmotorController.relativeStrength /= scaleFactor;
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (!reached && rb.useGravity)
        {
            if (useRealMovement)
            {
                rb.AddForceAtPosition(FLmotorController.force, FLmotor.transform.position, ForceMode.Acceleration);
                rb.AddForceAtPosition(FRmotorController.force, FRmotor.transform.position, ForceMode.Acceleration);
                rb.AddForceAtPosition(BLmotorController.force, BLmotor.transform.position, ForceMode.Acceleration);
                rb.AddForceAtPosition(BRmotorController.force, BRmotor.transform.position, ForceMode.Acceleration);
            }
            else
            {
                // MovementVertical();
                // MovementHorizontal();

                // rb.AddRelativeForce(transform.up * upForce);
                if ((rb.position - endpoint).magnitude > distanceMarginOfError)
                {
                    target = FindNextPosition(endpoint);
                    if (target != Vector3.one * -float.MaxValue) // valid point to go to
                    {
                        Vector3 input = controller.UpdateState(Time.fixedDeltaTime, rb.position, target);
                        rb.AddForce(input * 10);
                    }
                    else
                    {
                        print("Cannot pathfind further to destination.");
                        endpoint = rb.position; // nowhere we can go, so say we've made it back to our goal
                        target = endpoint;
                        reached = true;
                    }
                }
                else
                {
                    print("Reached destination.");
                    endpoint = rb.position;
                    target = endpoint;
                    reached = true;
                }
            }
        }
        else if (rb.useGravity)
        {
            Vector3 input = controller.UpdateState(Time.fixedDeltaTime, rb.position, target);
            rb.AddForce(input * 10);
        }
    }

    Vector4 CalculateThrottle(Vector3 direction)
    {
        Vector4 retval = Vector4.zero;

        retval.x = Math.Abs(Vector3.SignedAngle(direction, FLmotor.transform.up, Vector3.up)) / 180f;
        retval.y = Math.Abs(Vector3.SignedAngle(direction, FRmotor.transform.up, Vector3.up)) / 180f;
        retval.z = Math.Abs(Vector3.SignedAngle(direction, BLmotor.transform.up, Vector3.up)) / 180f;
        retval.w = Math.Abs(Vector3.SignedAngle(direction, BRmotor.transform.up, Vector3.up)) / 180f;

        return retval;
    }

    void MovementVertical()
    {
        upForce = -Physics.gravity.y * rb.mass;
        if (Input.GetKey(KeyCode.UpArrow) || direction.y > 0.0f)
        {
            upForce += 400f * rb.mass * direction.y;
        }
        else if (Input.GetKey(KeyCode.DownArrow) || direction.y < 0.0f)
        {
            upForce += 400f * rb.mass * direction.y;
            print(upForce);
        }
    }

    void MovementHorizontal()
    {
        rb.AddRelativeForce(new Vector3(direction.x * horizontalForce, 0f, direction.z * horizontalForce));
    }

    public void SetNavigation(Vector3 worldSpacePosition)
    {
        endpoint = worldSpacePosition;
        reached = false;
    }

    private Vector3 FindNextPosition(Vector3 pos)
    {
        checkedAngles.Clear();
        Vector3 val = FindNextPosition(pos, Vector3.zero);
        return val;
    }
    private Vector3 FindNextPosition(Vector3 pos, Vector3 rotation)
    {
        float rotationDeg = 30f;
        Vector3 noPathExistsVal = Vector3.one * -float.MaxValue;

        if (HasAngleBeenChecked(rotation))
        {
            return noPathExistsVal;
        }

        checkedAngles.Append(rotation);

        // check if we can directly go to the node
        RaycastHit hit;
        if (Physics.Raycast(rb.transform.position, (Quaternion.Euler(rotation) * pos) - rb.transform.position, out hit, maxDistance: lookaheadDistance))
        {
            Vector3 rotUpward = (rotation + new Vector3(rotationDeg, 0f, 0f));
            Vector3 rotDownward = (rotation + new Vector3(-rotationDeg, 0f, 0f));
            Vector3 rotLeft = (rotation + new Vector3(0f, rotationDeg, 0f));

            if (HasAngleBeenChecked(rotUpward) || HasAngleBeenChecked(rotDownward) || HasAngleBeenChecked(rotLeft) || Math.Abs(rotUpward.x) >= 360f || Math.Abs(rotDownward.x) >= 360f || Math.Abs(rotLeft.y) >= 360f)
            {
                return noPathExistsVal;
            }

            Vector3 upResult = FindNextPosition(pos, rotUpward);
            Vector3 leftResult = FindNextPosition(pos, rotLeft);
            //Vector3 downResult = FindNextPosition(pos, rotDownward);

            if (leftResult == noPathExistsVal)
            {
                // if (downResult == noPathExistsVal)
                // {
                //     return upResult;
                // }
                // else
                // {
                //     return downResult;
                // }
                return upResult;
            }
            else
            {
                return leftResult;
            }
        }
        else
        {
            return Quaternion.Euler(rotation) * pos;
        }
    }

    private bool HasAngleBeenChecked(Vector3 angle)
    {
        foreach (Vector3 checkedAngle in checkedAngles)
        {
            if (checkedAngle.Equals(angle))
            {
                return true;
            }
        }

        return false;
    }
}