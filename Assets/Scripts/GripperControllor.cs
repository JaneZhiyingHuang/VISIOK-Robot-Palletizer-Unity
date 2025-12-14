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

            // 1. 关闭物理
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // 2. 建立父子关系，但保持“世界变换”不变！
            attachedBox.SetParent(this.transform, true);

            Debug.Log("抓到了: " + attachedBox.name);
        }
    }

    public void Release()
    {
        if (attachedBox != null)
        {
            // 1. 解除父子关系 (放回世界/场景中)
            attachedBox.SetParent(null);

            // 2. 恢复物理模拟
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            Debug.Log("放下了: " + attachedBox.name);
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
