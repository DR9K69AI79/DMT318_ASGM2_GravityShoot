using UnityEngine;

namespace DWHITE {	
	/// <summary>
	/// 泛型单例基类，提供线程安全的惰性初始化
	/// </summary>
	public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
	{
	    private static T _instance;
	    private static readonly object _lock = new object();
	    
	    public static T Instance
	    {
	        get
	        {
	            if (_instance == null)
	            {
	                lock (_lock)
	                {
	                    if (_instance == null)
	                    {
	                        _instance = FindObjectOfType<T>();
	                        
	                        if (_instance == null)
	                        {
	                            GameObject go = new GameObject(typeof(T).Name);
	                            _instance = go.AddComponent<T>();
	                        }
	                        
	                        DontDestroyOnLoad(_instance.gameObject);
	                    }
	                }
	            }
	            return _instance;
	        }
	    }
	    
	    protected virtual void Awake()
	    {
	        if (_instance == null)
	        {
	            _instance = this as T;
	            DontDestroyOnLoad(gameObject);
	        }
	        else if (_instance != this)
	        {
	            Debug.LogWarning($"Multiple instances of {typeof(T).Name} detected. Destroying duplicate.");
	            Destroy(gameObject);
	        }
	    }
	}
}
