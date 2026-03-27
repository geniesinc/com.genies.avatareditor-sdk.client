using System;
using UnityEngine;
using Cysharp.Threading.Tasks;
using Genies.Avatars.Customization;
using Genies.CrashReporting;
using Genies.ServiceManagement;
using Genies.Avatars.Sdk;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Static convenience facade for opening and closing the Avatar Editor UI.
    /// - Auto-initializes required services on first use
    /// - Provides public static methods for opening/closing the editor and configuring save behavior
    /// - Customization APIs (equip, color, body, save/load) are in AvatarCustomizationSDK
    /// </summary>
    internal static class AvatarEditorSDK
    {
        public static bool IsInitialized =>
            InitializationCompletionSource is not null
            && InitializationCompletionSource.Task.Status == UniTaskStatus.Succeeded;
        private static UniTaskCompletionSource InitializationCompletionSource { get; set; }

        private static IAvatarEditorSdkService CachedService { get; set; }
        private static bool EventsSubscribed { get; set; }

        /// <summary>
        /// Event raised when the editor is opened.
        /// </summary>
        public static event Action EditorOpened = delegate { };

        /// <summary>
        /// Event raised when the editor is closed.
        /// </summary>
        public static event Action EditorClosed = delegate { };

        /// <summary>
        /// Event raised when the editor save option is changed.
        /// </summary>
        public static event Action EditorSaveOptionSet = delegate { };

        /// <summary>
        /// Event raised when editor save settings are changed.
        /// </summary>
        public static event Action EditorSaveSettingsSet = delegate { };

        #region Initialization / Service Access

        public static async UniTask<bool> InitializeAsync()
        {
            if (await AvatarCustomizationSDK.InitializeAsync())
            {
                var avatarEditorSdkInstaller = new AvatarEditorSdkInstaller();
                avatarEditorSdkInstaller.Register();
                return true;
            }

            CrashReporter.LogError("Error initializing avatar editor SDK");
            return false;
        }

        internal static async UniTask<IAvatarEditorSdkService> GetOrCreateAvatarEditorSdkInstance()
        {
            if (await InitializeAsync() is false)
            {
                CrashReporter.LogError("Avatar editor could not be initialized.");
                return default;
            }

            var service = ServiceManager.Get<IAvatarEditorSdkService>();
            SubscribeToServiceEvents(service);
            return service;
        }

        private static void SubscribeToServiceEvents(IAvatarEditorSdkService service)
        {
            if (service == null)
            {
                return;
            }

            if (ReferenceEquals(service, CachedService)
                && EventsSubscribed)
            {
                return;
            }

            CachedService = service;
            CachedService.EditorOpened += OnEditorOpened;
            CachedService.EditorClosed += OnEditorClosed;

            EventsSubscribed = true;
        }

        private static void OnEditorOpened()
        {
            EditorOpened?.Invoke();
        }

        private static void OnEditorClosed()
        {
            EditorClosed?.Invoke();
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

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
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

                var avatarEditorSdkService = await GetOrCreateAvatarEditorSdkInstance();
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
