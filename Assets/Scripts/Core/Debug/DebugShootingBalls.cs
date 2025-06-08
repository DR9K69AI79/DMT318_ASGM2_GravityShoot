using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DWHITE {	
	public class DebugShootingBalls : MonoBehaviour
	{
	    [Header("调试设置")]
	    [SerializeField] private GameObject ballPrefab; // 球体预制件
	    [SerializeField] private GameObject target; // 目标位置（可选）
	    [SerializeField] private bool useTarget = false; // 是否使用目标位置
	    [SerializeField] private float spawnInterval = 0.2f; // 球体生成间隔时间
	    [SerializeField] private int ballCount = 1; // 球体数量
	    //[SerializeField] private float spawnRadius = 5f; // 球体生成半径
	    [SerializeField] private float forceMagnitude = 10f; // 球体施加的力大小
	
	    private Vector3 direction;
	
	    private void Start()
	    {
	        // 确保球体预制件已设置
	        if (ballPrefab == null)
	        {
	            Debug.LogError("请设置球体预制件！");
	            enabled = false; // 禁用脚本
	            return;
	        }
	    }
	
	    private void Update()
	    {
	        if (Time.time % spawnInterval < Time.deltaTime)
	        {
	            SpawnShootingBalls();
	        }
	        
	        direction = useTarget && target != null ? (target.transform.position - transform.position).normalized : transform.forward;
	        if (useTarget && target != null)
	        {
	            transform.LookAt(target.transform.position);
	        }
	        else
	        {
	            transform.rotation = Quaternion.LookRotation(direction);
	        }
	    }
	
	    private void SpawnShootingBalls()
	    {
	        for (int i = 0; i < ballCount; i++)
	        {
	            Vector3 spawnPosition = transform.position;
	            GameObject ball = Instantiate(ballPrefab, spawnPosition, Quaternion.identity);
	            Rigidbody rb = ball.GetComponent<Rigidbody>();
	            if (rb != null) rb.AddForce(direction * forceMagnitude, ForceMode.Impulse);
	        }
	    }
	}
}
