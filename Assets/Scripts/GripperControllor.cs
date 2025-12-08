using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GripperController : MonoBehaviour
{
    private Transform boxInRange; // 记录当前感应到的箱子
    private Transform attachedBox; // 记录当前已经抓住的箱子

    public void PickUp()
    {
        if (attachedBox == null && boxInRange != null)
        {
            attachedBox = boxInRange;

            // 1. 关闭物理
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;

            // 2. 【关键修改】建立父子关系，但保持“世界变换”不变！
            // true 参数的意思是：保持箱子当前在世界中的大小、位置、角度不变
            // 防止因为吸盘本身有缩放，导致箱子瞬间变大或变小
            attachedBox.SetParent(this.transform, true);

            // 3. 【删除/注释这两行】
            // 不要强制归零！因为如果你的箱子Pivot在底部，归零后箱子可能就插到吸盘里面去了看不见了
            // attachedBox.localPosition = Vector3.zero; 
            // attachedBox.localRotation = Quaternion.identity;

            Debug.Log("抓到了: " + attachedBox.name);
        }
    }

    // 放下逻辑
    public void Release()
    {
        if (attachedBox != null)
        {
            // 1. 解除父子关系 (放回世界/场景中)
            attachedBox.SetParent(null);
            // 注意：如果是码垛，这里最好 SetParent(palletTransform);

            // 2. 恢复物理模拟
            Rigidbody rb = attachedBox.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = false;

            Debug.Log("放下了: " + attachedBox.name);
            attachedBox = null;
        }
    }
    // 【修改】把 OnTriggerEnter 改为 OnTriggerStay
    // Stay 的好处是：只要它们还插在一起，boxInRange 就永远有值，
    // 即使因为抖动导致 Enter/Exit 乱跳，Stay 也会在下一帧立刻补救回来。
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
