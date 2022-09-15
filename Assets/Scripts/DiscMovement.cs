using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlightRatings {
    public float speed, glide, turn, fade;

    public FlightRatings(float speed, float glide, float turn, float fade) {
        this.speed = speed;
        this.glide = glide;
        this.turn = turn;
        this.fade = fade;
    }
}

public class DiscMovement : MonoBehaviour {
    public DiscAttributes disc;
    public Camera cam;
    public float power = 100;

    private FlightRatings flightRatings;
    private Vector3 cameraVector;
    private Vector3 resetLocation;
    
    private float baseStableSpeed = 16;
    private double stopMagnitude = 0.15;
    private DateTime throwStartTime;
    private DateTime throwEndTime;
    private Vector3 throwStartPos;
    private Vector3 throwEndPos;
    private Boolean throwEnded;
    private string throwType = "backhand";

    void Start () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        rb.maxAngularVelocity = 70;
        
        resetLocation = transform.position;
        setDisc("teebird");
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
        float wingArea = (transform.localScale.x * 0.3f) * (transform.localScale.y * 0.015f);
        float wingDragCoefficient = 1f - (flightRatings.speed / 25);
        float radius = transform.localScale.x * 0.3f / 2;
        float flightPlateArea = (float)(Mathf.PI * radius * radius);
        float flightPlateDragCoefficient = 1.12f;

        float pctWing = Vector3.Cross(up().normalized, v.normalized).magnitude;
        float dragCoefficient = wingDragCoefficient * pctWing + flightPlateDragCoefficient * (1 - pctWing);
        float crossSectionArea = wingArea * pctWing + flightPlateArea * (1 - pctWing);
        double spinRate = Math.Max((20 - (DateTime.UtcNow - throwStartTime).TotalSeconds) / 20, 0.5);

        float dragForce = (float) (
            airDensity * v.magnitude * v.magnitude * dragCoefficient * crossSectionArea / 2.5 / spinRate
        );
        if (dragForce > 0.1 && v.magnitude > stopMagnitude) {
            rb.AddForce (-v.normalized * dragForce);
        }

        // approximate lift
        float angleCutoff = 85;
        float flightAngle = Vector3.Angle(up().normalized, v.normalized);
        double glideFactor = 0.9 + (flightRatings.glide / 15);
        float liftForce = (float) Math.Abs (v.magnitude / 10 * glideFactor);
        if (liftForce > 0.1 && flightAngle > angleCutoff) {
            rb.AddForce (up() * (liftForce + liftForce * (flightAngle - angleCutoff) / angleCutoff));
        } else if (liftForce > 0.1 && flightAngle < angleCutoff) {
            rb.AddForce (-up() * liftForce * (angleCutoff - flightAngle) / angleCutoff);
        }

        // add turn or fade
        double modifiedTurn = (-flightRatings.turn / 100) + (flightRatings.speed / 500);
        double modifiedFade = (flightRatings.fade / 50);
        double stableSpeed = baseStableSpeed + ((flightRatings.turn + flightRatings.fade) / 2) + (flightRatings.speed / 15);

        float throwTypeStabilityFactor = (throwType == "forehand" ? 1 : -1);
        if (v.magnitude > stableSpeed) {
            transform.RotateAround(
                new Vector3 (throwTypeStabilityFactor * v.x, 0, throwTypeStabilityFactor * v.z),
                degreesToRadians(modifiedTurn * (v.magnitude - stableSpeed))
            );
        } else if (v.magnitude < stableSpeed && v.magnitude > 1 && up().y > 0) {
            transform.RotateAround(
                new Vector3 (throwTypeStabilityFactor * v.x, 0, throwTypeStabilityFactor * v.z),
                degreesToRadians(modifiedFade * (v.magnitude - stableSpeed))
            );
        }
        Vector3 angularVelocity = rb.angularVelocity;
        rb.angularVelocity = new Vector3(0, 0, 0);
        rb.AddTorque (up() * angularVelocity.magnitude, ForceMode.VelocityChange);
    }

    /*
     * Handle user interactions with Update(), so we can sync those actions with LateUpdate().
     * Also handle changes to disc "state" (ex. in-flight vs. ready to throw)
     */
    void Update () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        Vector3 v = rb.velocity;
        Vector3 currentPosition = transform.position;
        disc.pickedUp = false;
        // throw disc
        if (Input.GetKeyDown (KeyCode.Space) && disc.isThrowable) {
            float throwTypePowerFactor = (throwType == "backhand" ? 1 : 0.9f);
            disc.isThrowable = false;
            disc.inFlight = true;
            rb.isKinematic = false;
            rb.AddForce (cameraVector * power * throwTypePowerFactor);
            rb.AddTorque (up() * power * throwTypePowerFactor, ForceMode.VelocityChange);
            throwStartTime = DateTime.UtcNow;
            throwStartPos = transform.position;
            throwEnded = false;
            return;
        }
        // change angle of release 
        else if (Input.GetKeyDown (KeyCode.W) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(-1)
            );
        } else if (Input.GetKeyDown (KeyCode.A) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                cameraVector, degreesToRadians(1)
            );
        } else if (Input.GetKeyDown (KeyCode.S) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(1)
            );
        } else if (Input.GetKeyDown (KeyCode.D) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                cameraVector, degreesToRadians(-1)
            );
        } 
        // change throw type
        else if (Input.GetKeyDown (KeyCode.B) && disc.isThrowable) {
            setThrowType("backhand");
        } else if (Input.GetKeyDown (KeyCode.F) && disc.isThrowable) {
            setThrowType("forehand");
        }
        // reset disc to original state
        else if (Input.GetKeyDown (KeyCode.R)) {
            rb.isKinematic = true;
            transform.position = resetLocation;
            disc.pickedUp = true;
            disc.canBePickedUp = false;
            disc.isThrowable = true;
            disc.inFlight = false;
            transform.rotation = Quaternion.Euler(new Vector3(180, 0, 0));
        }
        // switch disc
        else if (Input.GetKeyDown (KeyCode.Alpha0) && disc.isThrowable) {
            setDisc("berg");
        } else if (Input.GetKeyDown (KeyCode.Alpha1) && disc.isThrowable) {
            setDisc("judge");
        } else if (Input.GetKeyDown (KeyCode.Alpha2) && disc.isThrowable) {
            setDisc("zone");
        } else if (Input.GetKeyDown (KeyCode.Alpha3) && disc.isThrowable) {
            setDisc("buzzz");
        } else if (Input.GetKeyDown (KeyCode.Alpha4) && disc.isThrowable) {
            setDisc("verdict");
        } else if (Input.GetKeyDown (KeyCode.Alpha5) && disc.isThrowable) {
            setDisc("teebird");
        } else if (Input.GetKeyDown (KeyCode.Alpha6) && disc.isThrowable) {
            setDisc("firebird");
        } else if (Input.GetKeyDown (KeyCode.Alpha7) && disc.isThrowable) {
            setDisc("musket");
        } else if (Input.GetKeyDown (KeyCode.Alpha8) && disc.isThrowable) {
            setDisc("teedevil");
        } else if (Input.GetKeyDown (KeyCode.Alpha9) && disc.isThrowable) {
            setDisc("destroyer");
        }
        // change power level
        else if (Input.GetKeyUp(KeyCode.UpArrow) && disc.isThrowable) {
            power = Math.Min(power + 5, 105);
            Debug.Log ($"power: {power}%");
        } else if (Input.GetKeyUp(KeyCode.DownArrow) && disc.isThrowable) {
            power = Math.Max(power - 5, 50);
            Debug.Log ($"power: {power}%");
        }
        // pick up disc, so it's ready to throw again
        else if (Input.GetMouseButtonDown (1) && !disc.inFlight && disc.canBePickedUp) {
            disc.pickedUp = true;
            disc.canBePickedUp = false;
            disc.isThrowable = true;
            Vector3 pos = transform.position;
            pos.y = pos.y + 1;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(new Vector3(180, 0, 0));
        }

        // determine if disc has stopped
        if (disc.inFlight && v.magnitude < stopMagnitude) {
            if (!throwEnded) {
                throwEndTime = DateTime.UtcNow;
                throwEnded = true;
            } else if ((DateTime.UtcNow - throwEndTime).TotalSeconds > 2) {
                rb.isKinematic = true;
                disc.inFlight = false;
                disc.canBePickedUp = true;
                throwEndPos = transform.position;
                double throwDistance = Math.Round(Math.Abs((throwEndPos - throwStartPos).magnitude * 3.5));
                Debug.Log ($"Throw distance: {throwDistance} ft");
            }
        } else {
            throwEnded = false;
        }

        // print various stats for debugging
        if (Input.GetKeyDown (KeyCode.P)) {
            Debug.Log ("Angular velocity: " + rb.angularVelocity);
            Debug.Log ("Magnitude: " + v.magnitude);
            Debug.Log ("Velocity: " + v.normalized);
            Debug.Log ("Camera: " + cameraVector);
        }
    }

    private Vector3 up() {
        return -transform.up;
    }

    private float degreesToRadians(double degrees) {
        return (float)(2 * Math.PI / 360 * degrees);
    }

    private void setThrowType(string throwType) {
        this.throwType = throwType;
        Debug.Log ($"Throw type set to {throwType}");
    }

    private void setDisc(string discType) {
        Debug.Log ($"Disc selection: {discType}");
        switch (discType) {
            case "destroyer":
                flightRatings = new FlightRatings(12, 5, -0.5f, 2);
                break;
            case "teedevil":
                flightRatings = new FlightRatings(12, 5, -0.5f, 1);
                break;
            case "musket":
                flightRatings = new FlightRatings(10, 5, -0.5f, 1);
                break;
            case "firebird":
                flightRatings = new FlightRatings(9, 4, 0, 2);
                break;
            case "teebird":
                flightRatings = new FlightRatings(7, 5, 0, 1);
                break;
            case "verdict":
                flightRatings = new FlightRatings(5, 4, 0, 2);
                break;
            case "buzzz":
                flightRatings = new FlightRatings(5, 5, -1, 1);
                break;
            case "zone":
                flightRatings = new FlightRatings(4, 3, 0, 2);
                break;
            case "judge":
                flightRatings = new FlightRatings(2, 4, 0, 1);
                break;
            case "berg":
                flightRatings = new FlightRatings(1, 1, 0, 1);
                break;
        }
    }
}
