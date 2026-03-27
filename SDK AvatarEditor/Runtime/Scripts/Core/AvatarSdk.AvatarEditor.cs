using System;
using Cysharp.Threading.Tasks;
using Genies.Sdk.AvatarEditor.Core;
using UnityEngine;

namespace Genies.Sdk
{
    public sealed class AvatarEditorSdk
    {
        /// <summary>
        /// Provides events for SDK notifications.
        /// </summary>
        public static class Events
        {
            /// <summary>
            /// Event raised when the avatar editor is opened.
            /// </summary>
            public static event Action AvatarEditorOpened
            {
                add => AvatarEditorSDK.EditorOpened += value;
                remove => AvatarEditorSDK.EditorOpened -= value;
            }

            /// <summary>
            /// Event raised when the avatar editor is closed.
            /// </summary>
            public static event Action AvatarEditorClosed
            {
                add => AvatarEditorSDK.EditorClosed += value;
                remove => AvatarEditorSDK.EditorClosed -= value;
            }
        }

        /// <summary>
        /// Gets whether the avatar editor is currently open.
        /// </summary>
        /// <returns>True if the editor is open and active, false otherwise</returns>
        public static bool IsAvatarEditorOpen => AvatarEditorSDK.IsEditorOpen;

        /// <summary>
        /// Opens the Avatar Editor with the specified avatar and camera.
        /// </summary>
        /// <param name="avatar">The avatar to edit. If null, loads the current user's avatar.</param>
        /// <param name="camera">The camera to use for the editor. If null, uses Camera.main.</param>
        /// <returns>A UniTask that completes when the editor is opened.</returns>
        public static async UniTask OpenAvatarEditorAsync(ManagedAvatar avatar, Camera camera = null)
        {
            var geniesAvatar = avatar?.GeniesAvatar;
            if (geniesAvatar is null)
            {
                Debug.LogWarning("The Avatar Editor requires a valid Avatar instance to be opened.");
                return;
            }

            await AvatarEditorSDK.OpenEditorAsync(geniesAvatar, camera);
        }

        /// <summary>
        /// Closes the Avatar Editor and cleans up resources.
        /// </summary>
        /// /// <param name="revertAvatar">Whether the avatar should be reverted to it's pre-edited self.</param>
        /// <returns>A UniTask that completes when the editor is closed.</returns>
        public static async UniTask CloseAvatarEditorAsync(bool revertAvatar) => await AvatarEditorSDK.CloseEditorAsync(revertAvatar);

        /// <summary>
        /// Gets the active avatar being edited in the Avatar Editor.
        /// </summary>
        /// <returns>The currently active ManagedAvatar, or null if no avatar is currently being edited.</returns>
        public static ManagedAvatar GetAvatarEditorAvatar()
        {
            var geniesAvatar = AvatarEditorSDK.GetCurrentActiveAvatar();
            return geniesAvatar != null ? new ManagedAvatar(geniesAvatar) : null;
        }

        /// <summary>
        /// Sets the avatar editor to save locally and continue editing.
        /// </summary>
        /// <param name="profileId">The profile ID to use when saving locally. If null, uses the default template name.</param>
        /// <returns>A UniTask representing the async operation.</returns>
        public static async UniTask SetEditorSaveLocallyAndContinueAsync(string profileId) =>
            await AvatarEditorSDK.SetEditorSaveOptionAsync(Genies.Sdk.AvatarEditor.Core.AvatarSaveOption.SaveLocallyAndContinue, profileId);

        /// <summary>
        /// Sets the avatar editor to save locally and exit the editor.
        /// </summary>
        /// <param name="profileId">The profile ID to use when saving locally. If null, uses the default template name.</param>
        /// <returns>A UniTask representing the async operation.</returns>
        public static async UniTask SetEditorSaveLocallyAndExitAsync(string profileId) =>
            await AvatarEditorSDK.SetEditorSaveOptionAsync(Genies.Sdk.AvatarEditor.Core.AvatarSaveOption.SaveLocallyAndExit, profileId);

        /// <summary>
        /// Sets the avatar editor to save to the cloud and continue editing.
        /// </summary>
        /// <returns>A UniTask representing the async operation.</returns>
        public static async UniTask SetEditorSaveRemotelyAndContinueAsync() =>
            await AvatarEditorSDK.SetEditorSaveOptionAsync(Genies.Sdk.AvatarEditor.Core.AvatarSaveOption.SaveRemotelyAndContinue);

        /// <summary>
        /// Sets the avatar editor to save to the cloud and exit the editor.
        /// </summary>
        /// <returns>A UniTask representing the async operation.</returns>
        public static async UniTask SetEditorSaveRemotelyAndExitAsync() =>
            await AvatarEditorSDK.SetEditorSaveOptionAsync(Genies.Sdk.AvatarEditor.Core.AvatarSaveOption.SaveRemotelyAndExit);
    }
}
