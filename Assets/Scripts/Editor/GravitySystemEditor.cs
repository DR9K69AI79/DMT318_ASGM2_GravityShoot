using UnityEngine;
using UnityEditor;

namespace DWHITE {	
	/// <summary>
	/// 重力系统的Editor工具，用于快速设置和测试
	/// </summary>
	public class GravitySystemEditor : EditorWindow
	{
	    [MenuItem("Tools/Gravity System/Setup Test Scene")]
	    public static void SetupTestScene()
	    {
	        // 创建玩家
	        CreatePlayer();
	        
	        // 创建重力源
	        CreateGravitySphere("Planet_Main", Vector3.zero, 10f, 15f);
	        CreateGravitySphere("Planet_Small", new Vector3(25f, 0f, 0f), 5f, 8f);
	        CreateGravityPlane("Ground_Plane", new Vector3(0f, -20f, 0f), 9.8f, 15f);
	        
	        // 创建一些基础几何体作为参考
	        CreateReference();
	        
	        Debug.Log("重力系统测试场景设置完成！");
	    }
	
	    [MenuItem("Tools/Gravity System/Create Player")]
	    public static void CreatePlayer()
	    {
	        GameObject playerGO = new GameObject("Player");
	        
	        // 添加Rigidbody（新的物理基础）
	        Rigidbody rb = playerGO.AddComponent<Rigidbody>();
	        rb.mass = 1f;
	        rb.drag = 0f;
	        rb.angularDrag = 0f;
	        rb.useGravity = false;
	        rb.freezeRotation = true;
	        rb.interpolation = RigidbodyInterpolation.Interpolate;
	        
	        // 添加碰撞体
	        CapsuleCollider collider = playerGO.AddComponent<CapsuleCollider>();
	        collider.center = new Vector3(0, 1, 0);
	        collider.height = 2f;
	        collider.radius = 0.3f;
	        
	        // 添加输入控制
	        playerGO.AddComponent<PlayerInput>();
	        
	        // 添加新的运动控制
	        RBPlayerMotor motor = playerGO.AddComponent<RBPlayerMotor>();
	        
	        // 添加新的相机控制
	        FPSGravityCamera camera = playerGO.AddComponent<FPSGravityCamera>();
	        
	        // 尝试加载默认调参配置
	        MovementTuningSO defaultTuning = Resources.Load<MovementTuningSO>("MovementTuning");
	        if (defaultTuning != null)
	        {
	            SerializedObject serializedMotor = new SerializedObject(motor);
	            serializedMotor.FindProperty("_tuning").objectReferenceValue = defaultTuning;
	            serializedMotor.ApplyModifiedProperties();
	        }
	        
	        // 设置玩家位置
	        playerGO.transform.position = new Vector3(0, 15, 0);
	        
	        // 选中玩家
	        Selection.activeGameObject = playerGO;
	        
	        Debug.Log("Rigidbody玩家创建完成！使用了新的RBPlayerMotor和FPSGravityCamera");
	    }
	    
	    [MenuItem("Tools/Gravity System/Create Gravity Sphere")]
	    public static void CreateGravitySphere()
	    {
	        CreateGravitySphere("GravitySphere", Vector3.zero, 9.8f, 10f);
	    }
	    
	    public static GameObject CreateGravitySphere(string name, Vector3 position, float gravity, float radius)
	    {
	        GameObject go = new GameObject(name);
	        go.transform.position = position;
	        
	        // 添加重力组件
	        GravitySphere gravitySphere = go.AddComponent<GravitySphere>();
	        
	        // 设置参数
	        SerializedObject serializedGravity = new SerializedObject(gravitySphere);
	        serializedGravity.FindProperty("_gravity").floatValue = gravity;
	        serializedGravity.FindProperty("_radius").floatValue = radius;
	        serializedGravity.ApplyModifiedProperties();
	        
	        // 添加视觉表示
	        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
	        visual.name = "Visual";
	        visual.transform.SetParent(go.transform);
	        visual.transform.localPosition = Vector3.zero;
	        visual.transform.localScale = Vector3.one * radius * 2f;
	        
	        // 移除碰撞体（仅作为视觉参考）
	        if (visual.GetComponent<Collider>())
	            DestroyImmediate(visual.GetComponent<Collider>());
	            
	        return go;
	    }
	    
	    [MenuItem("Tools/Gravity System/Create Gravity Plane")]
	    public static void CreateGravityPlane()
	    {
	        CreateGravityPlane("GravityPlane", Vector3.zero, 9.8f, 10f);
	    }
	    
	    public static GameObject CreateGravityPlane(string name, Vector3 position, float gravity, float range)
	    {
	        GameObject go = new GameObject(name);
	        go.transform.position = position;
	        
	        // 添加重力组件
	        GravityPlane gravityPlane = go.AddComponent<GravityPlane>();
	        
	        // 设置参数
	        SerializedObject serializedGravity = new SerializedObject(gravityPlane);
	        serializedGravity.FindProperty("_gravity").floatValue = gravity;
	        serializedGravity.FindProperty("_range").floatValue = range;
	        serializedGravity.ApplyModifiedProperties();
	        
	        // 添加视觉表示
	        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Plane);
	        visual.name = "Visual";
	        visual.transform.SetParent(go.transform);
	        visual.transform.localPosition = Vector3.zero;
	        visual.transform.localScale = Vector3.one * 2f;
	        
	        return go;
	    }
	    
	    [MenuItem("Tools/Gravity System/Create Gravity Box")]
	    public static void CreateGravityBox()
	    {
	        GameObject go = new GameObject("GravityBox");
	        
	        // 添加重力组件
	        GravityBox gravityBox = go.AddComponent<GravityBox>();
	        
	        // 添加视觉表示
	        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
	        visual.name = "Visual";
	        visual.transform.SetParent(go.transform);
	        visual.transform.localPosition = Vector3.zero;
	        
	        Selection.activeGameObject = go;
	    }
	    
	    private static void CreateReference()
	    {
	        // 创建一些参考立方体
	        for (int i = 0; i < 3; i++)
	        {
	            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
	            cube.name = $"Reference_Cube_{i}";
	            cube.transform.position = new Vector3(i * 3f, 12f, 0f);
	            cube.GetComponent<Renderer>().material.color = Color.red;
	        }
	    }
	}
	
	/// <summary>
	/// 为重力源组件创建自定义Inspector
	/// </summary>
	[CustomEditor(typeof(GravitySphere))]
	public class GravitySphereEditor : Editor
	{
	    public override void OnInspectorGUI()
	    {
	        DrawDefaultInspector();
	        
	        GravitySphere sphere = (GravitySphere)target;
	        
	        EditorGUILayout.Space();
	        EditorGUILayout.LabelField("重力信息", EditorStyles.boldLabel);
	        
	        Vector3 testPos = sphere.transform.position + Vector3.up * sphere.Radius * 0.5f;
	        Vector3 gravity = sphere.GetGravity(testPos);
	        EditorGUILayout.LabelField($"测试重力: {gravity.magnitude:F2} m/s²");
	        EditorGUILayout.LabelField($"重力方向: {gravity.normalized}");
	        
	        if (GUILayout.Button("测试重力场"))
	        {
	            Debug.Log($"重力球 {sphere.name} - 半径: {sphere.Radius}m, 重力: {sphere.Gravity} m/s²");
	        }
	    }
	}
	
	[CustomEditor(typeof(GravityPlane))]
	public class GravityPlaneEditor : Editor
	{
	    public override void OnInspectorGUI()
	    {
	        DrawDefaultInspector();
	        
	        GravityPlane plane = (GravityPlane)target;
	        
	        EditorGUILayout.Space();
	        EditorGUILayout.LabelField("重力信息", EditorStyles.boldLabel);
	        EditorGUILayout.LabelField($"重力强度: {plane.Gravity} m/s²");
	        EditorGUILayout.LabelField($"影响范围: {plane.Range} m");
	        EditorGUILayout.LabelField($"重力方向: {-plane.transform.up}");
	    }
	}
	
	[CustomEditor(typeof(GravityBox))]
	public class GravityBoxEditor : Editor
	{
	    public override void OnInspectorGUI()
	    {
	        DrawDefaultInspector();
	        
	        GravityBox box = (GravityBox)target;
	        
	        EditorGUILayout.Space();
	        EditorGUILayout.LabelField("重力信息", EditorStyles.boldLabel);
	        EditorGUILayout.LabelField($"重力向量: {box.Gravity}");
	        EditorGUILayout.LabelField($"重力强度: {box.Gravity.magnitude:F2} m/s²");
	        EditorGUILayout.LabelField($"盒体大小: {box.Size}");
	    }
	}
	
	/// <summary>
	/// 重力场分析器窗口
	/// </summary>
	public class GravityFieldAnalyzer : EditorWindow
	{
	    private Vector3 _testPosition = Vector3.zero;
	    private float _sampleRadius = 20f;
	    private int _sampleCount = 100;
	    private bool _showVisualization = true;
	    
	    private void OnGUI()
	    {
	        GUILayout.Label("重力场分析器", EditorStyles.boldLabel);
	        EditorGUILayout.Space();
	        
	        _testPosition = EditorGUILayout.Vector3Field("测试位置", _testPosition);
	        _sampleRadius = EditorGUILayout.FloatField("采样半径", _sampleRadius);
	        _sampleCount = EditorGUILayout.IntField("采样数量", _sampleCount);
	        _showVisualization = EditorGUILayout.Toggle("显示可视化", _showVisualization);
	        
	        EditorGUILayout.Space();
	        
	        if (GUILayout.Button("分析当前位置重力"))
	        {
	            AnalyzeGravityAtPosition();
	        }
	        
	        if (GUILayout.Button("生成重力场报告"))
	        {
	            GenerateGravityFieldReport();
	        }
	        
	        EditorGUILayout.Space();
	        EditorGUILayout.HelpBox("此工具可以帮助分析场景中的重力场分布，优化重力源配置。", MessageType.Info);
	    }
	    
	    private void AnalyzeGravityAtPosition()
	    {
	        Vector3 gravity = CustomGravity.GetGravity(_testPosition, out Vector3 upAxis);
	        Vector3 strongestSourceUp = CustomGravity.GetUpAxis(_testPosition);
	        
	        Debug.Log($"=== 重力场分析结果 ===");
	        Debug.Log($"测试位置: {_testPosition}");
	        Debug.Log($"重力加速度: {gravity} (强度: {gravity.magnitude:F2} m/s²)");
	        Debug.Log($"上轴方向: {upAxis}");
	        Debug.Log($"最强重力源上轴: {strongestSourceUp}");
	        Debug.Log($"重力源数量: {CustomGravity.SourceCount}");
	        
	        EditorUtility.DisplayDialog("分析完成", 
	            $"重力强度: {gravity.magnitude:F2} m/s²\n" +
	            $"重力方向: {gravity.normalized}\n" +
	            $"上轴方向: {upAxis}\n\n" +
	            "详细信息请查看 Console 窗口", "确定");
	    }
	    
	    private void GenerateGravityFieldReport()
	    {
	        var report = new System.Text.StringBuilder();
        report.AppendLine("# 重力场分析报告\n");
	        
	        // 采样重力场
	        float maxGravity = 0f;
	        float minGravity = float.MaxValue;
	        float avgGravity = 0f;
	        int validSamples = 0;
	        
	        for (int i = 0; i < _sampleCount; i++)
	        {
	            Vector3 randomOffset = UnityEngine.Random.insideUnitSphere * _sampleRadius;
	            Vector3 samplePos = _testPosition + randomOffset;
	            
	            Vector3 gravity = CustomGravity.GetGravity(samplePos);
	            float gravityMagnitude = gravity.magnitude;
	            
	            if (gravityMagnitude > 0.001f)
	            {
	                maxGravity = Mathf.Max(maxGravity, gravityMagnitude);
	                minGravity = Mathf.Min(minGravity, gravityMagnitude);
	                avgGravity += gravityMagnitude;
	                validSamples++;
	            }
	        }
	        
	        if (validSamples > 0)
	        {
	            avgGravity /= validSamples;
	        }
	        
        report.AppendLine($"## 采样区域: 半径 {_sampleRadius}m，中心 {_testPosition}");
	        report.AppendLine($"- 有效采样数: {validSamples}/{_sampleCount}");
	        report.AppendLine($"- 最大重力强度: {maxGravity:F2} m/s²");
	        report.AppendLine($"- 最小重力强度: {minGravity:F2} m/s²");
	        report.AppendLine($"- 平均重力强度: {avgGravity:F2} m/s²");
	        report.AppendLine($"- 重力源总数: {CustomGravity.SourceCount}");
	        
	        Debug.Log(report.ToString());
	        
	        // 保存报告到文件
	        string fileName = $"GravityFieldReport_{System.DateTime.Now:yyyyMMdd_HHmmss}.md";
	        string filePath = System.IO.Path.Combine(Application.dataPath, fileName);
	        System.IO.File.WriteAllText(filePath, report.ToString());
	        
	        EditorUtility.DisplayDialog("报告生成完成", 
	            $"重力场报告已保存到:\n{filePath}\n\n" +
	            "同时输出到 Console 窗口", "确定");
	    }
	}
	
	/// <summary>
	/// 重力系统配置向导
	/// </summary>
	public class GravityConfigurationWizard : EditorWindow
	{
	    private enum ConfigurationType
	    {
	        Basic,          // 基础重力场
	        Planetary,      // 行星重力场
	        AntiGravity,    // 反重力区域
	        Complex         // 复杂重力场
	    }
	    
	    private ConfigurationType _configurationType = ConfigurationType.Basic;
	    private Vector3 _centerPosition = Vector3.zero;
	    private float _gravityStrength = 9.8f;
	    private float _fieldSize = 20f;
	    
	    private void OnGUI()
	    {
	        GUILayout.Label("重力系统配置向导", EditorStyles.boldLabel);
	        EditorGUILayout.Space();
	        
	        EditorGUILayout.LabelField("选择重力场类型:", EditorStyles.boldLabel);
	        _configurationType = (ConfigurationType)EditorGUILayout.EnumPopup("配置类型", _configurationType);
	        
	        EditorGUILayout.Space();
	        
	        _centerPosition = EditorGUILayout.Vector3Field("中心位置", _centerPosition);
	        _gravityStrength = EditorGUILayout.FloatField("重力强度", _gravityStrength);
	        _fieldSize = EditorGUILayout.FloatField("场地大小", _fieldSize);
	        
	        EditorGUILayout.Space();
	        
	        // 显示配置说明
	        ShowConfigurationDescription();
	        
	        EditorGUILayout.Space();
	        
	        if (GUILayout.Button("创建配置", GUILayout.Height(30)))
	        {
	            CreateConfiguration();
	        }
	        
	        EditorGUILayout.Space();
	        EditorGUILayout.HelpBox("此向导可帮助您快速创建常见的重力场配置。", MessageType.Info);
	    }
	    
	    private void ShowConfigurationDescription()
	    {
	        string description = _configurationType switch
	        {
	            ConfigurationType.Basic => "创建基础向下重力场，适合平台游戏",
	            ConfigurationType.Planetary => "创建球形重力场，模拟行星引力",
	            ConfigurationType.AntiGravity => "创建向上重力场，反重力效果",
	            ConfigurationType.Complex => "创建多重力源复杂场景",
	            _ => "选择一个配置类型"
	        };
	        
	        EditorGUILayout.HelpBox(description, MessageType.Info);
	    }
	    
	    private void CreateConfiguration()
	    {
	        switch (_configurationType)
	        {
	            case ConfigurationType.Basic:
	                CreateBasicConfiguration();
	                break;
	            case ConfigurationType.Planetary:
	                CreatePlanetaryConfiguration();
	                break;
	            case ConfigurationType.AntiGravity:
	                CreateAntiGravityConfiguration();
	                break;
	            case ConfigurationType.Complex:
	                CreateComplexConfiguration();
	                break;
	        }
	        
	        EditorUtility.DisplayDialog("配置完成", $"已创建 {_configurationType} 重力场配置", "确定");
	    }
	    
	    private void CreateBasicConfiguration()
	    {
	        // 创建基础向下重力平面
	        var gravityPlane = GravitySystemEditor.CreateGravityPlane(
	            "Basic_Gravity_Plane", 
	            _centerPosition, 
	            _gravityStrength, 
	            _fieldSize
	        );
	        
	        Selection.activeGameObject = gravityPlane;
	    }
	    
	    private void CreatePlanetaryConfiguration()
	    {
	        // 创建球形重力源
	        var gravitySphere = GravitySystemEditor.CreateGravitySphere(
	            "Planetary_Gravity", 
	            _centerPosition, 
	            _gravityStrength, 
	            _fieldSize
	        );
	        
	        Selection.activeGameObject = gravitySphere;
	    }
	    
	    private void CreateAntiGravityConfiguration()
	    {
	        // 创建向上的重力平面
	        var gravityPlane = GravitySystemEditor.CreateGravityPlane(
	            "AntiGravity_Plane", 
	            _centerPosition, 
	            -_gravityStrength,  // 负重力 = 反重力
	            _fieldSize
	        );
	        
	        // 旋转180度使其向上
	        gravityPlane.transform.rotation = Quaternion.Euler(180, 0, 0);
	        
	        Selection.activeGameObject = gravityPlane;
	    }
	    
	    private void CreateComplexConfiguration()
	    {
	        // 创建多个重力源的复杂配置
	        var parent = new GameObject("Complex_Gravity_System");
	        parent.transform.position = _centerPosition;
	        
	        // 中央主重力源
	        var mainGravity = GravitySystemEditor.CreateGravitySphere(
	            "Main_Gravity", 
	            _centerPosition, 
	            _gravityStrength, 
	            _fieldSize * 0.8f
	        );
	        mainGravity.transform.SetParent(parent.transform);
	        
	        // 周围的小重力源
	        for (int i = 0; i < 4; i++)
	        {
	            float angle = i * 90f * Mathf.Deg2Rad;
	            Vector3 offset = new Vector3(
	                Mathf.Cos(angle) * _fieldSize * 0.7f, 
	                0, 
	                Mathf.Sin(angle) * _fieldSize * 0.7f
	            );
	            
	            var smallGravity = GravitySystemEditor.CreateGravitySphere(
	                $"Small_Gravity_{i}", 
	                _centerPosition + offset, 
	                _gravityStrength * 0.5f, 
	                _fieldSize * 0.3f
	            );
	            smallGravity.transform.SetParent(parent.transform);
	        }
	        
	        Selection.activeGameObject = parent;
	    }
	}
}
