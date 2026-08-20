using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CommonCore
{
    [AddComponentMenu("AI/Perception/Perception Listener")]
    public class PerceptionListener : MonoBehaviour, IPerceptionListener
    {
        [SerializeField] protected Transform SensorOrigin;
        [SerializeField] protected SensorConfigBase[] SupportedSensors;
        [SerializeField] protected List<EFactionRelationship> SupportedRelationships;

        [SerializeField, Range(0.0f, 1.0f)] protected float AcquisitionThreshold = 0.6f;
        [SerializeField, Range(0.0f, 1.0f)] protected float LossThreshold = 0.3f;

        [SerializeField] protected bool UpdateBlackboard = true;
        [SerializeField] protected bool SendEvents = false;
        [SerializeField] protected UnityEvent<IPerceivable, float> OnFocusChanged = new();

        public Vector3 SensorLocation => SensorOrigin.position;

        public Vector3 SensorFacing => SensorOrigin.forward;

        public GameObject Owner => gameObject;

        public IFaction Faction { get; protected set; }

        protected IPerceptionManager LinkedPerceptionManager = null;
        protected Blackboard<FastName> LinkedBlackboard = null;
        // Tracks whether we are CURRENTLY registered (not just ever). In multi-scene the
        // PerceptionManager already exists, so AsyncLocateService resolves synchronously inside
        // Awake — meaning OnEnable then fires in the same Instantiate() with the manager already
        // resolved. Guarding on "currently registered" makes Awake + OnEnable idempotent and
        // also handles pool reuse (OnDisable clears it, OnEnable re-registers).
        private bool _isRegistered = false;

        protected IPerceivable PreviousBestPerceivable = null;
        protected float PreviousBestDetection = float.MinValue;
        protected IPerceivable CurrentBestPerceivable = null;
        protected float CurrentBestDetection = float.MinValue;

        /// <summary>Dev toggle (Editor/Dev builds): logs target-focus changes AND rejected re-targets with each
        /// candidate's detection strength + distance, so you can see WHY an enemy attacked a given twin. Note:
        /// targeting is STRENGTH-based with hysteresis (a candidate must beat the current best AND clear
        /// AcquisitionThreshold to steal focus) — NOT raw distance, which is why closer ≠ always chosen.</summary>
        public static bool DebugTargeting = false;   // flip true to diagnose targeting live
        private float _lastRejectLog;

        protected void Awake()
        {
            ServiceLocator.AsyncLocateService<IFaction>((ILocatableService InService) =>
            {
                Faction = InService as IFaction;
            }, gameObject, EServiceSearchMode.LocalOnly);

            ServiceLocator.AsyncLocateService<IPerceptionManager>((ILocatableService InService) =>
            {
                LinkedPerceptionManager = InService as IPerceptionManager;
                RegisterAllSensors();
            });

            if (UpdateBlackboard)
            {
                ServiceLocator.AsyncLocateService<Blackboard<FastName>>((ILocatableService InService) =>
                {
                    LinkedBlackboard = InService as Blackboard<FastName>;

                    LinkedBlackboard.Set(CommonCore.Names.Awareness_PreviousBestTarget, (GameObject)null);
                    LinkedBlackboard.Set(CommonCore.Names.Awareness_BestTarget, (GameObject)null);
                }, gameObject, EServiceSearchMode.LocalOnly);
            }
        }

        protected void OnEnable()
        {
            // On first activation the manager may not be resolved yet — Awake's locate callback
            // owns that registration. On pool reuse the manager is already cached, so re-register here.
            if (LinkedPerceptionManager != null)
                RegisterAllSensors();
        }

        protected void OnDisable()
        {
            LinkedPerceptionManager?.DeregisterListener(this);
            _isRegistered = false;
        }

        protected void OnDestroy()
        {
            if (LinkedPerceptionManager != null)
            {
                LinkedPerceptionManager.DeregisterListener(this);
            }
            _isRegistered = false;
        }

        // Idempotent: registers every supported sensor exactly once per active cycle.
        private void RegisterAllSensors()
        {
            if (_isRegistered || LinkedPerceptionManager == null) return;
            foreach (var Config in SupportedSensors)
                LinkedPerceptionManager.RegisterListener(this, Config);
            _isRegistered = true;
        }

        public bool CanDetect(IPerceivable InPerceivable)
        {
            if (Owner == InPerceivable.Owner)
                return false;

            if ((Faction == null) || (InPerceivable.Faction == null))
            {
                Debug.LogWarning($"[CanDetect] blocked — listenerFaction={Faction?.Definition?.DisplayName ?? "NULL"} " +
                                 $"perceivableFaction={InPerceivable.Faction?.Definition?.DisplayName ?? "NULL"} " +
                                 $"listener={Owner.name} perceivable={InPerceivable.Owner.name}", Owner);
                return false;
            }

            if ((SupportedRelationships == null) || (SupportedRelationships.Count == 0))
                return true;

            var Relationship = Faction.GetRelationshipTo(InPerceivable.Faction);
            return SupportedRelationships.Contains(Relationship);
        }

        public void OnNotifyBestPerceivable(IPerceivable InPerceivable, float InDetectionStrength, float InLastDetectionTime, Vector3 InLastDetectionLocation)
        {
            // scenario 1 - we have a current best and the new perceivable is different
            if ((CurrentBestPerceivable != null) && (CurrentBestPerceivable != InPerceivable))
            {
                CurrentBestDetection = LinkedPerceptionManager.GetDetectionStrength(this, CurrentBestPerceivable);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (DebugTargeting && Time.time - _lastRejectLog > 0.75f &&
                    (CurrentBestDetection > InDetectionStrength || InDetectionStrength < AcquisitionThreshold))
                {
                    _lastRejectLog = Time.time;
                    Vector3 me = SensorLocation;
                    Debug.Log($"[Targeting] {Owner.name} KEEPS {CurrentBestPerceivable.Owner.name} " +
                              $"(str={CurrentBestDetection:F2}, dist={Vector3.Distance(me, CurrentBestPerceivable.Position):F1})  vs  " +
                              $"{InPerceivable.Owner.name} (str={InDetectionStrength:F2}, dist={Vector3.Distance(me, InPerceivable.Position):F1}) " +
                              $"— candidate must beat current str AND clear acq={AcquisitionThreshold:F2} to switch", Owner);
                }
#endif

                // new detection is not stronger than current best
                if (CurrentBestDetection > InDetectionStrength)
                    return;

                // new detection is not strong enough to switch to
                if (InDetectionStrength < AcquisitionThreshold)
                    return;

                OnNotifyFocusChanged(InPerceivable, InDetectionStrength);
            } // scenario 2 - current best is the same
            else if (CurrentBestPerceivable == InPerceivable)
            {
                CurrentBestDetection = InDetectionStrength;

                // are we above the loss threshold?
                if (CurrentBestDetection >= LossThreshold)
                    return;

                OnNotifyFocusChanged(null, float.MinValue);
            } // scenario 3 - no current target
            else if (CurrentBestPerceivable == null)
            {
                // are we above the acquisition threshold
                if (InDetectionStrength < AcquisitionThreshold)
                    return;

                OnNotifyFocusChanged(InPerceivable, InDetectionStrength);
            }
        }

        public void OnNotifyLostPerceivable(IPerceivable InPerceivable)
        {
            if (InPerceivable == CurrentBestPerceivable)
                OnNotifyFocusChanged(null, float.MinValue);
        }

        protected void OnNotifyFocusChanged(IPerceivable InPerceivable, float InDetectionStrength)
        {
            PreviousBestPerceivable = CurrentBestPerceivable;
            PreviousBestDetection = CurrentBestDetection;

            CurrentBestPerceivable = InPerceivable;
            CurrentBestDetection = InDetectionStrength;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DebugTargeting)
            {
                Vector3 me = SensorLocation;
                string now = CurrentBestPerceivable != null
                    ? $"{CurrentBestPerceivable.Owner.name} (str={CurrentBestDetection:F2}, dist={Vector3.Distance(me, CurrentBestPerceivable.Position):F1})"
                    : "none";
                string was = PreviousBestPerceivable != null
                    ? $"{PreviousBestPerceivable.Owner.name} (str={PreviousBestDetection:F2}, dist={Vector3.Distance(me, PreviousBestPerceivable.Position):F1})"
                    : "none";
                Debug.Log($"[Targeting] {Owner.name} SWITCH: {was} → {now}", Owner);
            }
#endif

            if (UpdateBlackboard && (LinkedBlackboard != null))
            {
                LinkedBlackboard.Set(CommonCore.Names.Awareness_PreviousBestTarget,
                                     PreviousBestPerceivable != null ? PreviousBestPerceivable.Owner : (GameObject)null);

                LinkedBlackboard.Set(CommonCore.Names.Awareness_BestTarget,
                                     CurrentBestPerceivable != null ? CurrentBestPerceivable.Owner : (GameObject)null);
            }

            if (SendEvents)
                OnFocusChanged.Invoke(CurrentBestPerceivable, CurrentBestDetection);
        }
    }
}