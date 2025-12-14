using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections.Generic; 

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
    public Button btnRestart; 
    public TMP_Text txtStateDisplay;

    [Header("--- LOG ---")] 
    public TMP_Text txtConsoleLog;
    private Queue<string> _logQueue = new Queue<string>();

    [Header("--- COLOR ---")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.cyan;
    public Color colorIdle = Color.white;
    public Color colorWorking = Color.green;
    public Color colorCompleted = Color.yellow;
    public Color colorPaused = Color.red;

    private bool hasStarted = false;
    private bool isPaused = false;

    // ==========================================
    // 注册日志监听
    // ==========================================
    void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (txtConsoleLog == null) return;

        string formattedLog = "> " + logString;

        _logQueue.Enqueue(formattedLog);

        if (_logQueue.Count > 3)
        {
            _logQueue.Dequeue();
        }

        txtConsoleLog.text = string.Join("\n", _logQueue);
    }

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
        if (btnRestart != null) btnRestart.onClick.AddListener(OnRestartClicked);

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

            case ProgramState.PAUSED:
                txtStateDisplay.text = "PAUSED";
                txtStateDisplay.color = colorPaused;
                break;
        }
    }

    void OnStartClicked()
    {
        // 情况 1: 如果是暂停状态，点击 Start 代表 "RESUME" 
        if (hasStarted && isPaused)
        {
            PerformResume();
            return;
        }

        // 情况 2: 如果还没开始，执行 "START" 
        if (!hasStarted)
        {
            PerformStart();
        }
    }

    void PerformStart()
    {
        hasStarted = true;
        isPaused = false;
        btnStart.interactable = false;
        UpdateState(ProgramState.WORKING);
        boxFeeder.StartSpawning();
        autoManager.BeginWork();
    }

    void PerformResume()
    {
        isPaused = false;
        btnStart.interactable = false;
        if (autoManager != null) autoManager.SetPaused(false);
        UpdateState(ProgramState.WORKING);
    }

    void OnPauseClicked()
    {
        if (!hasStarted) return;
        if (isPaused) return;
        if (txtStateDisplay.text == "COMPLETED") return;

        isPaused = true;
        if (autoManager != null) autoManager.SetPaused(true);
        UpdateState(ProgramState.PAUSED);
        btnStart.interactable = true;
    }

    // ==========================================
    // Restart 按钮逻辑 
    // ==========================================
    void OnRestartClicked()
    {
        Debug.Log("🔄 正在重新加载场景...");

        // 获取当前场景的名字
        string currentSceneName = SceneManager.GetActiveScene().name;

        // 重新加载它！
        SceneManager.LoadScene(currentSceneName);
    }

    void ChangeLayerCount(int change)
    {
        // 只有没开始时才允许改层数
        if (hasStarted && !isPaused) return;
        if (hasStarted) return;

        int maxLayers = palletCalc.GetMaxSafeLayers();
        int current = palletCalc.targetLayers;
        current += change;
        current = Mathf.Clamp(current, 1, maxLayers);
        palletCalc.targetLayers = current;
        UpdateLayerUI();
        boxFeeder.RefreshCapacity();
    }

    void UpdateLayerUI()
    {
        if (txtLayerDisplay != null) txtLayerDisplay.text = palletCalc.targetLayers.ToString();
        if (txtMaxInfoDisplay != null) txtMaxInfoDisplay.text = $"Max Safe Layers: {palletCalc.GetMaxSafeLayers()}";
        if (txtSafeHeightDisplay != null)
        {
            float heightInCm = palletCalc.safeHeight * 100f;
            txtSafeHeightDisplay.text = $"Safe Height: {heightInCm:F0} CM";
        }
    }

    void OnOrientationSelected(bool isRotate90)
    {
        if (hasStarted) return;
        SetBtnColor(btnDefault, !isRotate90);
        SetBtnColor(btnRotate, isRotate90);
        if (isRotate90) palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_X;
        else palletCalc.placementOrientation = PalletCalculator.BoxOrientation.Align_Z_Rotated;
        boxFeeder.RefreshCapacity();
    }

    void OnBoxSelected(string type)
    {
        if (hasStarted) return;
        SetBtnColor(btnL, type == "L");
        SetBtnColor(btnM, type == "M");
        SetBtnColor(btnS, type == "S");
        boxFeeder.SwitchBoxType(type);
        int maxLayers = palletCalc.GetMaxSafeLayers();
        palletCalc.targetLayers = maxLayers;
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

    public void NotifyJobFinished()
    {
        UpdateState(ProgramState.COMPLETED);
        Debug.Log("🎉 任务完成！");
    }
}