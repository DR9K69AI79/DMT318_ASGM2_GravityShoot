using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 重力系统测试管理器，用于验证和调试重力系统
/// </summary>
public class GravityTestManager : MonoBehaviour
{
    [Header("测试设置")]
    [SerializeField] private bool _showDebugInfo = true;
    [SerializeField] private bool _showGravityVectors = false;
    [SerializeField] private float _vectorScale = 1f;
    
    [Header("UI调试")]
    [SerializeField] private Text _debugText;
    [SerializeField] private bool _enableDebugUI = true;
    
    private RBPlayerMotor _playerMotor;
    private Transform _playerTransform;
    
    private void Start()
    {
        // 查找玩家
        _playerMotor = FindObjectOfType<RBPlayerMotor>();
        if (_playerMotor != null)
        {
            _playerTransform = _playerMotor.transform;
        }
        
        // 创建调试UI
        if (_enableDebugUI && _debugText == null)
        {
            CreateDebugUI();
        }
    }
    
    private void Update()
    {
        if (_showDebugInfo)
        {
            UpdateDebugInfo();
        }
        
        // 快捷键
        HandleInput();
    }
    
    private void UpdateDebugInfo()
    {
        if (_playerMotor == null || _debugText == null) return;
        
        Vector3 playerPos = _playerTransform.position;
        Vector3 gravity = CustomGravity.GetGravity(playerPos, out Vector3 upAxis);
        Vector3 velocity = _playerMotor.Velocity;
        
        string debugInfo = $"=== 重力系统调试信息 ===\n";
        debugInfo += $"玩家位置: {playerPos:F1}\n";
        debugInfo += $"重力加速度: {gravity:F2} ({gravity.magnitude:F2} m/s²)\n";
        debugInfo += $"上轴方向: {upAxis:F2}\n";
        debugInfo += $"玩家速度: {velocity:F2} ({velocity.magnitude:F2} m/s)\n";
        debugInfo += $"是否着地: {_playerMotor.IsGrounded}\n";
        debugInfo += $"重力源数量: {CustomGravity.SourceCount}\n";
        debugInfo += $"\n=== 控制说明 ===\n";
        debugInfo += $"WASD: 移动\n";
        debugInfo += $"空格: 跳跃\n";
        debugInfo += $"鼠标: 视角\n";
        debugInfo += $"G: 切换重力矢量显示\n";
        debugInfo += $"R: 重置玩家位置\n";
        
        _debugText.text = debugInfo;
    }
    
    private void HandleInput()
    {
        // G键切换重力矢量显示
        if (Input.GetKeyDown(KeyCode.G))
        {
            _showGravityVectors = !_showGravityVectors;
            Debug.Log($"重力矢量显示: {(_showGravityVectors ? "开启" : "关闭")}");
        }
        
        // R键重置玩家位置
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetPlayerPosition();
        }
        
        // T键切换调试信息
        if (Input.GetKeyDown(KeyCode.T))
        {
            _showDebugInfo = !_showDebugInfo;
            if (_debugText != null)
                _debugText.gameObject.SetActive(_showDebugInfo);
        }
    }
    
    private void ResetPlayerPosition()
    {
        if (_playerTransform != null)
        {
            _playerTransform.position = new Vector3(0, 15, 0);
            Debug.Log("玩家位置已重置");
        }
    }
    
    private void CreateDebugUI()
    {
        // 创建Canvas
        GameObject canvasGO = new GameObject("DebugCanvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        // 创建调试文本
        GameObject textGO = new GameObject("DebugText");
        textGO.transform.SetParent(canvasGO.transform);
        
        RectTransform rectTransform = textGO.AddComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0, 1);
        rectTransform.anchorMax = new Vector2(0, 1);
        rectTransform.pivot = new Vector2(0, 1);
        rectTransform.anchoredPosition = new Vector2(20, -20);
        rectTransform.sizeDelta = new Vector2(400, 300);
        
        _debugText = textGO.AddComponent<Text>();
        _debugText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _debugText.fontSize = 14;
        _debugText.color = Color.white;
        _debugText.text = "重力系统调试信息";
        
        // 添加背景
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(textGO.transform);
        backgroundGO.transform.SetAsFirstSibling();
        
        RectTransform bgRect = backgroundGO.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = new Vector2(-10, -10);
        bgRect.offsetMax = new Vector2(10, 10);
        
        Image background = backgroundGO.AddComponent<Image>();
        background.color = new Color(0, 0, 0, 0.7f);
    }
    
    private void OnDrawGizmos()
    {
        if (!_showGravityVectors || _playerTransform == null) return;
        
        Vector3 playerPos = _playerTransform.position;
        Vector3 gravity = CustomGravity.GetGravity(playerPos);
        
        if (gravity.magnitude > 0.01f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(playerPos, playerPos + gravity * _vectorScale);
            
            // 绘制箭头
            Vector3 arrowHead = playerPos + gravity * _vectorScale;
            Vector3 arrowDir = gravity.normalized;
            Vector3 right = Vector3.Cross(arrowDir, Vector3.up).normalized;
            if (right.magnitude < 0.1f)
                right = Vector3.Cross(arrowDir, Vector3.forward).normalized;
            
            Vector3 up = Vector3.Cross(right, arrowDir).normalized;
            
            Gizmos.DrawLine(arrowHead, arrowHead - arrowDir * 0.5f + right * 0.2f);
            Gizmos.DrawLine(arrowHead, arrowHead - arrowDir * 0.5f - right * 0.2f);
            Gizmos.DrawLine(arrowHead, arrowHead - arrowDir * 0.5f + up * 0.2f);
            Gizmos.DrawLine(arrowHead, arrowHead - arrowDir * 0.5f - up * 0.2f);
        }
        
        // 绘制上轴
        Vector3 upAxis = CustomGravity.GetUpAxis(playerPos);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(playerPos, playerPos + upAxis * 2f);
    }
    
    // 公共方法供外部调用
    public void ToggleDebugInfo()
    {
        _showDebugInfo = !_showDebugInfo;
        if (_debugText != null)
            _debugText.gameObject.SetActive(_showDebugInfo);
    }
    
    public void ToggleGravityVectors()
    {
        _showGravityVectors = !_showGravityVectors;
    }
    
    public void SetVectorScale(float scale)
    {
        _vectorScale = Mathf.Max(0.1f, scale);
    }
}
