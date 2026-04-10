# Genies Avatar Editor SDK

The Avatar Editor SDK is a legacy add-on package for the [Genies Avatar SDK](https://assetstore.unity.com/packages/tools/game-toolkits/genies-avatar-sdk-336166?srsltid=AfmBOoq0n2Mqua3R8zaDscD2qRxoSjsYJE8FTPJqu8Qt53ZuHKq7JSfO) that provides a full-featured UI for customizing Genies Avatars. It allows users to change wearables, body features, face features, and more through an interactive editor experience.

While this package is publicly available for use, we will not be updating it as we continue to update the Genies Avatar SDK. We recommend using the prefabs and scripts from the ["Creating an Avatar Editor" sample](http://docs.genies.com/docs/sdk-avatar/sample-scenes/creating-editor) that can be imported from within the Genies Avatar SDK.

## Requirements

- Unity **2022.3** LTS (2022.3.32f1 or later) or Unity 6
- [Genies Avatar SDK](https://assetstore.unity.com/packages/tools/game-toolkits/genies-avatar-sdk-336166?srsltid=AfmBOoq0n2Mqua3R8zaDscD2qRxoSjsYJE8FTPJqu8Qt53ZuHKq7JSfO) (`com.genies.avatar-sdk.client` v3.8.4+)

## Installation

Ensure the Genies Avatar SDK is already installed, then add this package via the Unity Package Manager using either the HTTPS or SSH URL from this repo, depending on your setup.

## Usage

The public API is accessible through the `AvatarEditorSdk` class in the `Genies.Sdk` namespace.

### Opening the Editor

```csharp
using Genies.Sdk;

// Open the editor with a loaded avatar
await AvatarEditorSdk.OpenAvatarEditorAsync(myAvatar);

// Optionally specify a camera
await AvatarEditorSdk.OpenAvatarEditorAsync(myAvatar, myCamera);
```

### Closing the Editor

```csharp
// Close and keep edits
await AvatarEditorSdk.CloseAvatarEditorAsync(revertAvatar: false);

// Close and revert to pre-edit state
await AvatarEditorSdk.CloseAvatarEditorAsync(revertAvatar: true);
```

### Saving

The editor supports saving locally or to the cloud, with the option to continue editing or exit after saving:

```csharp
// Save to cloud and continue editing
await AvatarEditorSdk.SetEditorSaveRemotelyAndContinueAsync();

// Save to cloud and exit the editor
await AvatarEditorSdk.SetEditorSaveRemotelyAndExitAsync();

// Save locally and continue editing
await AvatarEditorSdk.SetEditorSaveLocallyAndContinueAsync(profileId);

// Save locally and exit the editor
await AvatarEditorSdk.SetEditorSaveLocallyAndExitAsync(profileId);
```

### Events

```csharp
// Subscribe to editor lifecycle events
AvatarEditorSdk.Events.AvatarEditorOpened += OnEditorOpened;
AvatarEditorSdk.Events.AvatarEditorClosed += OnEditorClosed;
```

### Querying State

```csharp
// Check if the editor is currently open
bool isOpen = AvatarEditorSdk.IsAvatarEditorOpen;

// Get the avatar currently being edited
ManagedAvatar avatar = AvatarEditorSdk.GetAvatarEditorAvatar();
```

See our [Documentation](https://docs.genies.com/docs/sdk-avatar/tools/legacy-editor) for more information.
