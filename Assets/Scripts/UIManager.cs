using UnityEngine;
using UnityEngine.UI;
using TMPro;

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

    [Header("--- 层数控制 ---")]
    public Button btnLayerMinus; 
    public Button btnLayerPlus;
    public TMP_Text txtLayerDisplay;
    public TMP_Text txtMaxInfoDisplay;
    public TMP_Text txtSafeHeightDisplay;

    [Header("--- 启动按钮 ---")]
    public Button btnStart;

    [Header("--- 选中高亮颜色 ---")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.cyan;

    private bool hasStarted = false;

    void Start()
    {
        // 1. 尺寸按钮
        btnL.onClick.AddListener(() => OnBoxSelected("L"));
        btnM.onClick.AddListener(() => OnBoxSelected("M"));
        btnS.onClick.AddListener(() => OnBoxSelected("S"));

        // 2. 方向按钮
        if (btnDefault) btnDefault.onClick.AddListener(() => OnOrientationSelected(false));
        if (btnRotate) btnRotate.onClick.AddListener(() => OnOrientationSelected(true));

        // 3. 层数按钮
        if (btnLayerMinus) btnLayerMinus.onClick.AddListener(() => ChangeLayerCount(-1));
        if (btnLayerPlus) btnLayerPlus.onClick.AddListener(() => ChangeLayerCount(1));

        if (btnStart != null) btnStart.onClick.AddListener(OnStartClicked);

        // 4. 默认选中 L 和 Default
        OnBoxSelected("L");
        OnOrientationSelected(false);
    }

    void OnStartClicked()
    {
        if (hasStarted) return;
        hasStarted = true;
        btnStart.interactable = false;

        boxFeeder.StartSpawning();
        autoManager.BeginWork();
    }

    // ==========================================
    // 层数加减逻辑
    // ==========================================
    void ChangeLayerCount(int change)
    {
        if (hasStarted) return;

        // 1. 获取当前最大允许层数
        int maxLayers = palletCalc.GetMaxSafeLayers();

        // 2. 计算新层数
        int current = palletCalc.targetLayers;
        current += change;

        // 3. 限制范围 (最少 1 层，最多 maxLayers 层)
        current = Mathf.Clamp(current, 1, maxLayers);

        // 4. 应用设置
        palletCalc.targetLayers = current;

        // 5. 更新 UI 和 Feeder 容量
        UpdateLayerUI();
        boxFeeder.RefreshCapacity(); // 关键：层数变了，总数量也变了
    }

    // 辅助：更新显示的文字
    void UpdateLayerUI()
    {
        if (txtLayerDisplay != null)
        {
            txtLayerDisplay.text = palletCalc.targetLayers.ToString();
        }

        if (txtMaxInfoDisplay != null)
        {
            int max = palletCalc.GetMaxSafeLayers();
            txtMaxInfoDisplay.text = $"Max Safe Layers: {max}";
        }
        if (txtSafeHeightDisplay != null)
        {
            float heightInCm = palletCalc.safeHeight * 100f;
            txtSafeHeightDisplay.text = $"Safe Height: {heightInCm:F0} CM";
        }
    }





    // ==========================================
    // 方向切换逻辑
    // ==========================================
    void OnOrientationSelected(bool isRotate90)
    {
        if (hasStarted) return;

        SetBtnColor(btnDefault, !isRotate90);
        SetBtnColor(btnRotate, isRotate90);

        if (isRotate90) palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_X;
        else palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_Z_Rotated;

        boxFeeder.RefreshCapacity();
    }

    // ==========================================
    // 尺寸切换逻辑
    // ==========================================
    void OnBoxSelected(string type)
    {
        if (hasStarted) return;

        // A. 视觉反馈
        SetBtnColor(btnL, type == "L");
        SetBtnColor(btnM, type == "M");
        SetBtnColor(btnS, type == "S");

        // B. 切换箱子类型 (BoxFeeder 会去同步尺寸给 Pallet)
        boxFeeder.SwitchBoxType(type);

        // C. 切换箱子后，重置层数为最大值
        int maxLayers = palletCalc.GetMaxSafeLayers();
        palletCalc.targetLayers = maxLayers;

        // D. 更新层数显示 & 再次刷新容量 
        UpdateLayerUI();
        boxFeeder.RefreshCapacity();
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