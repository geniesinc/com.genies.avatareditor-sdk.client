using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Analytics;
using Genies.Avatars;
using Genies.Avatars.Behaviors;
using Genies.Avatars.Services;
using Genies.CameraSystem;
using Genies.CrashReporting;
using Genies.Customization.Framework;
using Genies.Inventory.UIData;
using Genies.Looks.Customization.Commands;
using Genies.MegaEditor;
using Genies.Models;
using Genies.Naf;
using Genies.ServiceManagement;
using Genies.UIFramework.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Controller for the customize color view.
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class CustomFlairColorCustomizationController : BaseCustomizationController
#else
    public class CustomFlairColorCustomizationController : BaseCustomizationController
#endif
    {
        [SerializeField]
        private CustomizeColorView _prefab;
        private CustomizeColorView _customizeColorViewInstance;

        [SerializeField]
        public AvatarFeatureColorItemPickerDataSource flairColorDataSource;

        /// <summary>
        /// The focus camera to activate and set as active on this customization controller.
        /// </summary>
        public GeniesVirtualCameraCatalog virtualCamera;

        [NonSerialized] private PictureInPictureController _pictureInPictureController;

        private PictureInPictureController PictureInPictureController
        {
            get
            {
                if (_pictureInPictureController == null || _pictureInPictureController.gameObject == null)
                {
                    _pictureInPictureController = ServiceManager.Get<PictureInPictureController>();
                }

                return _pictureInPictureController;
            }
        }

        private VirtualCameraController<GeniesVirtualCameraCatalog> VirtualCameraController =>
            ServiceManager.Get<VirtualCameraController<GeniesVirtualCameraCatalog>>();

        private FlairColorPreset _workingPreset;
        private GradientColorUiData _previousPreset;
        private Color[] _originalFlairColors;

        public override async UniTask<bool> TryToInitialize(Customizer customizer)
        {
            _customizer = customizer;

            if (_customizeColorViewInstance == null)
            {
                _customizeColorViewInstance = _customizer.View.GetOrCreateViewInLayer("custom-flair-color-view",
                    CustomizerViewLayer.CustomizationEditorFullScreen, _prefab);
            }

            flairColorDataSource.Initialize(_customizer);

            return await UniTask.FromResult(true);
        }

        public override void StartCustomization()
        {
            VirtualCameraController.ActivateVirtualCamera(virtualCamera).Forget();
            PictureInPictureController.canBeDisabled = false;
            PictureInPictureController.Enable();
            VirtualCameraController.SetFullScreenModeInFocusCameras(true);

            flairColorDataSource.StartCustomization();

            _previousPreset = flairColorDataSource.CurrentLongPressColorData;

            if (_previousPreset != null)
            {
                switch (CurrentCustomColorViewState)
                {
                    case CustomColorViewState.CreateNew:
                        _workingPreset = NewCustomColorFromPreset(_previousPreset);
                        break;
                    case CustomColorViewState.Edit:
                        _workingPreset = EditCustomColorFromPreset(_previousPreset);
                        break;
                    default:
                        CrashReporter.LogError($"Invalid State to access the flair color pick {CurrentCustomColorViewState}");
                        _workingPreset = NewCustomColorFromPreset(_previousPreset);
                        break;
                }
                _originalFlairColors = (Color[])_workingPreset.Colors.Clone();
            }
            else
            {
                _workingPreset = new FlairColorPreset
                {
                    Guid = Guid.NewGuid().ToString(),
                    Colors = new[] { Color.black, Color.black, Color.black, Color.black },
                };
                _originalFlairColors = null;
            }

            var props = new AnalyticProperties();
            if (_workingPreset != null && !string.IsNullOrEmpty(_workingPreset.Guid))
            {
                props.AddProperty("selectedColor", _workingPreset.Guid);
            }

            AnalyticsReporter.LogEvent(
                FlairItemPickerDataSource.AnalyticsEventsPerFlairType[flairColorDataSource.FlairAssetType][FlairItemPickerDataSource.AnalyticsActionType.ColorPickerSelected],
                props);

            Color[] initialColors = new[] { _workingPreset.Colors[0], _workingPreset.Colors[1] };
            _customizeColorViewInstance.Initialize(initialColors);
            _customizeColorViewInstance.OnColorSelected.AddListener(UpdateFlairColor);
        }

        /// <summary>
        /// Updates the flair color of the avatar without using the command pattern.
        /// </summary>
        /// <param name="color">the given skin color</param>
        private void UpdateFlairColor(Color color)
        {
            var indexSelected = _customizeColorViewInstance.ColorRegionsView.SelectedRegionIndex;
            _workingPreset.Colors[indexSelected] = color;

            switch (flairColorDataSource.FlairAssetType)
            {
                case FlairAssetType.Eyebrows:
                    var colors = new GenieColorEntry[]
                    {
                        new(GenieColor.EyebrowsBase, _workingPreset.Colors[0]),
                        new(GenieColor.EyebrowsR, _workingPreset.Colors[1]),
                        new(GenieColor.EyebrowsG, _workingPreset.Colors[2]),
                        new(GenieColor.EyebrowsB, _workingPreset.Colors[3]),
                    };
                    CurrentCustomizableAvatar.SetColorsAsync(colors).Forget();
                    break;

                case FlairAssetType.Eyelashes:
                    colors = new GenieColorEntry[]
                    {
                        new(GenieColor.EyelashesBase, _workingPreset.Colors[0]),
                        new(GenieColor.EyelashesR, _workingPreset.Colors[1]),
                        new(GenieColor.EyelashesG, _workingPreset.Colors[2]),
                        new(GenieColor.EyelashesB, _workingPreset.Colors[3]),
                    };
                    CurrentCustomizableAvatar.SetColorsAsync(colors).Forget();
                    break;

                case FlairAssetType.None: default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void StopCustomization()
        {
            _customizeColorViewInstance.OnColorSelected.RemoveListener(UpdateFlairColor);

            PictureInPictureController.canBeDisabled = true;
            PictureInPictureController.Disable();
            VirtualCameraController.SetFullScreenModeInFocusCameras(false);

            CurrentCustomColorViewState = CustomColorViewState.Normal;
            flairColorDataSource.StopCustomization();
            _customizer.View.SecondaryItemPicker.Hide();
        }

        public override bool HasSaveAction()
        {
            return true;
        }

        public override async UniTask<bool> OnSaveAsync()
        {
            if (CurrentCustomizableAvatar == null)
            {
                CrashReporter.Log($"CustomFlairColorCustomizationController's OnSaveAsync no assigned CurrentCustomizableAvatar. Returning false.", LogSeverity.Error);
                return false;
            }

            try
            {
                var colorType = flairColorDataSource.FlairAssetType switch
                {
                    FlairAssetType.Eyebrows => IColorType.Eyebrow,
                    FlairAssetType.Eyelashes => IColorType.Eyelash,
                    _ => throw new ArgumentOutOfRangeException()
                };

                var newColors = new List<Color> { _workingPreset.Colors[0], _workingPreset.Colors[1], _workingPreset.Colors[2], _workingPreset.Colors[3] };

                if (!string.IsNullOrEmpty(_workingPreset.Id) && (await GetAllCustomFlairIdsAsync(colorType)).Contains(_workingPreset.Id))
                {
                    await flairColorDataSource.UserColorSource.UpdateUserColorAsync(_workingPreset.Id, newColors);
                }
                else
                {
                    await flairColorDataSource.UserColorSource.CreateUserColorAsync(colorType, newColors);
                }

                flairColorDataSource.Dispose();
                await flairColorDataSource.InitializeAndGetCountAsync(null, new());

                return true;
            }
            catch (Exception e)
            {
                CrashReporter.Log($"CustomFlairColorCustomizationController's OnSaveAsync with CurrentCustomColorViewState {CurrentCustomColorViewState} had an exception: {e}", LogSeverity.Error);
                return true;
            }
        }

        private async UniTask<List<string>> GetAllCustomFlairIdsAsync(IColorType colorType)
        {
            var entries = await flairColorDataSource.UserColorSource.GetUserColorsAsync(colorType);
            return entries != null ? entries.Where(e => !string.IsNullOrEmpty(e.Id)).Select(e => e.Id).ToList() : new List<string>();
        }

        public override bool HasDiscardAction()
        {
            return true;
        }

        public override UniTask<bool> OnDiscardAsync()
        {
            if (_originalFlairColors != null && _originalFlairColors.Length >= 4)
            {
                var colors = AvatarFeatureColorItemPickerDataSource.MapToFlairColors(_originalFlairColors, flairColorDataSource.FlairAssetType);
                var command = new SetNativeAvatarColorsCommand(colors, CurrentCustomizableAvatar);
                command.ExecuteAsync(new CancellationTokenSource().Token);
            }

            flairColorDataSource.Dispose();
            return UniTask.FromResult(true);
        }

        public override void Dispose()
        {
            base.Dispose();
            _customizeColorViewInstance.Dispose();
        }

        private FlairColorPreset EditCustomColorFromPreset(GradientColorUiData uiData)
        {
            var colors = AvatarFeatureColorItemPickerDataSource.SafeGetColorsArray(uiData);
            var copyColors = new Color[colors.Length];
            Array.Copy(colors, copyColors, colors.Length);
            return new FlairColorPreset
            {
                Id = uiData.AssetId,
                Guid = uiData.AssetId,
                Colors = copyColors,
            };
        }

        private FlairColorPreset NewCustomColorFromPreset(GradientColorUiData uiData)
        {
            var colors = AvatarFeatureColorItemPickerDataSource.SafeGetColorsArray(uiData);
            var copyColors = new Color[colors.Length];
            Array.Copy(colors, copyColors, colors.Length);
            var guid = Guid.NewGuid().ToString();
            return new FlairColorPreset
            {
                Id = guid,
                Guid = $"{flairColorDataSource.FlairAssetType}-{guid}",
                Colors = copyColors,
            };
        }
    }
}
