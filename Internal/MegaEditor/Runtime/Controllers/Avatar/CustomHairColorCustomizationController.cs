using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Avatars.Behaviors;
using Genies.CameraSystem;
using Genies.CrashReporting;
using Genies.Customization.Framework;
using Genies.Looks.Customization.Commands;
using Genies.MegaEditor;
using Genies.Models;
using Genies.Naf;
using Genies.ServiceManagement;
using Genies.Ugc;
using Genies.Ugc.CustomHair;
using Genies.UIFramework.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;


namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Controller for the customize color view.
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class CustomHairColorCustomizationController : BaseCustomizationController
#else
    public class CustomHairColorCustomizationController : BaseCustomizationController
#endif
    {
        [SerializeField]
        private CustomizeColorView _prefab;
        private CustomizeColorView _customizeColorViewInstance;

        [SerializeField]
        public AvatarFeatureColorItemPickerDataSource hairColorDataSource;

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

        private VirtualCameraController<GeniesVirtualCameraCatalog> VirtualCameraController =>  ServiceManager.Get<VirtualCameraController<GeniesVirtualCameraCatalog>>();

        private HairColorService HairColorService => this.GetService<HairColorService>();

        // skin color data to be used by the service
        private CustomHairColorData _editedData;

        // Store the original hair colors for reverting
        private Color[] _originalHairColors;

        public override async UniTask<bool> TryToInitialize(Customizer customizer)
        {
            _customizer = customizer;

            if (_customizeColorViewInstance == null)
            {
                var viewDesc = (hairColorDataSource.ColorPresetType == ColorPresetType.Hair)
                    ? "custom-hair-color-view"
                    : "custom-facialhair-color-view";
                _customizeColorViewInstance = _customizer.View.GetOrCreateViewInLayer(viewDesc,
                    CustomizerViewLayer.CustomizationEditorFullScreen, _prefab);
            }

            hairColorDataSource.Initialize(_customizer);

            return await UniTask.FromResult(true);
        }

        public override void StartCustomization()
        {
            VirtualCameraController.ActivateVirtualCamera(virtualCamera).Forget();

            PictureInPictureController.canBeDisabled = false;
            PictureInPictureController.Enable();
            VirtualCameraController.SetFullScreenModeInFocusCameras(true);

            hairColorDataSource.StartCustomization();

            // Initialize the customize color view with the current hair colors
            Color[] initialColors = (hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?
            new Color[]
            {
                CurrentCustomizableAvatar.GetColor(GenieColor.HairBase) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.HairR) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.HairG) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.HairB) ?? Color.white,
            } :
            new Color[]
            {
                CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairBase) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairR) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairG) ?? Color.white,
                CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairB) ?? Color.white,
            };

            // Store the original colors for potential revert
            _originalHairColors = new Color[initialColors.Length];
            Array.Copy(initialColors, _originalHairColors, initialColors.Length);

            _customizeColorViewInstance.Initialize(initialColors);

            // Add listener to the color picker color change event for regions
            _customizeColorViewInstance.OnColorSelected.AddListener(UpdateAvatarHairColor);

            // Force one-time avatar skin color update to the initial color
            UpdateAvatarHairColor(initialColors[0]);
        }

        public override void StopCustomization()
        {
            // Remove the color change listener to prevent memory leaks
            _customizeColorViewInstance.OnColorSelected.RemoveListener(UpdateAvatarHairColor);

            PictureInPictureController.canBeDisabled = true;
            PictureInPictureController.Disable();
            VirtualCameraController.SetFullScreenModeInFocusCameras(false);

            CurrentCustomColorViewState = CustomColorViewState.Normal;

            hairColorDataSource.StopCustomization();
        }

        public override bool HasSaveAction()
        {
            return true;
        }

        public override async UniTask<bool> OnSaveAsync()
        {
            if (CurrentCustomizableAvatar == null)
            {
                CrashReporter.Log($"CustomHairColorCustomizationController's OnSaveAsync no assigned CurrentCustomizableAvatar. Returning false.", LogSeverity.Error);

                return false;
            }

            try
            {
                // if we have a selected item, clicking this save button will override its color data.
                // else it will create a new color data, whose id can be null (cloud save will auto assign a guid to it).
                switch (CurrentCustomColorViewState)
                {
                    case CustomColorViewState.Edit:
                        // Load the existing data to preserve all properties for proper updating
                        _editedData = await HairColorService.CustomColorDataAsync(hairColorDataSource.CurrentHairColorId);
                        if (_editedData == null)
                        {
                            // Fallback: create new if somehow the existing data is not found
                            _editedData = new CustomHairColorData();
                            _editedData.Id = hairColorDataSource.CurrentHairColorId;
                        }
                        break;

                    case CustomColorViewState.CreateNew:
                        _editedData = new CustomHairColorData();
                        _editedData.Id = "";

                        break;

                    case CustomColorViewState.Normal:
                    default:
                        return true;
                }

                if (hairColorDataSource.ColorPresetType == ColorPresetType.Hair)
                {
                    _editedData.ColorBase = CurrentCustomizableAvatar.GetColor(GenieColor.HairBase) ?? Color.black;
                    _editedData.ColorR = CurrentCustomizableAvatar.GetColor(GenieColor.HairR) ?? Color.black;
                    _editedData.ColorG = CurrentCustomizableAvatar.GetColor(GenieColor.HairG) ?? Color.black;
                    _editedData.ColorB = CurrentCustomizableAvatar.GetColor(GenieColor.HairB) ?? Color.black;
                }
                else
                {
                    _editedData.ColorBase = CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairBase) ?? Color.black;
                    _editedData.ColorR = CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairR) ?? Color.black;
                    _editedData.ColorG = CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairG) ?? Color.black;
                    _editedData.ColorB = CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairB) ?? Color.black;
                }

                CustomHairColorData savedData = await HairColorService.CreateOrUpdateCustomHair(_editedData);

                if (savedData != null)
                {
                    hairColorDataSource.CurrentHairColorId = savedData.Id;

                    var newColors = (hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?
                    new GenieColorEntry[]
                    {
                        new(GenieColor.HairBase, _editedData.ColorBase), new(GenieColor.HairR, _editedData.ColorR),
                        new(GenieColor.HairG, _editedData.ColorG), new(GenieColor.HairB, _editedData.ColorB),
                    }:
                    new GenieColorEntry[]
                    {
                        new(GenieColor.FacialhairBase, _editedData.ColorBase), new(GenieColor.FacialhairR, _editedData.ColorR),
                        new(GenieColor.FacialhairG, _editedData.ColorG), new(GenieColor.FacialhairB, _editedData.ColorB),
                    };

                    ICommand command = new SetNativeAvatarColorsCommand(newColors, CurrentCustomizableAvatar);
                    await command.ExecuteAsync(new CancellationTokenSource().Token);
                }

                // Refresh the data provider to show updated custom colors (similar to flair colors)
                hairColorDataSource.Dispose();
                await hairColorDataSource.InitializeAndGetCountAsync(null, new());

                return true;
            }
            catch (Exception e)
            {
                CrashReporter.Log($"CustomHairColorCustomizationController's OnSaveAsync with CurrentCustomColorViewState {CurrentCustomColorViewState} had an exception: {e}", LogSeverity.Error);

                return true;
            }
        }

        /// <summary>
        /// Updates the skin color of the avatar without using the command pattern.
        /// </summary>
        /// <param name="color">the given skin color</param>
        private void UpdateAvatarHairColor(Color color)
        {
            switch (_customizeColorViewInstance.ColorRegionsView.SelectedRegionIndex)
            {
                case 0:
                    CurrentCustomizableAvatar.SetColorAsync((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairBase:GenieColor.FacialhairBase, color).Forget();
                    break;
                case 1:
                    CurrentCustomizableAvatar.SetColorAsync((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairR:GenieColor.FacialhairR, color).Forget();
                    break;
                case 2:
                    CurrentCustomizableAvatar.SetColorAsync((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairG:GenieColor.FacialhairG, color).Forget();
                    break;
                case 3:
                    CurrentCustomizableAvatar.SetColorAsync((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairB:GenieColor.FacialhairB, color).Forget();
                    break;
            }
        }

        public override bool HasDiscardAction()
        {
            return true;
        }

        public override UniTask<bool> OnDiscardAsync()
        {
            // Revert to the original hair colors using SetNativeAvatarColorsCommand (similar to flair colors)
            if (_originalHairColors != null && _originalHairColors.Length >= 4)
            {
                var colors = new GenieColorEntry[]
                {
                    new ((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairBase:GenieColor.FacialhairBase, _originalHairColors[0]),
                    new ((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairR:GenieColor.FacialhairR,    _originalHairColors[1]),
                    new ((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairG:GenieColor.FacialhairG,    _originalHairColors[2]),
                    new ((hairColorDataSource.ColorPresetType == ColorPresetType.Hair)?GenieColor.HairB:GenieColor.FacialhairB,    _originalHairColors[3]),
                };

                ICommand command = new SetNativeAvatarColorsCommand(colors, CurrentCustomizableAvatar);
                command.ExecuteAsync(new CancellationTokenSource().Token);
            }

            hairColorDataSource.Dispose();

            // Also reset the data source ID
            var previousHairColorId = hairColorDataSource.PreviousHairColorId;
            hairColorDataSource.CurrentHairColorId = previousHairColorId;

            return UniTask.FromResult(true);
        }

        public override void Dispose()
        {
            base.Dispose();
            _customizeColorViewInstance.Dispose();
        }
    }
}
