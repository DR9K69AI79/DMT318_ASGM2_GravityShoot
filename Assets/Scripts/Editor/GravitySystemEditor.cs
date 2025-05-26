using UnityEngine;
using UnityEditor;

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
