using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DroneFleet : MonoBehaviour
{
    // save references to each drone and the net
    public bool useIndivdualDroneMovement = false; // drones move independently of this object
    public GameObject net;
    public GameObject flDrone;
    public GameObject frDrone;
    public GameObject blDrone;
    public GameObject brDrone;

    private Rigidbody rb;
    private PIDController controller = new PIDController();

    private Vector3 target = Vector3.zero; // the next position we need to reach to get to the endpoint
    private Vector3 endpoint = Vector3.zero; // FINAL destination point we want to reach
    private float distanceMarginOfError = 2f; // distance we can be from the endpoint and consider it reached
    public float lookaheadDistance = 10f; // how far we want to check for collisions
    private List<Vector3> checkedAngles = new List<Vector3>(); // angles we have checked in obstacle avoidance
    
    // initial positions of the drones and net
    private Vector3 flDroneBasePosition;
    private Vector3 frDroneBasePosition;
    private Vector3 blDroneBasePosition;
    private Vector3 brDroneBasePosition;
    private Vector3 netBasePosition;

    // booleans to keep track of state of the fleet
    public bool reached = false;
    public bool reachedInitialPosition = false;
    public bool attachedTargetDrone = false;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // disable drone pathfinding/autonomy
        if (!useIndivdualDroneMovement)
        {
            flDrone.GetComponent<Rigidbody>().useGravity = false;
            frDrone.GetComponent<Rigidbody>().useGravity = false;
            blDrone.GetComponent<Rigidbody>().useGravity = false;
            brDrone.GetComponent<Rigidbody>().useGravity = false;
            flDrone.GetComponent<DroneController>().reached = true;
            frDrone.GetComponent<DroneController>().reached = true;
            blDrone.GetComponent<DroneController>().reached = true;
            brDrone.GetComponent<DroneController>().reached = true;
        }

        flDroneBasePosition = flDrone.transform.position - transform.position;
        frDroneBasePosition = frDrone.transform.position - transform.position;
        blDroneBasePosition = blDrone.transform.position - transform.position;
        brDroneBasePosition = brDrone.transform.position - transform.position;
        netBasePosition = net.transform.position - transform.position;
        rb = GetComponent<Rigidbody>();
        controller.proportionalGain = 0.5f;
        controller.integralGain = 0.3f;
        controller.derivativeGain = 0.5f;
    }

    // Update is called once per frame
    void Update()
    {
        // Vector3[] vertexArray = new Vector3[net.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices.Length];
        // for (int i = 0; i < vertexArray.Length; i++)
        // {
        //     if (i == 0)
        //     {
        //         vertexArray.Append(flDrone.transform.position);
        //     }
        //     else if (i == 10)
        //     {
        //         vertexArray.Append(frDrone.transform.position);
        //     }
        //     else if (i == 110)
        //     {
        //         vertexArray.Append(blDrone.transform.position);
        //     }
        //     else if (i == 120)
        //     {
        //         vertexArray.Append(brDrone.transform.position);
        //     }
        //     else
        //     {
        //         vertexArray.Append(net.GetComponent<SkinnedMeshRenderer>().sharedMesh.vertices[i]);
        //     }
        // }
        // net.GetComponent<SkinnedMeshRenderer>().sharedMesh.SetVertices(vertexArray);
    }

    void FixedUpdate()
    {
        if (!useIndivdualDroneMovement)
        {
            if ((rb.position - endpoint).magnitude > distanceMarginOfError)
            {
                target = endpoint;
                //target = FindNextPosition(endpoint); // big performance hit
                if (target != Vector3.one * -float.MaxValue) // valid point to go to
                {
                    Vector3 input = controller.UpdateState(Time.fixedDeltaTime, rb.position, target);
                    rb.AddForce(input * 10);
                }
                else
                {
                    print("Cannot pathfind further to destination.");
                    endpoint = rb.position; // nowhere we can go, so say we've made it back to our goal
                    reached = true;
                }
            }
            else if (!reached)
            {
                print("Reached destination.");
                reached = true;
            }
            // align drones with fleet
            flDrone.transform.position = flDroneBasePosition + transform.position;
            frDrone.transform.position = frDroneBasePosition + transform.position;
            blDrone.transform.position = blDroneBasePosition + transform.position;
            brDrone.transform.position = brDroneBasePosition + transform.position;
            net.transform.position = netBasePosition + transform.position;
        }
    }

    public void SetNavigation(Vector3 targetPosition)
    {
        if (useIndivdualDroneMovement)
        {
            // order drones by distance
            List<GameObject> order = new List<GameObject>();
            float[] distances = new float[4];
            float separation = 2.5f;

            // sort drones by distance
            float flDistance = (flDrone.transform.position - targetPosition).magnitude;
            float frDistance = (frDrone.transform.position - targetPosition).magnitude;
            float blDistance = (blDrone.transform.position - targetPosition).magnitude;
            float brDistance = (brDrone.transform.position - targetPosition).magnitude;

            distances[0] = flDistance;
            distances[1] = frDistance;
            distances[2] = blDistance;
            distances[3] = brDistance;

            Array.Sort(distances);

            foreach (float dist in distances)
            {
                if (dist == flDistance && !order.Contains(flDrone))
                {
                    order.Add(flDrone);
                }
                else if (dist == frDistance && !order.Contains(frDrone))
                {
                    order.Add(frDrone);
                }
                else if (dist == blDistance && !order.Contains(blDrone))
                {
                    order.Add(blDrone);
                }
                else if (dist == brDistance && !order.Contains(brDrone))
                {
                    order.Add(brDrone);
                }
            }

            // send closest drones to the closest position
            order[0].GetComponent<DroneController>().SetNavigation(targetPosition + (separation * (targetPosition - order[0].transform.position).normalized));
            order[1].GetComponent<DroneController>().SetNavigation(targetPosition + (Quaternion.Euler(90f, 0f, 0f) * (targetPosition - order[1].transform.position).normalized) * separation);
            order[2].GetComponent<DroneController>().SetNavigation(targetPosition + (Quaternion.Euler(-90f, 0f, 0f) * (targetPosition - order[2].transform.position).normalized) * separation);
            order[3].GetComponent<DroneController>().SetNavigation(targetPosition + (separation * -(targetPosition - order[3].transform.position).normalized));
        }
        else
        {
            endpoint = targetPosition;
        }
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
        // we hit something in the way of the target position
        if (Physics.Raycast(rb.transform.position, (Quaternion.Euler(rotation) * pos) - rb.transform.position, out hit, maxDistance: lookaheadDistance))
        {
            Vector3 rotUpward = (rotation + new Vector3(rotationDeg, 0f, 0f));
            Vector3 rotDownward = (rotation + new Vector3(-rotationDeg, 0f, 0f));
            Vector3 rotLeft = (rotation + new Vector3(0f, rotationDeg, 0f));

            if (HasAngleBeenChecked(rotUpward) || HasAngleBeenChecked(rotDownward) || HasAngleBeenChecked(rotLeft) || Math.Abs(rotUpward.x) >= 360f || Math.Abs(rotDownward.x) >= 360f || Math.Abs(rotLeft.y) >= 360f)
            {
                return noPathExistsVal;
            }

            // check rotations to the left and upward for a valid path
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
