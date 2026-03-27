using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Genies.Avatars;
using Genies.Avatars.Sdk;
using Genies.CrashReporting;
using Genies.Customization.Framework;
using Genies.Customization.Framework.Actions;
using Genies.Customization.Framework.Navigation;
using Genies.Customization.MegaEditor;
using Genies.Inventory;
using Genies.Login.Native;
using Genies.ServiceManagement;
using Genies.VirtualCamera;
using UnityEngine;
using UnityEngine.Assertions;
using Object = UnityEngine.Object;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Implementation of IAvatarEditorSdkService providing avatar editor UI functionality.
    /// Handles opening and closing the editor with proper initialization and cleanup.
    /// Customization APIs are now in AvatarCustomizationService (com.genies.avatars.customization package).
    /// </summary>
    internal class AvatarEditorSdkService : IAvatarEditorSdkService, IDisposable
    {
        private const string _avatarEditorPath = "Prefabs/AvatarEditor";
        private static GameObject _avatarEditorPrefab, _avatarEditorInstance;
        private static Camera _currentCamera;
        private static UniTaskCompletionSource _editorOpenedSource, _editorClosedSource;
        private readonly object _editorOpenedLock = new(), _editorClosedLock = new();
        private GeniesAvatar _currentActiveAvatar;
        private AvatarSaveSettings? _pendingSaveSettings;

        // Static persistent save settings that survive across editor sessions
        private static AvatarSaveSettings _persistentSaveSettings = new(AvatarSaveOption.SaveRemotelyAndExit);
        private static bool _hasInitializedPersistentSettings = false;

        // Pending Save and Exit flag setting
        private bool? _pendingSaveButtonSetting = null, _pendingExitButtonSetting = null;

        /// <summary>
        /// Gets the current persistent save settings, initializing with defaults if needed.
        /// These settings persist within the same play session due to static variable behavior.
        /// </summary>
        private static AvatarSaveSettings GetPersistentSaveSettings()
        {
            if (!_hasInitializedPersistentSettings)
            {
                _persistentSaveSettings = new AvatarSaveSettings(AvatarSaveOption.SaveRemotelyAndContinue);
                _hasInitializedPersistentSettings = true;
            }

            return _persistentSaveSettings;
        }

        /// <summary>
        /// Opens the avatar editor with the specified avatar and camera.
        /// If camera is null, attempts to get the camera with tag 'MainCamera' (Camera.main).
        /// </summary>
        public async UniTask OpenEditorAsync(GeniesAvatar avatar, Camera camera = null)
        {
            if (!GeniesLoginSdk.IsUserSignedIn())
            {
                CrashReporter.LogError("You need to be logged in to initialize the avatar editor");
                return;
            }

            if (_editorOpenedSource != null)
            {
                await _editorOpenedSource.Task;
                return;
            }

            lock (_editorOpenedLock)
            {
                if (_editorOpenedSource == null)
                {
                    _editorOpenedSource = new UniTaskCompletionSource();
                }
            }

            try
            {
                if (avatar == null)
                {
                    throw new NullReferenceException("An avatar is required in order to open the editor.");
                }

                if (camera == null)
                {
                    camera = Camera.main;
                    if (camera == null)
                    {
                        throw new NullReferenceException("A valid camera must be passed or a camera with tag 'MainCamera' must exist in the scene.");
                    }
                }

                PreloadSpecificAssetData();

                if (_avatarEditorInstance != null)
                {
                    if (!_avatarEditorInstance.activeInHierarchy)
                    {
                        _avatarEditorInstance.SetActive(true);
                    }

                    _currentActiveAvatar = avatar;
                    await InitializeEditing(avatar, camera);
                    EditorOpened?.Invoke();
                    return;
                }

                if (_avatarEditorPrefab == null)
                {
                    _avatarEditorPrefab = Resources.Load<GameObject>(_avatarEditorPath);
                }

                if (_avatarEditorPrefab == null)
                {
                    CrashReporter.LogError($"AvatarEditor prefab not found at path: {_avatarEditorPath}");
                    return;
                }

                _currentActiveAvatar = avatar;
                _avatarEditorInstance = Object.Instantiate(_avatarEditorPrefab);

                await InitializeEditing(avatar, camera);

                EditorOpened?.Invoke();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to open avatar editor: {ex.Message}");
            }
            finally
            {
                FinishEditorOpenedSource();
            }
        }

        /// <summary>
        /// Opens the avatar editor with the specified camera and loads the current user's avatar.
        /// </summary>
        public async UniTask OpenEditorAsync(Camera camera)
        {
            await OpenEditorAsync(null, camera);
        }

        /// <summary>
        /// Closes the avatar editor and cleans up resources.
        /// </summary>
        public async UniTask CloseEditorAsync(bool revertAvatar)
        {
            _currentActiveAvatar = null;

            if (_editorClosedSource != null)
            {
                await _editorClosedSource.Task;
                return;
            }

            lock (_editorClosedLock)
            {
                if (_editorClosedSource == null)
                {
                    _editorClosedSource = new UniTaskCompletionSource();
                }
            }

            if (_avatarEditorInstance == null)
            {
                return;
            }

            try
            {
                // - Return avatar definition to what it was before editing
                // - Return camera to previous position before editing
                var avatarEditingScreen = _avatarEditorInstance.GetComponentInChildren<AvatarEditingScreen>();
                if (avatarEditingScreen != null && avatarEditingScreen.EditingBehaviour is not null)
                {
                    await avatarEditingScreen.EditingBehaviour.EndEditing(revertAvatar);
                }
            }
            catch (Exception ex)
            {
                CrashReporter.LogWarning($"Exception caught while trying to clean up the avatar editor instance. This is usually ok since we're destroying the entire object instance anyway.\nException: {ex.Message}");
            }
            finally
            {
                if (_avatarEditorInstance != null)
                {
                    GameObject.Destroy(_avatarEditorInstance);
                    _avatarEditorInstance = null;
                }

                EditorClosed?.Invoke();
                FinishEditorClosedSource();

                // Clear any pending save settings when editor is closed (but keep persistent settings)
                _pendingSaveSettings = null;
            }
        }

        public void Dispose()
        {
            _ = CloseEditorAsync(true);
        }

        /// <summary>
        /// Gets the currently active avatar being edited in the editor.
        /// </summary>
        /// <returns>The currently active GeniesAvatar, or null if no avatar is currently being edited</returns>
        public GeniesAvatar GetCurrentActiveAvatar()
        {
            return _currentActiveAvatar;
        }

        /// <summary>
        /// Gets whether the editor is currently open.
        /// </summary>
        /// <returns>True if the editor is open and active, false otherwise</returns>
        public bool IsEditorOpen
        {
            get => _avatarEditorInstance != null && _avatarEditorInstance.activeInHierarchy;
        }

        /// <summary>
        /// Event raised when the editor is opened.
        /// </summary>
        public event Action EditorOpened;

        /// <summary>
        /// Event raised when the editor is closed.
        /// </summary>
        public event Action EditorClosed;

        /// <summary>
        /// Sets the save option for the avatar editor.
        /// Can be called before or after the editor is opened.
        /// </summary>
        /// <param name="saveOption">The save option to use when saving the avatar</param>
        public void SetEditorSaveOption(AvatarSaveOption saveOption)
        {
            SetEditorSaveSettings(new AvatarSaveSettings(saveOption));
        }

        /// <summary>
        /// Sets the save option and profile ID for the avatar editor.
        /// Can be called before or after the editor is opened.
        /// </summary>
        /// <param name="saveOption">The save option to use when saving the avatar</param>
        /// <param name="profileId">The profile ID to use when saving locally</param>
        public void SetEditorSaveOption(AvatarSaveOption saveOption, string profileId)
        {
            SetEditorSaveSettings(new AvatarSaveSettings(saveOption, profileId));
        }

        /// <summary>
        /// Sets the save settings for the avatar editor.
        /// These settings persist across multiple editor sessions within the same play session.
        /// Settings are automatically reset when play mode exits
        /// Can be called before or after the editor is opened.
        /// </summary>
        /// <param name="saveSettings">The save settings to use when saving the avatar</param>
        public void SetEditorSaveSettings(AvatarSaveSettings saveSettings)
        {
            // Store settings both for immediate use and persistent session storage
            _pendingSaveSettings = saveSettings;
            _persistentSaveSettings = saveSettings;
            _hasInitializedPersistentSettings = true;

            // If editor is already open, apply the save settings immediately
            if (_avatarEditorInstance != null)
            {
                ApplySaveSettings(saveSettings);
            }
        }

        #region Helpers

        /// <summary>
        /// Calls some endpoints from inventory to begin fetching data early
        /// </summary>
        private void PreloadSpecificAssetData()
        {
            try
            {
                var defaultInventory = ServiceManager.Get<IDefaultInventoryService>();
                defaultInventory.GetDefaultWearables(null, new List<string> { "hair" }).Forget();
                defaultInventory.GetUserWearables().Forget();
                defaultInventory.GetDefaultAvatarBaseData().Forget();
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to preload inventory assets when opening avatar editor: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies the save settings to the avatar editing screen if the editor is currently open.
        /// If the editor is not open, the settings will be applied when the editor is initialized.
        /// </summary>
        /// <param name="saveSettings">The save settings to apply</param>
        private void ApplySaveSettings(AvatarSaveSettings saveSettings)
        {
            // If the editor is not currently open, the settings are already stored in _pendingSaveSettings
            // and will be applied when the editor is opened in InitializeEditing()
            if (_avatarEditorInstance == null)
            {
                return; // This is not an error - the editor is simply not open yet
            }

            var avatarEditingScreen = _avatarEditorInstance.GetComponentInChildren<AvatarEditingScreen>();
            if (avatarEditingScreen == null)
            {
                CrashReporter.LogError("AvatarEditingScreen not found. Cannot apply save settings to open editor.");
                return;
            }

            avatarEditingScreen.SetSaveSettings(saveSettings);
        }

        private async UniTask InitializeEditing(GeniesAvatar avatar, Camera camera)
        {
            var virtualCameraManager = _avatarEditorInstance.GetComponentInChildren<VirtualCameraManager>();
            Assert.IsNotNull(virtualCameraManager);

            // We set the rotation here to Quaternion.identity for the camera system to behave correctly.
            // The genie is later also rotated to Quaternion.identity
            _avatarEditorInstance.transform.SetPositionAndRotation(avatar.Root.transform.position, Quaternion.identity);

            var editingScreen = _avatarEditorInstance.GetComponentInChildren<AvatarEditingScreen>();
            Assert.IsNotNull(editingScreen);

            // Apply Save and Exit flag setting if one was set before editor opened
            if (_pendingSaveButtonSetting.HasValue && _pendingExitButtonSetting.HasValue)
            {
                ApplySaveAndExitFlagSetting(_pendingSaveButtonSetting.Value, _pendingExitButtonSetting.Value);
            }

            await editingScreen.Initialize(avatar, camera, virtualCameraManager);

            // Apply save settings after initialization - use pending first, then persistent, then default
            AvatarSaveSettings settingsToApply;
            if (_pendingSaveSettings.HasValue)
            {
                settingsToApply = _pendingSaveSettings.Value;
            }
            else
            {
                settingsToApply = GetPersistentSaveSettings();
            }

            ApplySaveSettings(settingsToApply);
        }

        private static void FinishEditorOpenedSource()
        {
            var source = _editorOpenedSource;
            source?.TrySetResult();
            _editorOpenedSource = null;
        }

        private static void FinishEditorClosedSource()
        {
            var source = _editorClosedSource;
            _editorClosedSource = null;
            source?.TrySetResult();
        }

        /// <summary>
        /// Sets the Save and Exit ActionBarFlags on all BaseCustomizationControllers in the InventoryNavigationGraph.
        /// Excludes CustomHairColor_Controller, CustomEyelashColor_Controller, and CustomEyebrowColor_Controller
        /// (which always need it to exit their custom color editing screen)
        /// </summary>
        /// <param name="enableSaveButton">True to enable the save button, false to disable</param>
        /// <param name="enableExitButton">True to enable the exit button, false to disable</param>
        public void SetSaveAndExitButtonStatus(bool enableSaveButton, bool enableExitButton)
        {
            // Store the pending setting
            _pendingSaveButtonSetting = enableSaveButton;
            _pendingExitButtonSetting = enableExitButton;

            // If editor is already open, apply the setting immediately
            if (_avatarEditorInstance != null)
            {
                ApplySaveAndExitFlagSetting(enableSaveButton, enableExitButton);
            }
            // Note: If editor is not open, the setting will be applied during InitializeEditing
            // (when the editor is opened)
        }

        /// <summary>
        /// Applies the Save and Exit ActionBarFlags setting to all BaseCustomizationControllers in the InventoryNavigationGraph
        /// </summary>
        private void ApplySaveAndExitFlagSetting(bool enableSaveButton, bool enableExitButton)
        {
            try
            {
                if (_avatarEditorInstance == null)
                {
                    CrashReporter.LogWarning("Cannot apply Save and Exit flag setting - editor instance not found");
                    return;
                }

                NavigationGraph navigationGraph = null;

                var avatarEditingScreen = _avatarEditorInstance.GetComponentInChildren<AvatarEditingScreen>();
                if (avatarEditingScreen != null)
                {
                    navigationGraph = avatarEditingScreen.NavGraph;
                }

                if (navigationGraph == null)
                {
                    CrashReporter.LogError("NavigationGraph not found in AvatarEditingScreen");
                    return;
                }

                // Controllers to exclude
                var excludedControllers = new HashSet<Type>
                {
                    typeof(CustomHairColorCustomizationController),
                    typeof(CustomFlairColorCustomizationController)
                };

                // Get all nodes from the navigation graph
                var allControllers = new List<BaseCustomizationController>();
                CollectAllControllers(navigationGraph.GetRootNode(), allControllers, excludedControllers);

                // Update the ActionBarFlags for each controller
                foreach (var controller in allControllers)
                {
                    if (controller != null && controller.CustomizerViewConfig != null)
                    {
                        if (enableSaveButton)
                        {
                            controller.CustomizerViewConfig.actionBarFlags |= ActionBarFlags.Save;
                        }
                        else
                        {
                            controller.CustomizerViewConfig.actionBarFlags &= ~ActionBarFlags.Save;
                        }

                        if (enableExitButton)
                        {
                            controller.CustomizerViewConfig.actionBarFlags |= ActionBarFlags.Exit;
                        }
                        else
                        {
                            controller.CustomizerViewConfig.actionBarFlags &= ~ActionBarFlags.Exit;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CrashReporter.LogError($"Failed to apply Save and Exit ActionBarFlags: {ex.Message}");
            }
        }

        /// <summary>
        /// Recursively collects all BaseCustomizationControllers from the navigation graph,
        /// excluding specified controllers by type.
        /// </summary>
        private void CollectAllControllers(
            INavigationNode node,
            List<BaseCustomizationController> controllers,
            HashSet<Type> excludedTypes)
        {
            if (node == null)
            {
                return;
            }

            // Get the controller from this node
            if (node.Controller is BaseCustomizationController controller)
            {
                // Check if this controller should be excluded
                if (!excludedTypes.Any(t => t.IsAssignableFrom(controller.GetType())))
                {
                    controllers.Add(controller);
                }
            }

            // Recursively process child nodes
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    CollectAllControllers(child, controllers, excludedTypes);
                }
            }

            // Also check EditItemNode and CreateItemNode
            if (node.EditItemNode != null)
            {
                CollectAllControllers(node.EditItemNode, controllers, excludedTypes);
            }

            if (node.CreateItemNode != null)
            {
                CollectAllControllers(node.CreateItemNode, controllers, excludedTypes);
            }
        }

        #endregion
    }
}
