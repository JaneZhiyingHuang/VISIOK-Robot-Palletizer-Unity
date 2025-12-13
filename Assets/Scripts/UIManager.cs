using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("--- 脚本连接 ---")]
    public BoxFeeder boxFeeder;
    public PalletCalculator palletCalc; 
    public AutoManager autoManager;

    [Header("--- 箱子尺寸按钮 ---")]
    public Button btnL;
    public Button btnM;
    public Button btnS;

    [Header("--- 方向按钮 ---")]
    public Button btnDefault; 
    public Button btnRotate;  

    [Header("--- 启动按钮 ---")]
    public Button btnStart;

    [Header("--- 选中高亮颜色 ---")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.cyan;

    private bool hasStarted = false;

    void Start()
    {
        // 1. 尺寸按钮绑定
        btnL.onClick.AddListener(() => OnBoxSelected("L"));
        btnM.onClick.AddListener(() => OnBoxSelected("M"));
        btnS.onClick.AddListener(() => OnBoxSelected("S"));

        // 2. 方向按钮绑定
        // false 代表 Default, true 代表 Rotate 90
        if (btnDefault) btnDefault.onClick.AddListener(() => OnOrientationSelected(false));
        if (btnRotate) btnRotate.onClick.AddListener(() => OnOrientationSelected(true));

        if (btnStart != null) btnStart.onClick.AddListener(OnStartClicked);

        // 3. 默认选中 L 和 Default
        OnBoxSelected("L");
        OnOrientationSelected(false);
    }

    void OnStartClicked()
    {
        if (hasStarted) return;
        hasStarted = true;
        btnStart.interactable = false; // 禁用启动按钮

        boxFeeder.StartSpawning();
        autoManager.BeginWork();
    }

    // ==========================================
    // 方向切换逻辑
    // ==========================================
    void OnOrientationSelected(bool isRotate90)
    {
        if (hasStarted)
        {
            Debug.LogWarning("⚠️ 任务运行中，禁止切换方向！");
            return;
        }

        // A. 视觉反馈
        SetBtnColor(btnDefault, !isRotate90);
        SetBtnColor(btnRotate, isRotate90);

        // B. 修改 PalletCalculator 的参数
        // 根据你 PalletCalculator 的定义：
        // Align_X 对应 "Rotate 90"
        // Align_Z_Rotated 对应 "Default"
        if (isRotate90)
        {
            palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_X;
        }
        else
        {
            palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_Z_Rotated;
        }

        // C. 【关键】方向变了，能放的数量可能变了，通知 BoxFeeder 刷新容量
        boxFeeder.RefreshCapacity();
    }

    // ==========================================
    // 尺寸切换逻辑
    // ==========================================
    void OnBoxSelected(string type)
    {
        if (hasStarted)
        {
            Debug.LogWarning("⚠️ 任务运行中，禁止切换尺寸！");
            return;
        }

        // A. 视觉反馈
        SetBtnColor(btnL, type == "L");
        SetBtnColor(btnM, type == "M");
        SetBtnColor(btnS, type == "S");

        // B. 告诉 BoxFeeder 切换类型 (它会自动去读尺寸并同步给 Pallet)
        boxFeeder.SwitchBoxType(type);
    }

    void SetBtnColor(Button btn, bool isSelected)
    {
        if (btn == null) return;
        var colors = btn.colors;
        colors.normalColor = isSelected ? selectedColor : normalColor;
        colors.selectedColor = isSelected ? selectedColor : normalColor;
        btn.colors = colors;
    }
}