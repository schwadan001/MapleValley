using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DiscInfo {
    public string name;
    public float speed, glide, turn, fade;

    public DiscInfo(string name, float speed, float glide, float turn, float fade) {
        this.name = name;
        this.speed = speed;
        this.glide = glide;
        this.turn = turn;
        this.fade = fade;
    }
}

public class DiscMovement : MonoBehaviour {
    public DiscAttributes disc;
    public Camera cam;
    public TextMeshProUGUI discText;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI distanceText;

    private DiscInfo discInfo;
    private Vector3 cameraVector;
    private Vector3 resetLocation;
    
    private float baseStableSpeed = 16;
    private double stopMagnitude = 0.15;
    private float power;
    private DateTime throwStartTime;
    private DateTime throwEndTime;
    private Vector3 throwStartPos;
    private Boolean throwEnded;
    private string throwType = "bh";

    void Start () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        rb.maxAngularVelocity = 70;
        
        resetLocation = transform.position;

        setDisc("teebird");
        setPower(100);
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
        float wingDragCoefficient = 1f - (discInfo.speed / 25);
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
        double glideFactor = 0.9 + (discInfo.glide / 15);
        float liftForce = (float) Math.Abs (v.magnitude / 10 * glideFactor);
        if (liftForce > 0.1 && flightAngle > angleCutoff) {
            rb.AddForce (up() * (liftForce + liftForce * (flightAngle - angleCutoff) / angleCutoff));
        } else if (liftForce > 0.1 && flightAngle < angleCutoff) {
            rb.AddForce (-up() * liftForce * (angleCutoff - flightAngle) / angleCutoff);
        }

        // add turn or fade
        double modifiedTurn = (-discInfo.turn / 100) + (discInfo.speed / 500);
        double modifiedFade = (discInfo.fade / 50);
        double stableSpeed = baseStableSpeed + ((discInfo.turn + discInfo.fade) / 2) + (discInfo.speed / 15);

        float throwTypeStabilityFactor = (throwType == "fh" ? 1 : -1);
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
            float throwTypePowerFactor = (throwType == "bh" ? 1 : 0.9f);
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
            setThrowType("bh");
        } else if (Input.GetKeyDown (KeyCode.F) && disc.isThrowable) {
            setThrowType("fh");
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
            setPower(power + 5);
        } else if (Input.GetKeyUp(KeyCode.DownArrow) && disc.isThrowable) {
            setPower(power - 5);
        }
        // pick up disc, so it's ready to throw again
        else if (Input.GetMouseButtonDown (1) && !disc.inFlight && disc.canBePickedUp) {
            disc.pickedUp = true;
            disc.canBePickedUp = false;
            disc.isThrowable = true;
            Vector3 pos = transform.position;
            pos.y = pos.y + 0.7f;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(new Vector3(180, 0, 0));
        }

        // update distance if disc is in flight
        if (disc.inFlight) {
            double throwDistance = Math.Round(Math.Abs((transform.position - throwStartPos).magnitude * 3.5));
            distanceText.text = $"Throw distance: {throwDistance} ft";
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
        discText.text = $"{this.discInfo.name} ({this.throwType})".ToUpper();
    }

    private void setPower(float power) {
        if (power >= 50 && power <= 105) {
            this.power = power;
            powerText.text = $"Power: {this.power}%";
        }
    }

    private void setDisc(string discType) {
        switch (discType) {
            case "destroyer":
                discInfo = new DiscInfo(discType, 12, 5, -0.5f, 2);
                break;
            case "teedevil":
                discInfo = new DiscInfo(discType, 12, 5, -0.5f, 1);
                break;
            case "musket":
                discInfo = new DiscInfo(discType, 10, 5, -0.5f, 1);
                break;
            case "firebird":
                discInfo = new DiscInfo(discType, 9, 4, 0, 2);
                break;
            case "teebird":
                discInfo = new DiscInfo(discType, 7, 5, 0, 1);
                break;
            case "verdict":
                discInfo = new DiscInfo(discType, 5, 4, 0, 2);
                break;
            case "buzzz":
                discInfo = new DiscInfo(discType, 5, 5, -1, 1);
                break;
            case "zone":
                discInfo = new DiscInfo(discType, 4, 3, 0, 2);
                break;
            case "judge":
                discInfo = new DiscInfo(discType, 2, 4, 0, 1);
                break;
            case "berg":
                discInfo = new DiscInfo(discType, 1, 1, 0, 1);
                break;
        }
        discText.text = $"{this.discInfo.name} ({this.throwType})".ToUpper();
    }
}
