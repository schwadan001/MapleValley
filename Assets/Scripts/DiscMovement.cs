using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiscMovement : MonoBehaviour {
    public DiscAttributes disc;
    public Camera cam;
    public float power;
    public float stableSpeed;

    private Vector3 cameraVector;
    private double stopMagnitude = 0.05;

    void Start () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        rb.maxAngularVelocity = 70;
    }

    /*
     * Handle physics with FixedUpdate(), which is independent of user interaction and LateUpdate()
     * https://www.engineersedge.com/fluid_flow/circular_flat_disk_drag_coefficient_14035.htm
     * http://www.aerospaceweb.org/question/aerodynamics/q0231.shtml
     */
    void FixedUpdate () {
        // update position tracking variables
        cameraVector = transform.position - cam.transform.position;

        if (!disc.inFlight) {
            return;
        }

        Rigidbody rb = GetComponent<Rigidbody> ();
        Vector3 v = rb.velocity;

        // approximate drag
        float airDensity = 1.225f;
        float wingArea = transform.localScale.x * transform.localScale.y;
        float wingDragCoefficient = 0.6f; // 0.81f
        float radius = transform.localScale.x / 2;
        float flightPlateArea = (float)(Mathf.PI * radius * radius);
        float flightPlateDragCoefficient = 1.12f;

        float pctWing = Vector3.Cross(transform.transform.up.normalized, v.normalized).magnitude;
        float dragCoefficient = wingDragCoefficient * pctWing + flightPlateDragCoefficient * (1 - pctWing);
        float crossSectionArea = wingArea * pctWing + flightPlateArea * (1 - pctWing);

        float dragForce = (float) (airDensity * v.magnitude * v.magnitude * dragCoefficient * crossSectionArea) / 2;
        if (dragForce > 0.1 && v.magnitude > stopMagnitude) {
            rb.AddForce (-v.normalized * dragForce);
        }

        // approximate lift
        float angleCutoff = 85;
        float flightAngle = Vector3.Angle(transform.transform.up.normalized, v.normalized);
        float liftForce = (float) Math.Max ((v.magnitude / 9), 0);
        if (liftForce > 0.1 && flightAngle > angleCutoff) {
            rb.AddForce (transform.up * (liftForce + liftForce * (flightAngle - angleCutoff) / angleCutoff));
        } else if (liftForce > 0.1 && flightAngle < angleCutoff) {
            rb.AddForce (-transform.up * liftForce * (angleCutoff - flightAngle) / angleCutoff);
        }

        // add turn or fade
        double baseTurn = 0.02;
        double baseFade = 0.03;
        Vector3 angularVelocity = rb.angularVelocity;
        if (v.magnitude > stableSpeed) {
            transform.RotateAround(new Vector3 (-v.x, 0, -v.z), degreesToRadians(baseTurn * (v.magnitude - stableSpeed)));
        } else if (v.magnitude < stableSpeed && v.magnitude > 1 && transform.up.y > 0) {
            transform.RotateAround(new Vector3 (-v.x, 0, -v.z), degreesToRadians(baseFade * (v.magnitude - stableSpeed)));
        }
        rb.angularVelocity = new Vector3(0, 0, 0);
        rb.AddTorque (transform.up * angularVelocity.magnitude, ForceMode.VelocityChange);
    }

    /*
     * Handle user interactions with Update(), so we can sync those actions with LateUpdate()
     */
    void Update () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        Vector3 v = rb.velocity;
        Vector3 currentPosition = transform.position;
        disc.pickedUp = false;
        if (Input.GetKeyDown (KeyCode.Space) && disc.isThrowable) {
            disc.isThrowable = false;
            disc.inFlight = true;
            rb.isKinematic = false;
            rb.AddForce (cameraVector * power);
            rb.AddTorque (transform.up * power, ForceMode.VelocityChange);
            return;
        } else if (Input.GetKeyDown (KeyCode.W) && disc.isThrowable && transform.up.y > 0) {
            transform.RotateAround(new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(-1));
        } else if (Input.GetKeyDown (KeyCode.A) && disc.isThrowable && transform.up.y > 0) {
            transform.RotateAround(cameraVector, degreesToRadians(1));
        } else if (Input.GetKeyDown (KeyCode.S) && disc.isThrowable && transform.up.y > 0) {
            transform.RotateAround(new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(1));
        } else if (Input.GetKeyDown (KeyCode.D) && disc.isThrowable && transform.up.y > 0) {
            transform.RotateAround(cameraVector, degreesToRadians(-1));
        } else if (disc.inFlight && v.magnitude < stopMagnitude) {
            rb.isKinematic = true;
            disc.inFlight = false;
            disc.canBePickedUp = true;
        } else if (Input.GetMouseButtonDown (1) && !disc.inFlight && disc.canBePickedUp) {
            disc.pickedUp = true;
            disc.canBePickedUp = false;
            disc.isThrowable = true;
            Vector3 pos = transform.position;
            pos.y = pos.y + 1;
            transform.position = pos;
            transform.localEulerAngles = new Vector3 (0, 0, 0);
        }
        if (Input.GetMouseButtonDown (2)) {
            Debug.Log ("Angular velocity: " + rb.angularVelocity);
            Debug.Log ("Magnitude: " + v.magnitude);
            Debug.Log ("Velocity: " + v.normalized);
            Debug.Log ("Camera: " + cameraVector);
        }
    }

    private float degreesToRadians(double degrees) {
        return (float)(2 * Math.PI / 360 * degrees);
    }
}