using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif // UNITY_EDITOR

namespace CommonCore
{
    public abstract class MonoBehaviourSingleton<T> : MonoBehaviourSingleton where T : MonoBehaviour
    {
        static T _Instance = null;
        static bool _bInitialising = false;
        // True when _Instance was auto-fabricated (not found in any scene). A scene-resident
        // instance arriving later via ConstructIfNeeded should replace the fabricated one.
        static bool _bWasAutoCreated = false;
        static readonly object _InstanceLock = new object();

        public static T Instance
        {
            get
            {
                lock (_InstanceLock)
                {
                    // do nothing if currently quitting
                    if (bIsQuitting)
                        return null;

                    // instance already found?
                    if (_Instance != null)
                        return _Instance;

                    _bInitialising = true;

                    // search for any in-scene instance of T
                    var AllInstances = FindObjectsByType<T>(FindObjectsSortMode.None);

                    // found exactly one?
                    if (AllInstances.Length == 1)
                    {
                        _Instance = AllInstances[0];
                    } // found none — fabricate with a warning (genuinely lazy AI-framework services
                      // only; gameplay managers placed in Persistent.unity must never reach here)
                    else if (AllInstances.Length == 0)
                    {
                        Debug.LogWarning($"[MonoBehaviourSingleton] No scene instance of {typeof(T)} found — " +
                                         "auto-creating. If this is a gameplay manager, check Persistent.unity setup.");
                        _Instance = new GameObject($"Singleton<{typeof(T)}>").AddComponent<T>();
                        _bWasAutoCreated = true;
                    } // multiple found?
                    else
                    {
                        _Instance = AllInstances[0];

                        // destroy the duplicates
                        for (int Index = 1; Index < AllInstances.Length; ++Index)
                        {
                            Debug.LogError($"Destroying duplicate {typeof(T)} on {AllInstances[0].gameObject.name}");
                            Destroy(AllInstances[Index].gameObject);
                        }
                    }

                    _bInitialising = false;
                    return _Instance;
                }
            }
        }

        static void ConstructIfNeeded(MonoBehaviourSingleton<T> InInstance)
        {
            lock (_InstanceLock)
            {
                if (_Instance == null && !_bInitialising)
                {
                    _Instance = InInstance as T;
                }
                else if (_Instance != null && !_bInitialising)
                {
                    // If the current instance was auto-fabricated and a real scene-resident
                    // instance is now registering, prefer the scene-resident one.
                    if (_bWasAutoCreated)
                    {
                        Destroy(_Instance.gameObject);
                        _Instance = InInstance as T;
                        _bWasAutoCreated = false;
                    }
                    else
                    {
                        Debug.LogError($"Destroying duplicate {typeof(T)} on {InInstance.gameObject.name}");
                        Destroy(InInstance.gameObject);
                    }
                }
            }
        }

        private void Awake()
        {
            ConstructIfNeeded(this);

            OnAwake();
        }

        // Override to true only for AI-framework singletons that are auto-created outside of any
        // scene and must survive Bootstrap reloads. Persistent.unity residents must NOT override
        // this — Persistent is never unloaded, and DDOL causes duplicate managers on Restart.
        protected virtual bool ApplyDontDestroyOnLoad => false;

        protected virtual void OnAwake()
        {
            if (!ApplyDontDestroyOnLoad) return;

            if (transform.parent != null)
            {
#if UNITY_EDITOR
                Debug.LogError($"{gameObject.name} is parented to {transform.parent.gameObject.name}. Unparenting so that it can be marked as DontDestroyOnLoad");
#endif // UNITY_EDITOR
                transform.SetParent(null);
            }

            DontDestroyOnLoad(gameObject);
        }

#if UNITY_EDITOR
        void OnEnable()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnPlayModeChanged(PlayModeStateChange InChange)
        {
            if ((InChange == PlayModeStateChange.ExitingPlayMode) && (_Instance != null))
            {
                bIsQuitting = true;
                DestroyImmediate(gameObject);
            }
        }
#endif // UNITY_EDITOR
    }

    public abstract class MonoBehaviourSingleton : MonoBehaviour
    {
        protected static bool bIsQuitting { get; set; } = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void OnBeforeSceneLoad()
        {
            bIsQuitting = false;
        }

        private void OnApplicationQuit()
        {
            bIsQuitting = true;
        }

        public virtual void OnBootstrapped() { }
    }
}