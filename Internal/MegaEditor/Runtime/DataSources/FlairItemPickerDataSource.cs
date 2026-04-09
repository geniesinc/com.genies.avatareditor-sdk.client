using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Analytics;
using Genies.Assets.Services;
using Genies.CrashReporting;
using Genies.Customization.Framework.ItemPicker;
using Genies.Inventory;
using Genies.Inventory.UIData;
using Genies.Looks.Customization.Commands;
using Genies.Models;
using Genies.Naf;
using Genies.PerformanceMonitoring;
using Genies.Refs;
using Genies.ServiceManagement;
using Genies.UI.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;

namespace Genies.Customization.MegaEditor
{
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "FlairItemPickerDataSource", menuName = "Genies/Customizer/DataSource/FlairItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class FlairItemPickerDataSource : CustomizationItemPickerDataSource
#else
    public class FlairItemPickerDataSource : CustomizationItemPickerDataSource
#endif
    {
        [SerializeField]
        private List<ExtraItemPickerSettings> _extraItemPickerSettings;

        [SerializeField]
        private string _presetGuidPriority;

        [SerializeField]
        private string _nonePresetId;

        [SerializeField]
        private FlairAssetType _flairCategory;

        [SerializeField]
        public AvatarFeatureColorItemPickerDataSource flairColorDataSource;

        public FlairAssetType FlairCategory => _flairCategory;

        public static readonly Dictionary<FlairAssetType, Dictionary<AnalyticsActionType, string>> AnalyticsEventsPerFlairType =
            new Dictionary<FlairAssetType, Dictionary<AnalyticsActionType, string>>()
            {
                { FlairAssetType.Eyebrows, new Dictionary<AnalyticsActionType, string>()
                {
                    { AnalyticsActionType.EnterCategory, CustomizationAnalyticsEvents.EyeBrowCategorySelected },
                    { AnalyticsActionType.PresetSelected, CustomizationAnalyticsEvents.EyeBrowPresetClickEvent },
                    { AnalyticsActionType.ColorPresetSelected, CustomizationAnalyticsEvents.EyeBrowColorPresetClickEvent },
                    { AnalyticsActionType.ColorPickerSelected, CustomizationAnalyticsEvents.EyeBrowColorPickerClickEvent },
                }},
                { FlairAssetType.Eyelashes, new Dictionary<AnalyticsActionType, string>()
                {
                    { AnalyticsActionType.EnterCategory, CustomizationAnalyticsEvents.EyelashCategorySelected },
                    { AnalyticsActionType.PresetSelected, CustomizationAnalyticsEvents.EyeLashPresetClickEvent },
                    { AnalyticsActionType.ColorPresetSelected, CustomizationAnalyticsEvents.EyeLashColorPresetClickEvent },
                    { AnalyticsActionType.ColorPickerSelected, CustomizationAnalyticsEvents.EyeLashColorPickerClickEvent },
                }},
            };

#if GENIES_SDK && !GENIES_INTERNAL
        internal enum AnalyticsActionType
#else
        public enum AnalyticsActionType
#endif
        {
            EnterCategory = 1,
            PresetSelected = 2,
            ColorPresetSelected = 3,
            ColorPickerSelected = 4,
        }

        /// <summary>
        /// Fired when an item is clicked. (assetId, wasSelected). Controller can use for span/chaos reset.
        /// </summary>
        public event Action<string, bool> OnItemClicked;

        public override bool HasMoreItems => _uiProvider?.HasMoreData ?? false;
        public override bool IsLoadingMore => _uiProvider?.IsLoadingMore ?? false;

        private string _stringSubcategory => _flairCategory.ToString().ToLower();

        protected override void ConfigureProvider()
        {
            if (_uiProvider == null)
            {
                var config = UIDataProviderConfigs.DefaultAvatarFlairConfig;
                SetUIProvider(config, ServiceManager.Get<IAssetsService>());
            }
        }

        protected override string GetAssetTypeString()
        {
            return _stringSubcategory;
        }

        protected override void OnAfterInitialized()
        {
            _defaultCellSize = new Vector2(88, 96);
        }

        public override void StartCustomization()
        {
        }

        public override void StopCustomization()
        {
        }

        public override async UniTask<int> InitializeAndGetCountAsync(int? pageSize, CancellationToken token)
        {
            var count = await base.InitializeAndGetCountAsync(pageSize, token);
            if (_ids != null)
            {
                _ids = ReorderIdsWithPriority(_ids);
            }
            return count;
        }

        private List<string> ReorderIdsWithPriority(List<string> ids)
        {
            if (ids == null || ids.Count == 0)
            {
                return ids;
            }

            var orderedList = new List<string>(ids);

            if (!string.IsNullOrEmpty(_presetGuidPriority) && ids.Contains(_presetGuidPriority))
            {
                orderedList.Remove(_presetGuidPriority);
                orderedList.Insert(0, _presetGuidPriority);
            }

            return orderedList;
        }

        public override int GetCurrentSelectedIndex()
        {
            return GetCurrentSelectedIndexBase(CurrentCustomizableAvatar.IsAssetEquipped);
        }

        /// <summary>
        /// Returns the asset id of the currently equipped flair item, or null if none.
        /// </summary>
        public string GetCurrentEquippedAssetId()
        {
            var idx = GetCurrentSelectedIndex();
            if (idx < 0 || _ids == null || idx >= _ids.Count)
            {
                return null;
            }
            return _ids[idx];
        }

        public override bool ItemSelectedIsValidForProcessCTA()
        {
            var equippedId = _ids?.FirstOrDefault(CurrentCustomizableAvatar.IsAssetEquipped);
            return !string.Equals(equippedId, _nonePresetId);
        }

        public override async UniTask<bool> LoadMoreItemsAsync(CancellationToken cancellationToken)
        {
            if (_uiProvider == null || !HasMoreItems || IsLoadingMore)
            {
                return false;
            }

            try
            {
                var provider = GetUIProvider<DefaultInventoryAsset, BasicInventoryUiData>();
                if (provider == null)
                {
                    return false;
                }

                var newItemsList = await _uiProvider.LoadMoreAsync(categories: new List<string> { _stringSubcategory }, subcategory: null).AttachExternalCancellation(cancellationToken);
                var newItems = newItemsList?.Cast<BasicInventoryUiData>().ToList() ?? new List<BasicInventoryUiData>();

                if (newItems.Count == 0)
                {
                    return false;
                }

                _ids = await _uiProvider.GetAllAssetIds(categories: new List<string> { _stringSubcategory }, subcategory: null).AttachExternalCancellation(cancellationToken) ?? new List<string>();
                _ids = ReorderIdsWithPriority(_ids);

                return true;
            }
            catch (OperationCanceledException)
            {
                _ids = null;
                return false;
            }
            catch (Exception e)
            {
                CrashReporter.Log($"{GetType().Name}'s LoadMoreItemsAsync failed: {e}", LogSeverity.Error);
                return false;
            }
        }

        public override ItemPickerCellView GetCellPrefab(int index)
        {
            if (_extraItemPickerSettings == null)
            {
                return _defaultCellView;
            }

            var item = _extraItemPickerSettings.Find(e => e.StaticIndexOnList == index);
            return item != null ? item.ExtraItemPicker : _defaultCellView;
        }

        private async UniTask<Ref<BasicInventoryUiData>> GetDataForIndexAsync(int index)
        {
            if (_extraItemPickerSettings != null && _extraItemPickerSettings.Exists(e => e.StaticIndexOnList == index))
            {
                return CreateRef.From<BasicInventoryUiData>(null);
            }

            return await GetDataForIndexBaseAsync<DefaultInventoryAsset, BasicInventoryUiData>(index, "FlairItemPickerDataSource");
        }

        public override ItemPickerCtaConfig GetCtaConfig()
        {
            if (_flairCategory != FlairAssetType.Eyelashes)
            {
                return new ItemPickerCtaConfig(ctaType: CTAButtonType.CustomizeCTA, noneSelectedDelegate: CustomizeSelectedAsync);
            }

            return null;
        }

        private UniTask<bool> CustomizeSelectedAsync(CancellationToken cancellationToken)
        {
            switch (_flairCategory)
            {
                case FlairAssetType.Eyebrows:
                    CurrentDnaCustomizationViewState = AvatarBaseCategory.Brow;
                    break;
                default:
                    CrashReporter.LogError($"Invalid Customization Selection {_flairCategory}");
                    break;
            }
            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.ChaosFaceCustomSelectEvent);
            _customizer.GoToEditItemNode();
            return UniTask.FromResult(true);
        }

        public override async UniTask<bool> OnItemClickedAsync(int index, ItemPickerCellView clickedCell, bool wasSelected, CancellationToken cancellationToken)
        {
            if (_extraItemPickerSettings != null && _extraItemPickerSettings.Exists(e => e.StaticIndexOnList == index))
            {
                throw new NotImplementedException();
            }

            if (TryGetLoadedData<BasicInventoryUiData>(index, out var data) is false)
            {
                return false;
            }

            OnItemClicked?.Invoke(data.Item.AssetId, wasSelected);

            if (wasSelected && data.Item.IsEditable)
            {
                _customizer.GoToEditItemNode();
                return true;
            }

            var command = new EquipNativeAvatarAssetCommand(data.Item.AssetId, CurrentCustomizableAvatar);
            await command.ExecuteAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var props = new AnalyticProperties();
            props.AddProperty("flairAssetName", $"{data.Item?.AssetId}");
            AnalyticsReporter.LogEvent(AnalyticsEventsPerFlairType[_flairCategory][AnalyticsActionType.PresetSelected], props);

            _customizer.RegisterCommand(command);

            return true;
        }

        public override async UniTask<bool> InitializeCellViewAsync(ItemPickerCellView view, int index, bool isSelected, CancellationToken cancellationToken)
        {
            if (_extraItemPickerSettings != null && _extraItemPickerSettings.Exists(e => e.StaticIndexOnList == index))
            {
                view.SetState(ItemCellState.Initialized);
                view.SetDebuggingAssetLabel(_ids?[index]);
                return true;
            }

            var dataRef = await GetDataForIndexAsync(index);

            if (dataRef.Item == null || cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            if (view is InventoryPickerCellView inventoryView)
            {
                if (dataRef is Ref<BasicInventoryUiData> basicData)
                {
                    inventoryView.thumbnail.sprite = basicData.Item.Thumbnail.Item;
                    inventoryView.SetIsEditable(basicData.Item.IsEditable && _customizer.HasEditingNode == true);
                }
                inventoryView.SetDebuggingAssetLabel(dataRef.Item.AssetId);
                inventoryView.SetAssetName(dataRef.Item.DisplayName);
                return true;
            }

            if (view is GenericItemPickerCellView genericView)
            {
                if (dataRef is Ref<BasicInventoryUiData> basicData)
                {
                    genericView.thumbnail.sprite = basicData.Item.Thumbnail.Item;
                    genericView.SetIsEditable(basicData.Item.IsEditable && _customizer.HasEditingNode == true);
                }
                genericView.SetDebuggingAssetLabel(dataRef.Item.AssetId);
                return true;
            }

            return false;
        }

        public override void Dispose()
        {
            base.Dispose();
        }
    }
}
