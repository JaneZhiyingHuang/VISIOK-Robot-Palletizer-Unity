using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    // ==========================================
    // 1. 定义状态枚举 
    // ==========================================
    public enum ProgramState
    {
        IDLE,       
        WORKING,    
        COMPLETED,   
        PAUSED
    }

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

    [Header("--- STATE ---")]
    public Button btnStart;
    public Button btnPause;
    public Button butReset;
    public TMP_Text txtStateDisplay;

    [Header("--- COLOR ---")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.cyan;
    public Color colorIdle = Color.white;
    public Color colorWorking = Color.green;
    public Color colorCompleted = Color.yellow;
    public Color colorPaused = Color.red;

    private bool hasStarted = false;
    private bool isPaused = false;

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

        // 4. state button
        if (btnStart != null) btnStart.onClick.AddListener(OnStartClicked);
        if (btnPause != null) btnPause.onClick.AddListener(OnPauseClicked);

        
        OnBoxSelected("L");
        OnOrientationSelected(false);
        UpdateState(ProgramState.IDLE);
    }


    public void UpdateState(ProgramState newState)
    {
        if (txtStateDisplay == null) return;

        switch (newState)
        {
            case ProgramState.IDLE:
                txtStateDisplay.text = "IDLE";
                txtStateDisplay.color = colorIdle;
                break;

            case ProgramState.WORKING:
                txtStateDisplay.text = "WORKING";
                txtStateDisplay.color = colorWorking;
                break;

            case ProgramState.COMPLETED:
                txtStateDisplay.text = "COMPLETED";
                txtStateDisplay.color = colorCompleted;
                break;

            case ProgramState.PAUSED: // 确保这里有 PAUSED
                txtStateDisplay.text = "PAUSED";
                txtStateDisplay.color = colorPaused;
                break;
        }
    }
    // ==========================================
    // 【修改】Start 按钮逻辑 (兼顾启动和恢复)
    // ==========================================
    void OnStartClicked()
    {
        // 情况 1: 如果是暂停状态，点击 Start 代表 "RESUME" (恢复)
        if (hasStarted && isPaused)
        {
            PerformResume();
            return;
        }

        // 情况 2: 如果还没开始，执行 "START" (启动)
        if (!hasStarted)
        {
            PerformStart();
        }
    }

    // 辅助：执行启动
    void PerformStart()
    {
        hasStarted = true;
        isPaused = false;

        // 启动后，Start 按钮变灰，直到暂停或结束
        btnStart.interactable = false;

        UpdateState(ProgramState.WORKING);

        boxFeeder.StartSpawning();
        autoManager.BeginWork();
    }

    // 辅助：执行恢复
    void PerformResume()
    {
        isPaused = false;

        // 恢复后，Start 按钮再次变灰
        btnStart.interactable = false;

        // 通知机械臂继续
        if (autoManager != null) autoManager.SetPaused(false);

        UpdateState(ProgramState.WORKING);
    }

    // ==========================================
    // Pause 按钮逻辑 (只负责暂停)
    // ==========================================
    void OnPauseClicked()
    {
        // 1. 如果还没开始，或者已经结束，或者已经是暂停状态，就不处理
        if (!hasStarted) return;
        if (isPaused) return; // 如果已经暂停了，点暂停没反应（要点 Start 恢复）
        if (txtStateDisplay.text == "COMPLETED") return;

        // 2. 执行暂停
        isPaused = true;

        // 3. 通知 AutoManager
        if (autoManager != null) autoManager.SetPaused(true);

        // 4. 更新 UI 为 PAUSED
        UpdateState(ProgramState.PAUSED);

        // 5. 重新激活 Start 按钮，让用户可以点击它来恢复
        btnStart.interactable = true;
    }


    // ==========================================
    // 层数加减逻辑
    // ==========================================
    void ChangeLayerCount(int change)
    {
        if (hasStarted && !isPaused) return;
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

    // ==========================================
    // 让外部脚本通知任务完成
    // ==========================================
    public void NotifyJobFinished()
    {
        UpdateState(ProgramState.COMPLETED);
        Debug.Log("🎉 任务完成！");
    }
}