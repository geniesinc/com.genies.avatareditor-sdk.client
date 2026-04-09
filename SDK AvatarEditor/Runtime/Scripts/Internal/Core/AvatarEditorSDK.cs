using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Genies.Avatars.Sdk;
using Genies.CrashReporting;
using Genies.ServiceManagement;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Static convenience facade for opening and closing the Avatar Editor UI.
    /// - Auto-initializes required services on first use
    /// - Provides public static methods for opening/closing the editor and configuring save behavior
    /// - Customization APIs (equip, color, body, save/load) are in AvatarCustomizationSDK
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal static class AvatarEditorSDK
#else
    public static class AvatarEditorSDK
#endif
    {
        /// <summary>
        /// Event raised when the Avatar Editor SDK is initialized successfully.
        /// </summary>
        internal static event Action AvatarEditorSdkInitialized = delegate { };

        /// <summary>
        /// Event raised when the editor is opened.
        /// </summary>
        internal static event Action EditorOpened = delegate { };

        /// <summary>
        /// Event raised when the editor is closed.
        /// </summary>
        internal static event Action EditorClosed = delegate { };

        /// <summary>
        /// Event raised when the editor save option is changed.
        /// </summary>
        internal static event Action EditorSaveOptionSet = delegate { };

        /// <summary>
        /// Event raised when editor save settings are changed.
        /// </summary>
        internal static event Action EditorSaveSettingsSet = delegate { };

        #region Initialization / Service Access

        private static UniTaskCompletionSource<bool> InitializationSource;
        private static bool Initialized;

        public static bool IsInitialized => Initialized;

        public static async UniTask<bool> InitializeAsync()
        {
            if (InitializationSource != null)
            {
                return await InitializationSource.Task.Preserve();
            }

            InitializationSource = new UniTaskCompletionSource<bool>();

            PerformInitializationAsync().Forget();
            var status = await InitializationSource.Task.Preserve();

            AvatarEditorSdkInitialized?.Invoke();
            return status;
        }

        private static async UniTask PerformInitializationAsync()
        {
            try
            {
                if (await AvatarSdk.InitializeAsync() is false)
                {
                    CrashReporter.LogError("Error initializing avatar editor SDK");
                    InitializationSource?.TrySetResult(false);
                    InitializationSource = null;
                    return;
                }

                new AvatarEditorSdkInstaller().Register();

                var service = ServiceManager.Get<IAvatarEditorSdkService>();
                if (service != null)
                {
                    service.EditorOpened += () => EditorOpened?.Invoke();
                    service.EditorClosed += () => EditorClosed?.Invoke();
                }

                Initialized = true;
                InitializationSource?.TrySetResult(true);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to initialize avatar editor SDK: {ex.Message}");
                InitializationSource?.TrySetResult(false);
                InitializationSource = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            InitializationSource = null;
            Initialized = false;
        }

        #endregion

        #region Public Static API — Editor UI

        public static async UniTask OpenEditorAsync(GeniesAvatar avatar, Camera camera = null)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                await avatarEditorSdkService.OpenEditorAsync(avatar, camera);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to open avatar editor: {ex.Message}");
            }
        }

        public static async UniTask CloseEditorAsync(bool revertAvatar)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                await avatarEditorSdkService.CloseEditorAsync(revertAvatar);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to close avatar editor: {ex.Message}");
            }
        }

        public static GeniesAvatar GetCurrentActiveAvatar()
        {
            try
            {
                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                return avatarEditorSdkService?.GetCurrentActiveAvatar();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to get current active avatar: {ex.Message}");
                return null;
            }
        }

        public static bool IsEditorOpen
        {
            get
            {
                try
                {
                    var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                    return avatarEditorSdkService?.IsEditorOpen ?? false;
                }
                catch (Exception ex)
                {
                    CrashReporter.LogError($"Failed to get editor open state: {ex.Message}");
                    return false;
                }
            }
        }

        #endregion

        #region Public Static API — Editor Save / Button Configuration

        public static async UniTask SetEditorSaveOptionAsync(AvatarSaveOption saveOption)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    CrashReporter.LogError("AvatarEditorSdkService not found. Cannot set save option.");
                    return;
                }

                avatarEditorSdkService.SetEditorSaveOption(saveOption);
                EditorSaveOptionSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save option: {ex.Message}");
            }
        }

        public static async UniTask SetEditorSaveOptionAsync(AvatarSaveOption saveOption, string profileId)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetEditorSaveOption(saveOption, profileId);
                EditorSaveOptionSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save option: {ex.Message}");
            }
        }

        public static async UniTask SetEditorSaveSettingsAsync(AvatarSaveSettings saveSettings)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetEditorSaveSettings(saveSettings);
                EditorSaveSettingsSet?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set editor save settings: {ex.Message}");
            }
        }

        public static async UniTask SetSaveAndExitButtonStatusAsync(bool enableSaveButton, bool enableExitButton)
        {
            try
            {
                if (await InitializeAsync() is false)
                {
                    throw new InvalidOperationException("Failed to initialize AvatarEditorSDK");
                }

                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    throw new NullReferenceException("AvatarEditorSdkService not found");
                }

                avatarEditorSdkService.SetSaveAndExitButtonStatus(enableSaveButton, enableExitButton);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set Save and Exit ActionBarFlags: {ex.Message}");
            }
        }

        public static void SetSaveAndExitButtonStatus(bool enableSaveButton, bool enableExitButton)
        {
            try
            {
                var avatarEditorSdkService = ServiceManager.Get<IAvatarEditorSdkService>();
                if (avatarEditorSdkService == null)
                {
                    CrashReporter.LogWarning("AvatarEditorSdkService not found. Make sure the SDK is initialized before calling this method.");
                    return;
                }

                avatarEditorSdkService.SetSaveAndExitButtonStatus(enableSaveButton, enableExitButton);
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to set Save and Exit ActionBarFlags: {ex.Message}");
            }
        }

        #endregion
    }
}
