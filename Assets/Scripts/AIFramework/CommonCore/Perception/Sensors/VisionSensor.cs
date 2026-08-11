using System.Collections.Generic;
using UnityEngine;

namespace CommonCore
{
    public class VisionSensor : SensorBase
    {
        protected class VisionQuery
        {
            public IPerceptionListener Listener;
            public IPerceivable Perceivable;
        }

        protected List<IPerceptionListener> Listeners = new();
        protected List<IPerceivable> Perceivables = new();
        protected HashSet<VisionQuery> Queries = new();
        protected Dictionary<IPerceptionListener, SensorConfigBase> ListenerConfigs = new();

        protected virtual bool IsQueryPotentiallyRelevant(IPerceptionListener InListener, IPerceivable InPerceivable)
        {
            if (InListener.Owner == InPerceivable.Owner)
                return false;

            return true;
        }

        public override void RegisterListener(IPerceptionListener InListener, SensorConfigBase InSensorConfig)
        {
            base.RegisterListener(InListener, InSensorConfig);

            if (!Listeners.Contains(InListener))
            {
                Listeners.Add(InListener);
                ListenerConfigs[InListener] = InSensorConfig;

                foreach (var Perceivable in Perceivables)
                {
                    if (!IsQueryPotentiallyRelevant(InListener, Perceivable))
                        continue;

                    Queries.Add(new VisionQuery() { Listener = InListener, Perceivable = Perceivable });
                }
            }
        }

        public override void DeregisterListener(IPerceptionListener InListener)
        {
            base.DeregisterListener(InListener);

            Listeners.Remove(InListener);
            Queries.RemoveWhere((VisionQuery Query) => Query.Listener == InListener);
            ListenerConfigs.Remove(InListener);
        }

        public override void RegisterPerceivable(IPerceivable InPerceivable)
        {
            base.RegisterPerceivable(InPerceivable);

            if (!Perceivables.Contains(InPerceivable))
            {
                Perceivables.Add(InPerceivable);

                // update the vision queries
                foreach (var Listener in Listeners)
                {
                    if (!IsQueryPotentiallyRelevant(Listener, InPerceivable))
                        continue;

                    Queries.Add(new VisionQuery() { Listener = Listener, Perceivable = InPerceivable });
                }
            }
        }

        public override void DeregisterPerceivable(IPerceivable InPerceivable)
        {
            base.DeregisterPerceivable(InPerceivable);

            Perceivables.Remove(InPerceivable);
            Queries.RemoveWhere((VisionQuery Query) => Query.Perceivable == InPerceivable);
        }

        public override void Tick(float InDeltaTime, List<ListenerEntry> InListenerEntries, List<IPerceivable> InPerceivables, IPerceptionManager InManager)
        {
            base.Tick(InDeltaTime, InListenerEntries, InPerceivables, InManager);

            foreach (var Query in Queries)
            {
                var Listener = Query.Listener;
                var Perceivable = Query.Perceivable;

                if (!Listener.CanDetect(Perceivable))
                    continue;

                var Config = ListenerConfigs[Listener] as VisionSensorConfig;

                float Strength = float.MinValue;
                bool queryResult = RunQuery(InDeltaTime, Listener, Perceivable, Config, out Strength);

                if (queryResult && (Strength > 0))
                    InManager.ReportDetection(Listener, Perceivable, this, Strength);
            }
        }

        public override bool EvaluateInjectedDetection(IPerceptionListener InListener, SensorConfigBase InSensorConfig, IPerceivable InPerceivable, float InStrength)
        {
            if (!base.EvaluateInjectedDetection(InListener, InSensorConfig, InPerceivable, InStrength))
                return false;

            float Strength = float.MinValue;
            return RunQuery(Time.deltaTime, InListener, InPerceivable, InSensorConfig as VisionSensorConfig, out Strength) && (Strength > 0);
        }

        protected virtual bool RunQuery(float InDeltaTime, IPerceptionListener InListener, IPerceivable InPerceivable, VisionSensorConfig InConfig, out float Strength)
        {
            var VectorToPerceivable = InPerceivable.Position - InListener.SensorLocation;

            Strength = float.MinValue;

            // are we out of range?
            if (VectorToPerceivable.sqrMagnitude > (InConfig.VisionConeRange * InConfig.VisionConeRange))
            {
                Debug.LogWarning($"[VisionQuery] RANGE FAIL dist={VectorToPerceivable.magnitude:F1} max={InConfig.VisionConeRange} listener={InListener.Owner.name} perceivable={InPerceivable.Owner.name}");
                return false;
            }

            VectorToPerceivable.Normalize();

            // are we out of the vision cone?
            float DotProduct = Vector3.Dot(VectorToPerceivable, InListener.SensorFacing);
            if (DotProduct < InConfig.CosVisionConeAngle)
            {
                Debug.LogWarning($"[VisionQuery] CONE FAIL dot={DotProduct:F2} minDot={InConfig.CosVisionConeAngle:F2} listener={InListener.Owner.name} perceivable={InPerceivable.Owner.name}");
                return false;
            }

            // raycast to the target
            RaycastHit HitResult;
            if (Physics.Raycast(InListener.SensorLocation, VectorToPerceivable, out HitResult,
                                InConfig.VisionConeRange, InConfig.LayersToCheck, QueryTriggerInteraction.Ignore))
            {
                if (HitResult.collider.gameObject == InPerceivable.Owner)
                {
                    Strength = InConfig.Sensitivity.Evaluate(DotProduct) * InConfig.DetectionMultiplier * InDeltaTime;

                    return true;
                }

                Debug.LogWarning($"[VisionRaycast] WRONG_GO hit={HitResult.collider.gameObject.name} (layer={HitResult.collider.gameObject.layer}) " +
                                 $"expected={InPerceivable.Owner.name} listener={InListener.Owner.name} " +
                                 $"listenerPos={InListener.SensorLocation} perceivablePos={InPerceivable.Position} hitPoint={HitResult.point}", HitResult.collider.gameObject);
            }
            else
            {
                Debug.LogWarning($"[VisionRaycast] MISS — no collider hit between {InListener.Owner.name} and {InPerceivable.Owner.name}");
            }

            return false;
        }
    }
}