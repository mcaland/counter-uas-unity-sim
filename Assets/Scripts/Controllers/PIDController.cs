using System;
using UnityEngine;

public class PIDController
{
    private enum DerivativeMeasurement
    {
        Velocity,
        ErrorSlope
    }
    public float proportionalGain;
    public float integralGain;
    public float derivativeGain;

    private Vector3 errorLast;
    private Vector3 valueLast;

    private Vector3 integrationStored;
    private Vector3 integralSaturation;

    private bool derivativeInitialized = false;
    private DerivativeMeasurement derivativeMethod;
    public Vector3 UpdateState(float deltaTime, Vector3 currentValue, Vector3 targetValue)
    {
        Vector3 error = targetValue - currentValue;

        Vector3 P = proportionalGain * error;

        Vector3 errorSlope = (error - errorLast) / deltaTime;
        errorLast = error;

        Vector3 valueSlope = (currentValue - valueLast) / deltaTime;
        valueLast = currentValue;

        Vector3 derivativeValue = Vector3.zero;

        if (derivativeInitialized)
        {
            derivativeValue = errorSlope;
            if (derivativeMethod == DerivativeMeasurement.Velocity)
            {
                derivativeValue = -valueSlope;
            }
        }
        
        derivativeInitialized = true;

        integrationStored.x = Mathf.Clamp(integrationStored.x + error.x * deltaTime, -integralSaturation.x, integralSaturation.x);
        integrationStored.y = Mathf.Clamp(integrationStored.y + error.y * deltaTime, -integralSaturation.y, integralSaturation.y);
        integrationStored.z = Mathf.Clamp(integrationStored.z + error.z * deltaTime, -integralSaturation.z, integralSaturation.z);

        Vector3 I = integralGain * integrationStored;

        Vector3 D = derivativeGain * derivativeValue;

        return P + I + D;
    }

    public void ResetState()
    {
        derivativeInitialized = false;
    }
}
