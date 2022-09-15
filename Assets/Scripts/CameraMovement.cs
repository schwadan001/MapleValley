using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public DiscAttributes discAttr;
    public Rigidbody discRb;
    public Camera cam;
    public Transform target;
    public float distanceToTarget;
    public Vector3 startingCameraOffset;

    private Vector3 previousPosition;
    private Vector3 flightOffset;

    void Start() {
        Vector3 newPosition = target.transform.position + startingCameraOffset;
        cam.transform.position = newPosition;
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.Escape)) {
            Application.Quit();
        }
    }
    
    // Camera rotate source: https://www.emmaprats.com/p/how-to-rotate-the-camera-around-an-object-in-unity3d
    void LateUpdate() {
        float curDistanceToTarget = Vector3.Distance(cam.transform.position, target.transform.position);
        if (Input.GetMouseButtonDown(0) && !discAttr.inFlight) {
            previousPosition = cam.ScreenToViewportPoint(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && !discAttr.inFlight) {
            // reposition camera
            Vector3 newPosition = cam.ScreenToViewportPoint(Input.mousePosition);
            Vector3 direction = previousPosition - newPosition;
            float rotationAroundYAxis = -direction.x * 180; // camera moves horizontally
            float rotationAroundXAxis = direction.y * 180; // camera moves vertically
            
            cam.transform.position = target.position;
            cam.transform.Rotate(new Vector3(1, 0, 0), rotationAroundXAxis);
            cam.transform.Rotate(new Vector3(0, 1, 0), rotationAroundYAxis, Space.World);
            cam.transform.Translate(new Vector3(0, 0, -distanceToTarget));

            flightOffset = cam.transform.position - target.transform.position;
            previousPosition = newPosition;
        }
        else if (discAttr.pickedUp) {
            Vector3 pos = cam.transform.position;
            pos.y = pos.y + 1;
            cam.transform.position = pos;
        }
        else if (curDistanceToTarget > distanceToTarget && !discAttr.isThrowable) {
            // set position of camera
            Vector3 discVelocity = discRb.velocity.normalized * distanceToTarget;
            Vector3 targetPosition = target.transform.position - discVelocity;
            cam.transform.position = Vector3.Lerp(
                cam.transform.position,
                targetPosition,
                Time.deltaTime * (float) Math.Max(Math.Sqrt(discRb.velocity.magnitude), 0.5f)
            );
            // set angle of camera
            cam.transform.LookAt(target.transform.position);
        }

        // reset disc if "R" is pressed
        if (Input.GetKeyDown(KeyCode.R)) {
            Start();
            cam.transform.LookAt(target.transform.position);
        }
    }
}