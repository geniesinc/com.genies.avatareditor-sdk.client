#if UNITY_EDITOR
using System.Collections.Generic;
using Genies.Telemetry;
using UnityEditor;

namespace Genies.Sdk.AvatarEditor.Core
{
    [InitializeOnLoad]
    internal static class AvatarEditorSdkTelemetryObserver
    {
        private static bool _subscribed;

        private const string ContextValue = "Avatar SDK";

        private static string sdkVersion = "";

        static AvatarEditorSdkTelemetryObserver()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                EnsureSubscribed();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                Unsubscribe();
            }
        }

        private static void EnsureSubscribed()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;

            AvatarEditorSDK.AvatarEditorSdkInitialized += OnAvatarEditorSdkInitialized;
            AvatarEditorSDK.EditorOpened += OnEditorOpened;
            AvatarEditorSDK.EditorClosed += OnEditorClosed;
            AvatarEditorSDK.EditorSaveOptionSet += OnSaveOptionSet;
            AvatarEditorSDK.EditorSaveSettingsSet += OnSaveSettingsSet;
        }

        private static void Unsubscribe()
        {
            if (!_subscribed)
                return;

            _subscribed = false;

            AvatarEditorSDK.AvatarEditorSdkInitialized -= OnAvatarEditorSdkInitialized;
            AvatarEditorSDK.EditorOpened -= OnEditorOpened;
            AvatarEditorSDK.EditorClosed -= OnEditorClosed;
            AvatarEditorSDK.EditorSaveOptionSet -= OnSaveOptionSet;
            AvatarEditorSDK.EditorSaveSettingsSet -= OnSaveSettingsSet;
        }

        private static void OnAvatarEditorSdkInitialized() =>
            RecordEvent("avatar_editor_sdk_initialized");
        private static void OnEditorOpened() =>
            RecordEvent("avatar_editor_opened");

        private static void OnEditorClosed() =>
            RecordEvent("avatar_editor_closed");

        private static void OnSaveOptionSet() =>
            RecordEvent("avatar_editor_save_option_set");

        private static void OnSaveSettingsSet() =>
            RecordEvent("avatar_editor_save_settings_set");

        private static void RecordEvent(string eventName)
        {
            GeniesTelemetry.RecordEvent(
                TelemetryEvent.Create(
                    eventName,
                    WithSdkMetadata(null)
                )
            );
        }

        private static Dictionary<string, object> WithSdkMetadata(Dictionary<string, object> properties)
        {
            var result = properties != null
                ? new Dictionary<string, object>(properties)
                : new Dictionary<string, object>();

            if (string.IsNullOrWhiteSpace(sdkVersion))
            {
                sdkVersion = GetSdkVersionFromCache();
            }

            result["context"] = ContextValue;
            result["sdkVersion"] = string.IsNullOrWhiteSpace(sdkVersion) ? "unknown" : sdkVersion;

            return result;
        }

        private static string GetSdkVersionFromCache()
        {
            try
            {
                var cache = UnityEngine.Resources.Load<GeniesSdkVersionCache>(GeniesTelemetryInstaller.CacheName);
                if (cache != null &&
                    !string.IsNullOrWhiteSpace(cache.Version) &&
                    cache.Version != "unknown")
                {
                    return cache.Version;
                }
            }
            catch
            {
            }

            return "unknown";
        }
    }
}
#endif
