using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RobotMover : MonoBehaviour
{
    [Header("核心组件")]
    public Transform ikTarget;        // 拖入 IK_Target (控制位置)
    public GripperController gripper; // 拖入 Gripper_Sensor (控制抓取)

    [Header("参数设置")]
    public float moveSpeed = 1.0f;    // 移动速度

    void Update()
    {
        // ==========================
        // 1. 控制升降 (W = 上, S = 下)
        // ==========================
        if (Input.GetKey(KeyCode.W))
        {
            // 向世界坐标的上方移动
            ikTarget.Translate(Vector3.up * moveSpeed * Time.deltaTime, Space.World);
        }
        else if (Input.GetKey(KeyCode.S))
        {
            // 向世界坐标的下方移动
            ikTarget.Translate(Vector3.down * moveSpeed * Time.deltaTime, Space.World);
        }

        // ==========================
        // 2. 控制前后左右 (方向键) - 方便你把箱子移开
        // ==========================
        float h = Input.GetAxis("Horizontal"); // A/D 或 左右箭头
        float v = Input.GetAxis("Vertical");   // W/S 或 上下箭头 (注意 W/S 会和升降冲突，建议用箭头键)

        // 如果想把前后左右分离开，可以用 I/K/J/L 键，或者这里简单演示一下：
        if (Input.GetKey(KeyCode.UpArrow)) ikTarget.Translate(Vector3.forward * moveSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.DownArrow)) ikTarget.Translate(Vector3.back * moveSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.LeftArrow)) ikTarget.Translate(Vector3.left * moveSpeed * Time.deltaTime, Space.World);
        if (Input.GetKey(KeyCode.RightArrow)) ikTarget.Translate(Vector3.right * moveSpeed * Time.deltaTime, Space.World);


        // ==========================
        // 3. 控制抓取 (G = 抓, R = 放)
        // ==========================
        if (Input.GetKeyDown(KeyCode.G))
        {
            Debug.Log("指令：抓取");
            gripper.PickUp();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("指令：释放");
            gripper.Release();
        }
    }
}