using UnityEngine;
using UnityEditor;

namespace SynapticAIPro
{
    /// <summary>
    /// Synaptic AI Pro 内部ログのラッパー。
    /// EditorPrefs "Synaptic.VerboseLog" で Info/Warn の出力を抑制可能。
    /// Error は常に出力される（重要なエラーは隠さない）。
    /// </summary>
    public static class SynLog
    {
        private const string PREF_KEY = "Synaptic.VerboseLog";

        public static bool VerboseEnabled
        {
            get => EditorPrefs.GetBool(PREF_KEY, true);
            set => EditorPrefs.SetBool(PREF_KEY, value);
        }

        public static void Info(string msg)
        {
            if (VerboseEnabled) Debug.Log(msg);
        }

        public static void Info(string msg, Object context)
        {
            if (VerboseEnabled) Debug.Log(msg, context);
        }

        public static void Warn(string msg)
        {
            if (VerboseEnabled) Debug.LogWarning(msg);
        }

        public static void Warn(string msg, Object context)
        {
            if (VerboseEnabled) Debug.LogWarning(msg, context);
        }

        // Error は常に出力（重要な情報）
        public static void Error(string msg)
        {
            Debug.LogError(msg);
        }

        public static void Error(string msg, Object context)
        {
            Debug.LogError(msg, context);
        }
    }
}
