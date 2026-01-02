using System;
using UnityEngine;

public class MotorController : MonoBehaviour
{
    public float maximumStrength = 1.0f;
    const float MINIMUM_STRENGTH = 0.0f;
    public float relativeStrength = 0.25f;
    public Vector3 force = Vector3.zero;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float angle = Vector3.Angle(transform.up, Vector3.up);
        force = Quaternion.AngleAxis(angle, Vector3.up) * -Physics.gravity * Math.Max(MINIMUM_STRENGTH, relativeStrength * maximumStrength);
    }
}
