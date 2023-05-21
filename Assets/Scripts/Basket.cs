using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Basket : MonoBehaviour {
    public DiscAttributes discAttr;
    public Rigidbody discRb;

    void OnCollisionExit(Collision collision) {
        if (collision.gameObject.name == "Disc" && !discAttr.inFlight) {
            discAttr.canBePickedUp = false;
        }
    }
}
