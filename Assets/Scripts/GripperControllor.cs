using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GripperController : MonoBehaviour
{
    private Transform boxInRange; 
    private Transform attachedBox; 

    public void PickUp()
    {
        if (attachedBox == null && boxInRange != null)
        {
            attachedBox = boxInRange;

            // 1. Disable physics
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // 2. Set parent-child relationship, but keep "world transform" unchanged!
            attachedBox.SetParent(this.transform, true);

            //Debug.Log("Picked up: " + attachedBox.name);
        }
    }

    public void Release()
    {
        if (attachedBox != null)
        {
            // 1. Detach parent-child relationship (Put back into world/scene)
            attachedBox.SetParent(null);

            // 2. Restore physics simulation
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            //Debug.Log("Released: " + attachedBox.name);
            attachedBox = null;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Box"))
        {
            boxInRange = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Box") && other.transform == boxInRange)
        {
            boxInRange = null;
        }
    }
}