using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;


public class DiscType {
    public string name;
    public float speed, glide, turn, fade, stability;

    public DiscType(string name, float speed, float glide, float turn, float fade) {
        this.name = name;
        this.speed = speed;
        this.glide = glide;
        this.turn = turn;
        this.fade = fade;
        this.stability = fade + turn;
    }
}


public class DiscMovement : MonoBehaviour {
    public DiscAttributes disc;
    public Camera cam;
    public TextMeshProUGUI discText;
    public TextMeshProUGUI powerText;
    public TextMeshProUGUI distanceText;
    public GameObject backhandButton;
    public GameObject forehandButton;
    public GameObject bagButton;
    public GameObject bagContainer;

    private Dictionary<int, GameObject> buttons = new Dictionary<int, GameObject>();
    private int discButtonStartNum;

    private DiscType discInfo;
    private Vector3 cameraVector;
    private Vector3 resetLocation;
    
    private float baseStableSpeed = 16;
    private double stopMagnitude = 0.15;
    private string throwType = "bh";
    private float power;

    private DateTime throwStartTime;
    private DateTime throwEndTime;
    private Vector3 throwStartPos;
    private Boolean throwEnded;

    private Dictionary<string, DiscType> discTypes = new Dictionary<string, DiscType> () {
        {"berg", new DiscType("Kastaplast Berg", 1, 1, 0, 1)},
        {"judge", new DiscType("Dynamic Discs Judge", 2, 4, 0, 1)},
        {"zone", new DiscType("Discraft Zone", 4, 3, 0, 3)},
        {"buzzz", new DiscType("Discraft Buzzz", 5, 5, -1, 1)},
        {"verdict", new DiscType("Dynamic Discs Verdict", 5, 4, 0, 2)},
        {"explorer", new DiscType("Latitude 64 Explorer", 7, 5, 0, 1)},
        {"firebird", new DiscType("Innova Firebird", 9, 4, 0, 2)},
        {"musket", new DiscType("Latitude 64 Musket", 10, 5, -0.5f, 1)},
        {"sheriff", new DiscType("Dynamic Discs Sheriff", 12, 5, -0.5f, 1)},
        {"destroyer", new DiscType("Innova Destroyer", 12, 5, 0, 1.5f)}
    };

    void Start () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        rb.maxAngularVelocity = 70;
        
        this.resetLocation = transform.position;

        // set up HUD / buttons
        int buttonNum = 1;
        this.buttons.Add(buttonNum++, this.backhandButton);
        this.buttons.Add(buttonNum++, this.forehandButton);
        this.buttons.Add(buttonNum++, this.bagButton);
        this.discButtonStartNum = buttonNum;
        foreach (KeyValuePair<string, DiscType> kv in this.discTypes) {
            int discIdx = buttonNum - discButtonStartNum;
            GameObject btn = DefaultControls.CreateButton(
                new DefaultControls.Resources()
            );
            btn.name = $"{kv.Key}DiscButton";
            btn.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            btn.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0);
            btn.GetComponent<RectTransform>().offsetMin = new Vector2(15, 60 + 40 * (discIdx));
            btn.GetComponent<RectTransform>().offsetMax = new Vector2(215, 60 + 40 * (discIdx + 1) - 5);
            btn.GetComponentInChildren<Text>().text = kv.Value.name;
            btn.transform.SetParent(bagContainer.transform, false);
            this.buttons.Add(buttonNum++, btn);
        } 
        foreach (KeyValuePair<int, GameObject> kv in this.buttons) {
            extractButton(kv.Value).onClick.AddListener(() => buttonClicked(kv.Key));
        }
        deactivateHUD();
        activateDefaultHUD();

        // set defaults
        setDisc("buzzz");
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

        // approximate & apply drag
        float airDensity = 1.225f;
        float wingArea = 0.3f * 0.015f;
        float wingDragCoefficient = 1f - (discInfo.speed / 25);
        float radius = 0.3f / 2;
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

        // approximate & apply lift
        float angleCutoff = 85;
        float flightAngle = Vector3.Angle(up().normalized, v.normalized);
        double glideFactor = 0.9 + (discInfo.glide / 15);
        float liftForce = (float) Math.Abs (v.magnitude / 10 * glideFactor);
        if (liftForce > 0.1 && flightAngle > angleCutoff) {
            rb.AddForce (up() * (liftForce + liftForce * (flightAngle - angleCutoff) / angleCutoff));
        } else if (liftForce > 0.1 && flightAngle < angleCutoff) {
            rb.AddForce (-up() * liftForce * (angleCutoff - flightAngle) / angleCutoff);
        }

        // apply turn or fade
        double modifiedTurn = (-discInfo.turn / 100) + (discInfo.speed / 600);
        double modifiedFade = (discInfo.fade / 50);
        double stableSpeed = baseStableSpeed + discInfo.stability + (discInfo.speed / 15);

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
     * Also handle changes to disc "state" (ex. in-flight vs. ready to throw) and UI updates
     */
    void Update () {
        Rigidbody rb = GetComponent<Rigidbody> ();
        Vector3 v = rb.velocity;
        Vector3 currentPosition = transform.position;
        disc.pickedUp = false;

        /*
         * User Input Checks
         */

        // throw disc
        if (Input.GetKeyDown (KeyCode.Space) && disc.isThrowable) {
            float throwTypePowerFactor = (throwType == "bh" ? 1 : 0.9f);
            disc.isThrowable = false;
            disc.inFlight = true;
            rb.isKinematic = false;
            deactivateHUD();
            rb.AddForce (cameraVector * power * throwTypePowerFactor);
            rb.AddTorque (up() * power * throwTypePowerFactor, ForceMode.VelocityChange);
            throwStartTime = DateTime.UtcNow;
            throwStartPos = transform.position;
            throwEnded = false;
        }

        // change angle of release 
        else if (Input.GetKeyDown (KeyCode.W) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(-1)
            );
        }
        else if (Input.GetKeyDown (KeyCode.A) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                cameraVector, degreesToRadians(1)
            );
        }
        else if (Input.GetKeyDown (KeyCode.S) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                new Vector3(cameraVector.z, cameraVector.y, -cameraVector.x), degreesToRadians(1)
            );
        }
        else if (Input.GetKeyDown (KeyCode.D) && disc.isThrowable && up().y > 0) {
            transform.RotateAround(
                cameraVector, degreesToRadians(-1)
            );
        }

        // change throw type
        else if (Input.GetKeyDown (KeyCode.B) && disc.isThrowable) {
            buttonClicked(1);
        }
        else if (Input.GetKeyDown (KeyCode.F) && disc.isThrowable) {
            buttonClicked(2);
        }

        // reset disc to original state
        else if (Input.GetKeyDown (KeyCode.R)) {
            rb.isKinematic = true;
            transform.position = resetLocation;
            disc.pickedUp = true;
            disc.canBePickedUp = false;
            disc.isThrowable = true;
            disc.inFlight = false;
            transform.rotation = Quaternion.Euler(new Vector3(0, 0, 0));
            activateDefaultHUD();
        }

        // switch disc
        else if (Input.GetKeyDown (KeyCode.Alpha0) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 0);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha1) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 1);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha2) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 2);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha3) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 3);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha4) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 4);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha5) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 5);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha6) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 6);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha7) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 7);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha8) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 8);
        }
        else if (Input.GetKeyDown (KeyCode.Alpha9) && disc.isThrowable) {
            buttonClicked(discButtonStartNum + 9);
        }

        // change power level
        else if (Input.GetKeyUp(KeyCode.UpArrow) && disc.isThrowable) {
            setPower(power + 5);
        }
        else if (Input.GetKeyUp(KeyCode.DownArrow) && disc.isThrowable) {
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
            transform.rotation = Quaternion.Euler(new Vector3(0, 90, 0));
            activateDefaultHUD();
        }

        /*
         * Checks Independent of User Input
         */

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

    /*
     * Physics Helper Functions
     */

    private Vector3 up() {
        return transform.up;
    }

    private float degreesToRadians(double degrees) {
        return (float)(2 * Math.PI / 360 * degrees);
    }

    /*
     * Functions to set state & make related updates
     */

    private void setThrowType(string throwType) {
        this.throwType = throwType;
    }

    private void setPower(float power) {
        if (power >= 50 && power <= 110) {
            this.power = power;
            this.powerText.text = $"Power: {this.power}%";
        }
    }

    private void setDisc(string discType) {
        this.discInfo = this.discTypes[discType];
        this.discText.text = this.discInfo.name;
        var material = Resources.Load("Discs/Materials/" + this.discInfo.name) as Material;
        GetComponent<MeshRenderer>().material = material;
        bagContainer.SetActive(false);
    }

    /*
     * HUD Functions
     */

    private Button extractButton(GameObject gameObject) {
        return gameObject.GetComponentInChildren<Button>();
    }

    private void buttonClicked(int buttonNum) {
        GameObject obj = this.buttons[buttonNum];
        Button btn = extractButton(obj);
        switch(buttonNum) {
            case 1:
                btn.interactable = false;
                extractButton(this.forehandButton).interactable = true;
                setThrowType("bh");
                break;
            case 2:
                btn.interactable = false;
                extractButton(this.backhandButton).interactable = true;
                setThrowType("fh");
                break;
            case 3:
                bagContainer.SetActive(!bagContainer.activeSelf);
                break;
            case 4:
                setDisc("berg");
                break;
            case 5:
                setDisc("judge");
                break;
            case 6:
                setDisc("zone");
                break;
            case 7:
                setDisc("buzzz");
                break;
            case 8:
                setDisc("verdict");
                break;
            case 9:
                setDisc("explorer");
                break;
            case 10:
                setDisc("firebird");
                break;
            case 11:
                setDisc("musket");
                break;
            case 12:
                setDisc("sheriff");
                break;
            case 13:
                setDisc("destroyer");
                break;
        }
    }

    private void deactivateHUD() {
        foreach (KeyValuePair<int, GameObject> kv in this.buttons) {
            GameObject obj = kv.Value;
            Button btn = extractButton(obj);
            switch(obj.tag) {
                case "HUDMenu":
                    btn.interactable = false;
                    break;
                default:
                    obj.SetActive(false);
                    break;
            }
        }
        bagContainer.SetActive(false);
    }

    private void activateDefaultHUD() {
        // actions by tag
        foreach (KeyValuePair<int, GameObject> kv in this.buttons) {
            GameObject obj = kv.Value;
            Button btn = extractButton(obj);
            switch(obj.tag) {
                case "HUDMenu":
                    btn.interactable = true;
                    break;
                default:
                    obj.SetActive(true);
                    break;               
            }
        }
        // specific button actions
        if (this.throwType == "bh") {
            extractButton(this.backhandButton).interactable = false;
        } else {
            extractButton(this.forehandButton).interactable = false;
        }
    }
}
