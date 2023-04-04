using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Basket : MonoBehaviour {
    public DiscAttributes discAttr;
    public Rigidbody discRb;

    void OnCollisionEnter(Collision collision) {
        if (collision.gameObject.name == "Disc") {
            discAttr.inFlight = false;
            discRb.isKinematic = true;
        }
    }
}
