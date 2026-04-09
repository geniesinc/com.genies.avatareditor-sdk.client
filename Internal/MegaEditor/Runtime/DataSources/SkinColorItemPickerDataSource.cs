using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Analytics;
using Genies.Assets.Services;
using Genies.Avatars;
using Genies.CrashReporting;
using Genies.Customization.Framework;
using Genies.Customization.Framework.ItemPicker;
using Genies.Inventory;
using Genies.Inventory.UIData;
using Genies.Looks.Customization.Commands;
using Genies.Naf;
using Genies.Refs;
using Genies.ServiceManagement;
using Genies.Ugc;
using Genies.Ugc.CustomSkin;
using Genies.UI.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;

namespace Genies.Customization.MegaEditor
{
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "SkinColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/SkinColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class SkinColorItemPickerDataSource : ColorItemPickerDataSource
#else
    public class SkinColorItemPickerDataSource : ColorItemPickerDataSource
#endif
    {
        /// <summary>
        /// Allows overriding layout config from <see cref="SkinColorCustomizationController"/> with OverrideWithCustomLayoutConfig.
        /// </summary>
        private ItemPickerLayoutConfig _overrideLayoutConfig;

        /// <summary>
        /// The event name to dispatch to analytics
        /// </summary>
        protected override string ColorAnalyticsEventName => CustomizationAnalyticsEvents.SkinColorPresetClickEvent;

        private SkinColorService _skinColorService => this.GetService<SkinColorService>();

        /// <summary>
        /// Override the default layout config with a custom one (e.g. from SkinColorCustomizationController.customLayoutConfig).
        /// </summary>
        public void OverrideWithCustomLayoutConfig(ItemPickerLayoutConfig layoutConfig)
        {
            _overrideLayoutConfig = layoutConfig;
        }

        /// <summary>
        /// Default Skin Color loaded from ColorAsset with the Id to be <see cref="UnifiedDefaults.DefaultSkinColor"/>
        /// </summary>
        private SkinColorData _defaultSkinColorData;
        public SkinColorData PreviousSkinColorData { get; private set; }
        public SkinColorData CurrentSkinColorData { get; set; }

        public List<string> CustomIds;
        private List<string> _presetIds;

        protected override void ConfigureProvider()
        {
            if (_uiProvider == null)
            {
                var config = UIDataProviderConfigs.SkinColorPresetsConfig;
                SetUIProvider(config, ServiceManager.Get<IAssetsService>());
            }
        }

        protected override string GetAssetTypeString()
        {
            return "skin";
        }

        protected override async UniTask<List<string>> GetCustomIdsAsync(CancellationToken token)
        {
            CustomIds = await _skinColorService.GetAllCustomSkinIdsAsync();
            return CustomIds;
        }

        protected override async UniTask<List<string>> GetPresetIdsAsync(int? pageSize, CancellationToken token)
        {
            if (_uiProvider == null)
            {
                _presetIds = new List<string>();
                return _presetIds;
            }
            _presetIds = await _uiProvider.GetAllAssetIds(categories: new List<string>{ GetAssetTypeString() }, pageSize: pageSize) ?? new List<string>();
            return _presetIds;
        }

        public override async UniTask<int> InitializeAndGetCountAsync(int? pageSize, CancellationToken token)
        {
            int count = await base.InitializeAndGetCountAsync(pageSize, token);

            // Preload all data so we can show which color is selected
            for (int i = 0; i < _ids.Count; i++)
            {
                await GetDataForIndexAsync(i);
            }

            return count;
        }

        public override void StartCustomization()
        {
            CurrentSkinColorData = ColorAssetToSkinColorData(CurrentCustomizableAvatar.GetColor(GenieColor.Skin) ?? Color.black);
            PreviousSkinColorData = CurrentSkinColorData;

            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.ColorPresetCustomizationStarted);
        }

        public override void StopCustomization()
        {
            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.ColorPresetCustomizationStopped);
        }

        public override ItemPickerCtaConfig GetCtaConfig()
        {
            CTAButtonType buttonType = CTAButtonType.SingleCreateNewCTA;
            return new ItemPickerCtaConfig(
                ctaType: buttonType,
                horizontalLayoutCtaOverride: _Cta,
                createNewAction: OnCreateNew);
        }

        private void OnCreateNew()
        {
            Dispose();
            CurrentCustomColorViewState = CustomColorViewState.CreateNew;

            // Store previous color for discarding
            PreviousSkinColorData = CurrentSkinColorData;

            _customizer.GoToCreateItemNode();
        }

        public override ItemPickerLayoutConfig GetLayoutConfig()
        {
            return new ItemPickerLayoutConfig()
            {
                horizontalOrVerticalLayoutConfig =
                    new HorizontalOrVerticalLayoutConfig()
                    {
                        padding = new RectOffset(16, 16, 28, 28),
                        spacing = 12,
                    },
                gridLayoutConfig = new GridLayoutConfig()
                {
                    cellSize = new Vector2(56, 56),
                    columnCount = 5,
                    padding = new RectOffset(16, 16, 24, 8),
                    spacing = new Vector2(16, 16),
                },
            };
        }

        /// <summary>
        /// Gets which skin color UI is selected by comparing the actual color values.
        /// </summary>
        /// <remarks>Compares the avatar's current skin color with each preset's color to find a match.
        /// Falls back to base implementation (IsAssetEquipped check) if data isn't cached yet.</remarks>
        /// <returns>the index of the UI item. -1 if none is selected.</returns>
        public override int GetCurrentSelectedIndex()
        {
            var currentSkinColor = CurrentCustomizableAvatar.GetColor(GenieColor.Skin) ?? Color.black;

            // Use base helper method to find matching color
            var index = GetCurrentSelectedIndexByColor<SimpleColorUiData>(
                currentSkinColor,
                data => data.InnerColor);

            // Fallback to base implementation (asset ID check) if no match found
            return index >= 0 ? index : base.GetCurrentSelectedIndex();
        }

        /// <summary>
        /// Get cached data if exists else load a new ref.
        /// </summary>
        /// <param name="index"> Item index </param>
        public override async UniTask<Ref<SimpleColorUiData>> GetDataForIndexAsync(int index)
        {
            if (TryGetLoadedData<SimpleColorUiData>(index, out var data))
            {
                return data;
            }

            if (index < 0 || _ids == null)
            {
                return default;
            }

            // Ensure _ids is populated (normally happens in InitializeAndGetCountAsync)
            if(_ids.Count == 0)
            {
                try
                {
                    // Fetch ids
                    var ids = await GetPresetIdsAsync(pageSize: null, CancellationToken.None);

                    // Fetch optional custom ids
                    var customIds = await GetCustomIdsAsync(CancellationToken.None);

                    // Combine if any custom ids exist
                    if (customIds is { Count: > 0 })
                    {
                        var ordered = new List<string>(customIds);
                        ordered.AddRange(ids);
                        _ids = ordered;
                    }
                    else
                    {
                        _ids = ids;
                    }
                }
                catch (OperationCanceledException)
                {
                    _ids = new();
                }
            }

            if (index >= _ids.Count)
            {
                return default;
            }
            var id = _ids[index];
            SimpleColorUiData uiData;

            if (CustomIds != null && CustomIds.Contains(id))
            {
                try
                {
                    using var colorRef = await _skinColorService.GetSkinColorForIdAsync(id);
                    if (colorRef.IsAlive && colorRef.Item != null)
                    {
                        var customColorData = colorRef.Item;
                        uiData = new SimpleColorUiData(
                            assetId: id,
                            displayName: null,
                            category: null,
                            subCategory: null,
                            order: 0,
                            description: null,
                            isEditable: true,
                            innerColor: customColorData.BaseColor,
                            middleColor: customColorData.BaseColor,
                            outerColor: customColorData.BaseColor,
                            borderValue: 0.0f
                        );
                    }
                    else
                    {
                        uiData = new SimpleColorUiData(
                            assetId: id,
                            displayName: null,
                            category: null,
                            subCategory: null,
                            order: 0,
                            description: null,
                            isEditable: false,
                            innerColor: Color.black,
                            middleColor: Color.black,
                            outerColor: Color.black,
                            borderValue: 0.0f
                        );
                    }
                }
                catch (Exception ex)
                {
                    CrashReporter.LogError($"Failed to load custom skin color data for ID {id}: {ex.Message}");

                    uiData = new SimpleColorUiData(
                        assetId: id,
                        displayName: null,
                        category: null,
                        subCategory: null,
                        order: 0,
                        description: null,
                        isEditable: false,
                        innerColor: Color.black,
                        middleColor: Color.black,
                        outerColor: Color.black,
                        borderValue: 0.0f
                    );
                }
            }
            else
            {
                uiData = await GetUIProvider<ColoredInventoryAsset, SimpleColorUiData>().GetDataForAssetId(id);
            }

            var newDataRef = CreateRef.FromDependentResource(uiData);
            _loadedData ??= new();
            _loadedData[index] = newDataRef;

            return newDataRef;
        }

        /// <summary>
        /// Business logic when a cell is clicked. If already selected, triggers long-press (edit); otherwise equips the skin color.
        /// </summary>
        public override async UniTask<bool> OnItemClickedAsync(int index, ItemPickerCellView clickedCell, bool wasSelected, CancellationToken cancellationToken)
        {
            if (TryGetLoadedData<SimpleColorUiData>(index, out var dataRef) is false)
            {
                return false;
            }

            if (!dataRef.IsAlive || dataRef.Item == null)
            {
                return false;
            }

            var longPressCellView = clickedCell as LongPressCellView;
            if (longPressCellView != null && _editOrDeleteController.IsActive)
            {
                _editOrDeleteController.DisableAndDeactivateButtons().Forget();
            }

            if (wasSelected)
            {
                OnLongPress(longPressCellView);
                return true;
            }

            clickedCell.ToggleSelected(true);
            clickedCell.Index = index;

            ICommand command = await CreateEquipCommandAsync(dataRef.Item, cancellationToken);
            if (command == null)
            {
                return false;
            }

            await command.ExecuteAsync(cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var props = new AnalyticProperties();
            props.AddProperty("name", dataRef.Item.DisplayName ?? dataRef.Item.AssetId);
            AnalyticsReporter.LogEvent(ColorAnalyticsEventName, props);

            _customizer.RegisterCommand(command);
            return true;
        }

        /// <summary>
        /// Initialize the cell view when visible. Wires long-press and sets thumbnail/editable state.
        /// </summary>
        public override async UniTask<bool> InitializeCellViewAsync(ItemPickerCellView view, int index, bool isSelected, CancellationToken cancellationToken)
        {
            var result = await base.InitializeCellViewAsync(view, index, isSelected, cancellationToken);
            if (!result)
            {
                return false;
            }

            if (isSelected && TryGetLoadedData<SimpleColorUiData>(index, out var dataRef) && dataRef.IsAlive && dataRef.Item != null)
            {
                _currentLongPressColorData = dataRef.Item;
            }

            return true;
        }

        /// <summary>
        /// Long-press: show edit/delete only for custom (non-preset) skin colors that are not currently equipped.
        /// </summary>
        protected override async void OnLongPress(LongPressCellView longPressCellView)
        {
            if (longPressCellView == null || longPressCellView.Index < 0)
            {
                return;
            }

            if (_currentLongPressCell == longPressCellView && _editOrDeleteController.IsActive)
            {
                return;
            }

            _currentLongPressCell = longPressCellView;

            var longPressColorDataRef = await GetDataForIndexAsync(_currentLongPressCell.Index);
            _currentLongPressColorData = longPressColorDataRef.Item;

            if (_currentLongPressColorData == null)
            {
                return;
            }

            if (_presetIds != null && _presetIds.Contains(_currentLongPressColorData.AssetId))
            {
                return;
            }

            if (IsColorEquipped(_currentLongPressColorData.AssetId))
            {
                return;
            }

            if (!_currentLongPressColorData.IsEditable)
            {
                return;
            }

            OnLongPressBeforeEnableEdit(_currentLongPressColorData);
            await _editOrDeleteController.Enable(_currentLongPressCell.gameObject);
        }

        /// <summary>
        /// Creates the command to equip the skin color.
        /// </summary>
        protected override UniTask<ICommand> CreateEquipCommandAsync(SimpleColorUiData colorData, CancellationToken cancellationToken)
        {
            CurrentSkinColorData = new SkinColorData { BaseColor = colorData.InnerColor };

            // Update avatar skin color
            return UniTask.FromResult<ICommand>(new EquipSkinColorCommand(colorData.InnerColor, CurrentCustomizableAvatar));
        }

        /// <summary>
        /// Checks if a skin color is currently equipped using EquippedSkinColorIds.
        /// </summary>
        protected override bool IsColorEquipped(string assetId)
        {
            return EquippedSkinColorIds.Contains(assetId);
        }

        /// <summary>
        /// Called before enabling edit on long press. Sets the current skin color data.
        /// </summary>
        protected override void OnLongPressBeforeEnableEdit(SimpleColorUiData colorData)
        {
            if (colorData?.InnerColor != null)
            {
                CurrentSkinColorData = new SkinColorData { BaseColor = colorData.InnerColor, Id = colorData.AssetId};
            }
        }



        private static SkinColorData ColorAssetToSkinColorData(Color color)
        {
            return new SkinColorData(){ BaseColor = color,};
        }

    }
}
