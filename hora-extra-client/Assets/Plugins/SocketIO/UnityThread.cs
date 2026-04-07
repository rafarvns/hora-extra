using System;
using System.Collections.Concurrent;
using UnityEngine;

public class UnityThread : MonoBehaviour
{
    private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
    private static UnityThread _instance;

    public static void Execute(Action action) => _queue.Enqueue(action);

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        while (_queue.TryDequeue(out var action)) action?.Invoke();
    }

    // Helper to ensure UnityThread exists in the scene
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        if (_instance == null)
        {
            var go = new GameObject("UnityThreadExecutor");
            _instance = go.AddComponent<UnityThread>();
        }
    }
}
