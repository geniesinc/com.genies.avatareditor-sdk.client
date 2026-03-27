using System;
using Cysharp.Threading.Tasks;
using Genies.Avatars;
using Genies.Avatars.Sdk;
using UnityEngine;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Core service interface for managing the Avatar Editor UI.
    /// Provides methods for opening and closing the avatar editor with customizable properties.
    /// Editor-specific functionality only; customization APIs are in IAvatarCustomizationService.
    /// </summary>
    internal interface IAvatarEditorSdkService
    {
        /// <summary>
        /// Opens the avatar editor with the specified avatar and camera.
        /// </summary>
        /// <param name="avatar">The avatar to edit. If null, loads the current user's avatar.</param>
        /// <param name="camera">The camera to use for the editor. If null, uses Camera.main.</param>
        /// <returns>A UniTask that completes when the editor is opened.</returns>
        public UniTask OpenEditorAsync(GeniesAvatar avatar, Camera camera = null);

        /// <summary>
        /// Closes the avatar editor and cleans up resources.
        /// </summary>
        /// <returns>A UniTask that completes when the editor is closed.</returns>
        /// <param name="revertAvatar">Whether the avatar should be reverted to its pre-edited version.</param>
        public UniTask CloseEditorAsync(bool revertAvatar);

        /// <summary>
        /// Gets the currently active avatar being edited in the editor.
        /// </summary>
        /// <returns>The currently active GeniesAvatar, or null if no avatar is currently being edited</returns>
        public GeniesAvatar GetCurrentActiveAvatar();

        /// <summary>
        /// Gets whether the editor is currently open.
        /// </summary>
        /// <returns>True if the editor is open and active, false otherwise</returns>
        public bool IsEditorOpen { get; }

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
        /// </summary>
        /// <param name="saveOption">The save option to use when saving the avatar</param>
        public void SetEditorSaveOption(AvatarSaveOption saveOption);

        /// <summary>
        /// Sets the save option and profile ID for the avatar editor.
        /// </summary>
        /// <param name="saveOption">The save option to use when saving the avatar</param>
        /// <param name="profileId">The profile ID to use when saving locally</param>
        public void SetEditorSaveOption(AvatarSaveOption saveOption, string profileId);

        /// <summary>
        /// Sets the save settings for the avatar editor.
        /// Settings persist across multiple editor sessions within the same play session.
        /// </summary>
        /// <param name="saveSettings">The save settings to use when saving the avatar</param>
        public void SetEditorSaveSettings(AvatarSaveSettings saveSettings);

        /// <summary>
        /// Sets the Save and Exit ActionBarFlags on all BaseCustomizationControllers in the InventoryNavigationGraph.
        /// Excludes CustomHairColor_Controller, CustomEyelashColor_Controller, and CustomEyebrowColor_Controller
        /// (which always need it to exit their custom color editing screen)
        /// </summary>
        /// <param name="enableSaveButton">True to enable the save button, false to disable</param>
        /// <param name="enableExitButton">True to enable the exit button, false to disable</param>
        public void SetSaveAndExitButtonStatus(bool enableSaveButton, bool enableExitButton);
    }
}
