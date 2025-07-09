// TransitionDebugSystem.cs - CREATE THIS FILE
using UnityEngine;
using System.Collections.Generic;
using System.Text;

namespace Cardini.Motion
{
    /// <summary>
    /// Captures detailed information about why module/state transitions occur
    /// This is a STATIC class - no component needed!
    /// </summary>
    public static class TransitionDebugSystem
    {
        private static List<DetailedTransitionRecord> recentTransitions = new List<DetailedTransitionRecord>();
        private const int MAX_DETAILED_HISTORY = 10;
        
        public static void LogTransition(string fromState, string toState, TransitionType type, 
                                       Dictionary<string, object> conditions, 
                                       List<string> competingModules = null)
        {
            var record = new DetailedTransitionRecord
            {
                fromState = fromState,
                toState = toState,
                type = type,
                timestamp = Time.time,
                frameCount = Time.frameCount,
                conditions = new Dictionary<string, object>(conditions),
                competingModules = competingModules ?? new List<string>(),
                triggerReason = DetermineTriggerReason(conditions)
            };
            
            recentTransitions.Insert(0, record);
            if (recentTransitions.Count > MAX_DETAILED_HISTORY)
            {
                recentTransitions.RemoveAt(recentTransitions.Count - 1);
            }
            
            // Optional: Log to console for immediate debugging
            // if (UnityEngine.Debug.unityLogger.logEnabled)
            // {
            //     Debug.Log($"🔄 {type} Transition: {fromState} → {toState} | {record.triggerReason}");
            // }
        }
        
        public static List<DetailedTransitionRecord> GetRecentTransitions()
        {
            return new List<DetailedTransitionRecord>(recentTransitions);
        }
        
        public static string FormatTransitionDetails(DetailedTransitionRecord record)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"<b>{record.type}: {record.fromState} → {record.toState}</b>");
            sb.AppendLine($"<size=10><color=#B0B0B0>Frame {record.frameCount} | {(Time.time - record.timestamp):F2}s ago</color></size>");
            sb.AppendLine($"<color=#FFD700>Trigger:</color> {record.triggerReason}");
            
            if (record.conditions.Count > 0)
            {
                sb.AppendLine("<color=#98FB98>Conditions:</color>");
                foreach (var condition in record.conditions)
                {
                    string value = FormatConditionValue(condition.Value);
                    string status = GetConditionStatus(condition.Key, condition.Value);
                    sb.AppendLine($"  {status} {condition.Key}: {value}");
                }
            }
            
            if (record.competingModules.Count > 0)
            {
                sb.AppendLine($"<color=#FF6B6B>Competing:</color> {string.Join(", ", record.competingModules)}");
            }
            
            return sb.ToString();
        }
        
        private static string DetermineTriggerReason(Dictionary<string, object> conditions)
        {
            // Smart analysis of conditions to determine primary trigger
            if (conditions.ContainsKey("InputTriggered") && (bool)conditions["InputTriggered"])
                return "Input triggered";
            if (conditions.ContainsKey("LostGround") && (bool)conditions["LostGround"])
                return "Lost ground contact";
            if (conditions.ContainsKey("TimerExpired") && (bool)conditions["TimerExpired"])
                return "Timer expired";
            if (conditions.ContainsKey("SpeedThreshold") && (bool)conditions["SpeedThreshold"])
                return "Speed threshold met";
            if (conditions.ContainsKey("CollisionDetected") && (bool)conditions["CollisionDetected"])
                return "Collision detected";
            
            return "Condition change";
        }
        
        private static string FormatConditionValue(object value)
        {
            switch (value)
            {
                case bool b: return b ? "true" : "false";
                case float f: return f.ToString("F2");
                case Vector3 v: return $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
                case Vector2 v2: return $"({v2.x:F1}, {v2.y:F1})";
                default: return value?.ToString() ?? "null";
            }
        }
        
        private static string GetConditionStatus(string conditionName, object value)
        {
            // Determine if this condition was met or failed
            if (value is bool boolValue)
            {
                return boolValue ? "<color=#90EE90>✓</color>" : "<color=#FF6B6B>✗</color>";
            }
            
            // For non-boolean values, assume they're informational
            return "<color=#87CEEB>•</color>";
        }
    }
    
    public enum TransitionType
    {
        Module,
        State,
        InputState,
        PhysicsState
    }
    
    [System.Serializable]
    public struct DetailedTransitionRecord
    {
        public string fromState;
        public string toState;
        public TransitionType type;
        public float timestamp;
        public int frameCount;
        public Dictionary<string, object> conditions;
        public List<string> competingModules;
        public string triggerReason;
    }
    
    /// <summary>
    /// Helper class for modules to easily report their transition conditions
    /// </summary>
    public class TransitionConditionBuilder
    {
        private Dictionary<string, object> conditions = new Dictionary<string, object>();
        private List<string> competing = new List<string>();
        
        public TransitionConditionBuilder AddCondition(string name, object value)
        {
            conditions[name] = value;
            return this;
        }
        
        public TransitionConditionBuilder AddBool(string name, bool value)
        {
            return AddCondition(name, value);
        }
        
        public TransitionConditionBuilder AddFloat(string name, float value)
        {
            return AddCondition(name, value);
        }
        
        public TransitionConditionBuilder AddVector(string name, Vector3 value)
        {
            return AddCondition(name, value);
        }
        
        public TransitionConditionBuilder AddThreshold(string name, float current, float threshold, bool shouldExceed = true)
        {
            bool met = shouldExceed ? current >= threshold : current <= threshold;
            string comparison = shouldExceed ? ">=" : "<=";
            conditions[name] = $"{current:F2} {comparison} {threshold:F2}";
            conditions[name + "_Met"] = met;
            return this;
        }
        
        public TransitionConditionBuilder AddCompetingModule(string moduleName)
        {
            competing.Add(moduleName);
            return this;
        }
        
        public void LogTransition(string from, string to, TransitionType type)
        {
            TransitionDebugSystem.LogTransition(from, to, type, conditions, competing);
        }
        
        public static TransitionConditionBuilder Create() => new TransitionConditionBuilder();
    }
}