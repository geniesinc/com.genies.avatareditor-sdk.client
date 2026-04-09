using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Genies.Analytics;
using Genies.Avatars.Behaviors;
using Genies.Customization.Framework;
using Genies.Customization.Framework.ItemPicker;
using Genies.Inventory.UIData;
using Genies.Models;
using Genies.Looks.Customization.Commands;
using Genies.Naf;
using Genies.PerformanceMonitoring;
using Genies.Refs;
using Genies.ServiceManagement;
using Genies.UI.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Handles customizing the avatar flairs (eyebrows, eyelashes, etc..)
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "FlairCustomizationController", menuName = "Genies/Customizer/Controllers/Flair Customization Controller")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class FlairCustomizationController : BaseCustomizationController
#else
    public class FlairCustomizationController : BaseCustomizationController
#endif
    {
        [SerializeField]
        public FlairItemPickerDataSource flairItemDataSource;

        [SerializeField]
        public AvatarFeatureColorItemPickerDataSource flairColorDataSource;

        /// <summary>
        /// The focus camera to activate and set as active on this customization controller.
        /// </summary>
        [SerializeField]
        private GeniesVirtualCameraCatalog _virtualCamera = GeniesVirtualCameraCatalog.FullBodyFocusCamera;

        /// <summary>
        /// Connected Chaos customization node's controller
        /// Used to reset all custom vector values when equipped preset has changed
        /// </summary>
        [SerializeField]
        private FaceVectorCustomizationController _chaosCustomizer;

        private CustomInstrumentationManager _InstrumentationManager => CustomInstrumentationManager.Instance;
        private static string _RootTransactionName => CustomInstrumentationOperations.CreateNewLookTransaction;
        private string _categorySpan;
        private string _previousSpan;

        public override UniTask<bool> TryToInitialize(Customizer customizer)
        {
            _customizer = customizer;

            flairItemDataSource.Initialize(_customizer);
            flairColorDataSource.Initialize(_customizer);

            return UniTask.FromResult(true);
        }

        public override void StartCustomization()
        {
            _categorySpan = _InstrumentationManager.StartChildSpanUnderTransaction(_RootTransactionName,
                nameof(FlairCustomizationController), $"open face - {flairItemDataSource.FlairCategory.ToString().ToLower()} category");

            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.FlairCustomizationStarted);
            ActivateCamera();

            flairItemDataSource.StartCustomization();
            _customizer.View.PrimaryItemPicker.Show(flairItemDataSource).Forget();

            flairColorDataSource.SetCategoryAndConfigureProvider(flairItemDataSource.FlairCategory);
            flairColorDataSource.StartCustomization();
            ShowSecondaryPicker(flairColorDataSource);
            ScrollToSelectedItemInSecondaryPicker(flairColorDataSource).Forget();

            AddListeners();

            var currentAssetEquipped = flairItemDataSource.GetCurrentEquippedAssetId();
            var props = new AnalyticProperties();
            if (currentAssetEquipped != null)
            {
                props.AddProperty("flairAssetName", currentAssetEquipped);
            }
            AnalyticsReporter.LogEvent(FlairItemPickerDataSource.AnalyticsEventsPerFlairType[flairItemDataSource.FlairCategory][FlairItemPickerDataSource.AnalyticsActionType.EnterCategory], props);
        }

        private void AddListeners()
        {
            _customizer.View.EditOrDeleteController.OnEditClicked += EditCustomColorData;
            _customizer.View.EditOrDeleteController.OnDeleteClicked += DeleteCustomColorData;
            _customizer.View.SecondaryItemPicker.OnScroll += CloseEditOrDeleteButtonsWhenCrossingLeftMargin;
            flairItemDataSource.OnItemClicked += OnFlairItemClicked;
        }

        private void RemoveListeners()
        {
            _customizer.View.EditOrDeleteController.OnEditClicked -= EditCustomColorData;
            _customizer.View.EditOrDeleteController.OnDeleteClicked -= DeleteCustomColorData;
            _customizer.View.SecondaryItemPicker.OnScroll -= CloseEditOrDeleteButtonsWhenCrossingLeftMargin;
            flairItemDataSource.OnItemClicked -= OnFlairItemClicked;
        }

        private void OnFlairItemClicked(string assetId, bool wasSelected)
        {
            var currentPoseSpan = _InstrumentationManager.StartChildSpanUnderSpan(_categorySpan, assetId, $"flair asset id");
            _InstrumentationManager.FinishChildSpan(_previousSpan);
            _previousSpan = currentPoseSpan;

            if (!wasSelected && _chaosCustomizer != null)
            {
                _chaosCustomizer.ResetAllValues();
            }
        }

        private void EditCustomColorData()
        {
            CurrentCustomColorViewState = CustomColorViewState.Edit;
            _customizer.GoToCreateItemNode();
        }

        private async void DeleteCustomColorData()
        {
            var deletedDataId = flairColorDataSource.CurrentLongPressColorData?.AssetId;

            _customizer.View.EditOrDeleteController.DisableAndDeactivateButtons().Forget();

            var nextIndexToEquip = flairColorDataSource.CurrentLongPressIndex + 1;
            Ref<GradientColorUiData> nextUiDataRef = await flairColorDataSource.GetDataForIndexAsync(nextIndexToEquip);

            flairColorDataSource.CurrentLongPressColorData = nextUiDataRef.Item;

            ICommand command = new SetNativeAvatarColorsCommand(GetColors(flairColorDataSource), CurrentCustomizableAvatar);

            await command.ExecuteAsync();

            if (deletedDataId != null)
            {
                await flairColorDataSource.UserColorSource.DeleteUserColorAsync(deletedDataId);

                flairColorDataSource.Dispose();
                await flairColorDataSource.InitializeAndGetCountAsync(null, new System.Threading.CancellationToken());

                ShowSecondaryPicker(flairColorDataSource);
            }
        }

        private static GenieColorEntry[] GetColors(AvatarFeatureColorItemPickerDataSource source)
        {
            var colors = AvatarFeatureColorItemPickerDataSource.SafeGetColorsArray(source.CurrentLongPressColorData);
            return AvatarFeatureColorItemPickerDataSource.MapToFlairColors(colors, source.FlairAssetType);
        }

        public override void StopCustomization()
        {
            _InstrumentationManager.FinishChildSpan(_previousSpan);
            _InstrumentationManager.FinishChildSpan(_categorySpan);
            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.FlairCustomizationStopped);
            ResetCamera();

            RemoveListeners();

            _customizer.View.EditOrDeleteController.DeactivateButtonsImmediately();

            _customizer.View.PrimaryItemPicker.Hide();
            HideSecondaryPicker();

            flairItemDataSource.StopCustomization();
            flairColorDataSource.StopCustomization();
        }

        private void CloseEditOrDeleteButtonsWhenCrossingLeftMargin()
        {
            var editOrDeleteController = _customizer.View.EditOrDeleteController;
            if (editOrDeleteController.IsActive && editOrDeleteController.transform.localPosition.x < -120)
            {
                editOrDeleteController.DeactivateButtonsImmediately();
            }
        }

        public override void OnUndoRedo()
        {
            _InstrumentationManager.FinishChildSpan(_previousSpan);
            _customizer.View.PrimaryItemPicker.RefreshSelection().Forget();
            _customizer.View.SecondaryItemPicker.RefreshSelection().Forget();
        }

        public override void Dispose()
        {
            base.Dispose();

            _categorySpan = null;
            _previousSpan = null;

            flairItemDataSource.Dispose();
            flairColorDataSource.Dispose();
        }

        private void ActivateCamera()
        {
            CurrentVirtualCameraController.ActivateVirtualCamera(_virtualCamera).Forget();
        }

        private void ResetCamera()
        {
            CurrentVirtualCameraController.ActivateVirtualCamera(GeniesVirtualCameraCatalog.FullBodyFocusCamera).Forget();
        }
    }
}
