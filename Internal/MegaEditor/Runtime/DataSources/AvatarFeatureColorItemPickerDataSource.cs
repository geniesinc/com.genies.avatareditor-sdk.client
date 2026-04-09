using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Analytics;
using Genies.Assets.Services;
using Genies.Avatars;
using Genies.Avatars.Services;
using Genies.CrashReporting;
using Genies.Customization.Framework;
using Genies.Customization.Framework.ItemPicker;
using Genies.Inventory;
using Genies.Inventory.UIData;
using Genies.Looks.Customization.Commands;
using Genies.Models;
using Genies.Naf;
using Genies.Refs;
using Genies.ServiceManagement;
using Genies.Ugc.CustomHair;
using Genies.UI.Widgets;
using UnityEngine;
using static Genies.Customization.MegaEditor.CustomizationContext;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Category for avatar feature color pickers: flair (eyebrow/eyelash) or hair (hair/facial hair).
    /// One data source can serve any of these via inspector category or SetCategory at runtime.
    /// </summary>
    public enum AvatarFeatureColorCategory
    {
        FlairEyebrow,
        FlairEyelash,
        Hair,
        FacialHair,
    }

    /// <summary>
    /// Unified data source for gradient (4-channel) avatar feature colors: flair (eyebrows, eyelashes)
    /// and hair (hair, facial hair). Replaces FlairColorItemPickerDataSource and HairColorItemPickerDataSource
    /// with a single class driven by <see cref="AvatarFeatureColorCategory"/>.
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "AvatarFeatureColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/AvatarFeatureColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class AvatarFeatureColorItemPickerDataSource : CustomizationItemPickerDataSource
#else
    public class AvatarFeatureColorItemPickerDataSource : CustomizationItemPickerDataSource
#endif
    {
        [SerializeField] private NoneOrNewCTAController _Cta;

        [SerializeField]
        private AvatarFeatureColorCategory _category = AvatarFeatureColorCategory.FlairEyebrow;

        private string _colorAnalyticsEventName;

        private IUserColorSource _userColorSource => this.GetService<IUserColorSource>();
        private HairColorService _hairColorService => this.GetService<HairColorService>();

        private LongPressCellView _currentLongPressCell;
        private GradientColorUiData _currentLongPressColorData;

        /// <summary> When using IUserColorSource (Flair), cache (id -> 4 colors) for GetDataForIndexAsync. </summary>
        private Dictionary<string, Color[]> _cachedUserFlairColorsById;

        protected virtual Shader _ColorShader => Shader.Find("Genies/ColorPresetIcon");
        private const float Border = -1.81f;
        private static readonly int s_border = Shader.PropertyToID("_Border");
        private static readonly int s_innerColor = Shader.PropertyToID("_InnerColor");
        private static readonly int s_midColor = Shader.PropertyToID("_MidColor");

        public List<string> CustomIds;
        private List<string> _presetIds;

        // --- Public API for controllers (compatibility with Flair / Hair) ---

        public GradientColorUiData CurrentLongPressColorData
        {
            get => _currentLongPressColorData;
            set => _currentLongPressColorData = value;
        }

        public int CurrentLongPressIndex => _currentLongPressCell != null ? _currentLongPressCell.Index : -1;

        /// <summary> For Flair: previous color for discard. For Hair: not used. </summary>
        public GradientColorUiData PreviousPresetColor { get; private set; }

        /// <summary> For Hair: previous/current color id for customize/discard. For Flair: not used. </summary>
        public string PreviousHairColorId;

        public string CurrentHairColorId { get; set; }

        /// <summary> For Flair: exposed for CustomFlairColorCustomizationController. Null when category is Hair. </summary>
        public IUserColorSource UserColorSource => IsFlairCategory ? _userColorSource : null;

        public ColorPresetType ColorPresetType => CategoryToColorPresetType(_category);
        public FlairAssetType FlairAssetType => CategoryToFlairAssetType(_category);

        private bool IsFlairCategory =>
            _category == AvatarFeatureColorCategory.FlairEyebrow || _category == AvatarFeatureColorCategory.FlairEyelash;

        private bool IsHairCategory =>
            _category == AvatarFeatureColorCategory.Hair || _category == AvatarFeatureColorCategory.FacialHair;

        /// <summary>
        /// Set category at runtime (e.g. from FlairCustomizationController for Eyebrows/Eyelashes).
        /// </summary>
        public void SetCategoryAndConfigureProvider(AvatarFeatureColorCategory category)
        {
            _category = category;
            _uiProvider = null;
            ConfigureProviderCore();
        }

        /// <summary>
        /// Set category from FlairAssetType for compatibility with existing Flair controllers.
        /// </summary>
        public void SetCategoryAndConfigureProvider(FlairAssetType flairCategory)
        {
            _category = flairCategory switch
            {
                FlairAssetType.Eyebrows => AvatarFeatureColorCategory.FlairEyebrow,
                FlairAssetType.Eyelashes => AvatarFeatureColorCategory.FlairEyelash,
                _ => throw new ArgumentOutOfRangeException(nameof(flairCategory), flairCategory, null)
            };
            _uiProvider = null;
            ConfigureProviderCore();
        }

        protected override void ConfigureProvider()
        {
            ConfigureProviderCore();
        }

        private void ConfigureProviderCore()
        {
            if (_uiProvider != null)
            {
                return;
            }

            switch (_category)
            {
                case AvatarFeatureColorCategory.FlairEyebrow:
                    _uiProvider = new InventoryUIDataProvider<ColoredInventoryAsset, GradientColorUiData>(
                        UIDataProviderConfigs.FlairEyebrowColorPresetsConfig,
                        ServiceManager.Get<IAssetsService>());
                    break;
                case AvatarFeatureColorCategory.FlairEyelash:
                    _uiProvider = new InventoryUIDataProvider<ColoredInventoryAsset, GradientColorUiData>(
                        UIDataProviderConfigs.FlairEyelashColorPresetsConfig,
                        ServiceManager.Get<IAssetsService>());
                    break;
                case AvatarFeatureColorCategory.Hair:
                    _hairColorService.SetCategory(ColorPresetType.Hair);
                    SetUIProvider<ColoredInventoryAsset, GradientColorUiData>(
                        UIDataProviderConfigs.HairColorPresetsConfig,
                        ServiceManager.Get<IAssetsService>());
                    break;
                case AvatarFeatureColorCategory.FacialHair:
                    _hairColorService.SetCategory(ColorPresetType.FacialHair);
                    SetUIProvider<ColoredInventoryAsset, GradientColorUiData>(
                        UIDataProviderConfigs.FacialHairColorPresetsConfig,
                        ServiceManager.Get<IAssetsService>());
                    break;
                default:
                    CrashReporter.LogError($"Invalid AvatarFeatureColorCategory {_category}");
                    break;
            }
        }

        protected override string GetAssetTypeString()
        {
            return _category switch
            {
                AvatarFeatureColorCategory.FlairEyebrow => Models.ColorPresetType.FlairEyebrow.ToString().ToLower(),
                AvatarFeatureColorCategory.FlairEyelash => Models.ColorPresetType.FlairEyelash.ToString().ToLower(),
                AvatarFeatureColorCategory.Hair => "hair",
                AvatarFeatureColorCategory.FacialHair => "facialhair",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected override async UniTask<List<string>> GetCustomIdsAsync(CancellationToken token)
        {
            if (IsFlairCategory)
            {
                var colorType = _category == AvatarFeatureColorCategory.FlairEyebrow ? IColorType.Eyebrow : IColorType.Eyelash;
                CustomIds ??= new List<string>();
                CustomIds.Clear();
                var userEntries = await _userColorSource
                    .GetUserColorsAsync(colorType, token)
                    .AttachExternalCancellation(token);
                if (userEntries != null)
                {
                    _cachedUserFlairColorsById = new Dictionary<string, Color[]>();
                    foreach (var entry in userEntries)
                    {
                        if (!string.IsNullOrEmpty(entry.Id))
                        {
                            CustomIds.Add(entry.Id);
                            if (entry.Colors != null && entry.Colors.Length >= 2)
                            {
                                _cachedUserFlairColorsById[entry.Id] = entry.Colors;
                            }
                        }
                    }
                }
                return CustomIds;
            }

            CustomIds = await _hairColorService.GetAllCustomHairIdsAsync();
            return CustomIds ?? new List<string>();
        }

        protected override async UniTask<List<string>> GetPresetIdsAsync(int? pageSize, CancellationToken token)
        {
            if (_uiProvider == null)
            {
                _presetIds = new List<string>();
                return _presetIds;
            }
            _presetIds = await _uiProvider.GetAllAssetIds(
                categories: new List<string> { GetAssetTypeString() },
                pageSize: pageSize) ?? new List<string>();
            return _presetIds;
        }

        public override void StartCustomization()
        {
            if (IsFlairCategory)
            {
                PreviousPresetColor = null;
            }
            else
            {
                PreviousHairColorId = CurrentHairColorId;
                CurrentHairColorId = GetEquippedColorId();
                if (CurrentHairColorId == null)
                {
                    CurrentHairColorId = PreviousHairColorId;
                }

                _colorAnalyticsEventName = _category == AvatarFeatureColorCategory.FacialHair
                    ? CustomizationAnalyticsEvents.FacialHairColorPresetClickEvent
                    : CustomizationAnalyticsEvents.HairColorPresetClickEvent;

                AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.ColorPresetCustomizationStarted);
            }
        }

        public override void StopCustomization()
        {
            AnalyticsReporter.LogEvent(CustomizationAnalyticsEvents.ColorPresetCustomizationStopped);
        }

        public override ItemPickerCtaConfig GetCtaConfig()
        {
            return new ItemPickerCtaConfig(
                ctaType: CTAButtonType.SingleCreateNewCTA,
                horizontalLayoutCtaOverride: _Cta,
                createNewAction: OnCreateNew);
        }

        private void OnCreateNew()
        {
            Dispose();
            CurrentCustomColorViewState = CustomColorViewState.CreateNew;

            if (IsFlairCategory)
            {
                PreviousPresetColor = CurrentLongPressColorData;
            }
            else
            {
                PreviousHairColorId = CurrentHairColorId;
            }

            _customizer.GoToCreateItemNode();
        }

        public override ItemPickerLayoutConfig GetLayoutConfig()
        {
            return new ItemPickerLayoutConfig()
            {
                horizontalOrVerticalLayoutConfig = new HorizontalOrVerticalLayoutConfig()
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

        public override int GetCurrentSelectedIndex()
        {
            if (_ids == null || _ids.Count == 0)
            {
                return -1;
            }

            Color[] currentColors = GetCurrentColorsForCategory();
            var index = GetCurrentSelectedIndexByGradientColors(
                currentColors,
                SafeGetColorsArray,
                GetDataForIndexAsync);

            if (index >= 0)
            {
                return index;
            }

            if (IsHairCategory)
            {
                var equippedId = GetEquippedColorId();
                if (!string.IsNullOrEmpty(equippedId))
                {
                    for (var i = 0; i < _ids.Count; i++)
                    {
                        if (_ids[i] == equippedId)
                        {
                            return i;
                        }
                    }
                }
            }

            return GetCurrentSelectedIndexBase(CurrentCustomizableAvatar.IsAssetEquipped);
        }

        public override async UniTask<int> InitializeAndGetCountAsync(int? pageSize, CancellationToken token)
        {
            int count = await base.InitializeAndGetCountAsync(pageSize, token);
            for (int i = 0; i < _ids.Count; i++)
            {
                await GetDataForIndexAsync(i);
            }

            return count;
        }

        public async UniTask<Ref<GradientColorUiData>> GetDataForIndexAsync(int index)
        {
            if (TryGetLoadedData<GradientColorUiData>(index, out var data))
            {
                return data;
            }

            if (_ids == null || index < 0 || index >= _ids.Count)
            {
                return default;
            }

            var id = _ids[index];
            GradientColorUiData uiData;

            if (CustomIds != null && CustomIds.Contains(id))
            {
                if (IsFlairCategory)
                {
                    Color[] colors = null;
                    if (_cachedUserFlairColorsById != null && _cachedUserFlairColorsById.TryGetValue(id, out var cached))
                    {
                        colors = cached;
                    }
                    else if (_userColorSource != null)
                    {
                        try
                        {
                            var entry = await _userColorSource.GetUserColorByIdAsync(id);
                            if (entry.HasValue && entry.Value.Colors != null && entry.Value.Colors.Length >= 4)
                            {
                                colors = entry.Value.Colors;
                            }
                        }
                        catch (Exception ex)
                        {
                            CrashReporter.LogError($"Failed to load custom flair color data for ID {id}: {ex.Message}");
                        }
                    }

                    if (colors != null && colors.Length >= 4)
                    {
                        uiData = new GradientColorUiData(id, null, null, null, 0, true, colors[0], colors[1], colors[2], colors[3]);
                    }
                    else
                    {
                        uiData = new GradientColorUiData(id, null, null, null, 0, false, Color.black, Color.black, Color.black, Color.black);
                    }
                }
                else
                {
                    try
                    {
                        var customColorData = await _hairColorService.CustomColorDataAsync(id);
                        if (customColorData != null)
                        {
                            uiData = new GradientColorUiData(id, null, null, null, 0, true,
                                customColorData.ColorBase, customColorData.ColorR, customColorData.ColorG, customColorData.ColorB);
                        }
                        else
                        {
                            throw new Exception("Custom hair color data was null");
                        }
                    }
                    catch (Exception ex)
                    {
                        CrashReporter.LogError($"Failed to load custom hair color data for ID {id}: {ex.Message}");
                        uiData = new GradientColorUiData(id, null, null, null, 0, false, Color.black, Color.black, Color.black, Color.black);
                    }
                }
            }
            else
            {
                uiData = await GetUIProvider<ColoredInventoryAsset, GradientColorUiData>().GetDataForAssetId(id);
            }

            var newDataRef = CreateRef.FromDependentResource(uiData);
            _loadedData ??= new Dictionary<int, object>();
            _loadedData[index] = newDataRef;
            return newDataRef;
        }

        public override async UniTask<bool> OnItemClickedAsync(
            int index,
            ItemPickerCellView clickedCell,
            bool wasSelected,
            CancellationToken cancellationToken)
        {
            if (TryGetLoadedData<GradientColorUiData>(index, out var dataRef) is false)
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
            _currentLongPressColorData = dataRef.Item;

            if (IsHairCategory)
            {
                CurrentHairColorId = dataRef.Item.AssetId;
            }

            var colors = SafeGetColorsArray(dataRef.Item);
            var entries = MapToGenieColors(colors, _category);

            ICommand command = new SetNativeAvatarColorsCommand(entries, CurrentCustomizableAvatar);
            await command.ExecuteAsync(cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            _customizer.RegisterCommand(command);

            var props = new AnalyticProperties();
            if (IsFlairCategory)
            {
                props.AddProperty("flairAssetName", dataRef.Item.AssetId);
                AnalyticsReporter.LogEvent(
                    FlairItemPickerDataSource.AnalyticsEventsPerFlairType[FlairAssetType][FlairItemPickerDataSource.AnalyticsActionType.ColorPresetSelected],
                    props);
            }
            else
            {
                props.AddProperty("name", dataRef.Item.DisplayName ?? dataRef.Item.AssetId);
                AnalyticsReporter.LogEvent(_colorAnalyticsEventName, props);
            }

            return true;
        }

        public override async UniTask<bool> InitializeCellViewAsync(
            ItemPickerCellView view,
            int index,
            bool isSelected,
            CancellationToken cancellationToken)
        {
            var dataRef = await GetDataForIndexAsync(index);

            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var longPressCellView = view as LongPressCellView;
            if (longPressCellView != null && dataRef.IsAlive && dataRef.Item != null)
            {
                var colorUiData = dataRef.Item;

                if (longPressCellView.OnLongPress == null)
                {
                    longPressCellView.OnLongPress += OnLongPress;
                    if (longPressCellView.Index < 0)
                    {
                        longPressCellView.Index = index;
                    }
                }

                var colors = SafeGetColorsArray(colorUiData);
                longPressCellView.thumbnail.material = GetSwatchMaterial(colors[0]);

                bool isEquipped = IsColorEquipped(colorUiData.AssetId);
                longPressCellView.SetShowEditableIcon(colorUiData.IsEditable && !isEquipped);
                longPressCellView.SetDebuggingAssetLabel(colorUiData.AssetId);

                if (isSelected)
                {
                    _currentLongPressColorData = colorUiData;
                }
            }

            return true;
        }

        private async void OnLongPress(LongPressCellView longPressCellView)
        {
            if (_currentLongPressCell == longPressCellView && _editOrDeleteController.IsActive)
            {
                return;
            }

            if (longPressCellView == null || longPressCellView.Index < 0)
            {
                return;
            }

            _currentLongPressCell = longPressCellView;
            var longPressColorDataRef = await GetDataForIndexAsync(longPressCellView.Index);
            _currentLongPressColorData = longPressColorDataRef.Item;

            if (_currentLongPressColorData == null)
            {
                return;
            }

            if (_presetIds != null && _presetIds.Contains(_currentLongPressColorData.AssetId))
            {
                return;
            }

            if (IsHairCategory && EquippedHairColorIds.Contains(_currentLongPressColorData.AssetId))
            {
                return;
            }

            if (!_currentLongPressColorData.IsEditable)
            {
                return;
            }

            if (IsHairCategory)
            {
                CurrentHairColorId = _currentLongPressColorData.AssetId;
            }

            await _editOrDeleteController.Enable(_currentLongPressCell.gameObject);
        }

        public override void Dispose()
        {
            base.Dispose();
            _currentLongPressColorData = null;
            _cachedUserFlairColorsById?.Clear();
            _cachedUserFlairColorsById = null;
            CustomIds?.Clear();
            CustomIds = null;
            _presetIds?.Clear();
            _presetIds = null;
        }

        // --- Helpers ---

        private bool IsColorEquipped(string assetId)
        {
            if (IsHairCategory)
            {
                return EquippedHairColorIds.Contains(assetId);
            }
            return CurrentCustomizableAvatar.IsAssetEquipped(assetId);
        }

        private string GetEquippedColorId()
        {
            if (_ids == null)
            {
                return null;
            }
            for (var i = 0; i < _ids.Count; i++)
            {
                if (CurrentCustomizableAvatar.IsAssetEquipped(_ids[i]))
                {
                    return _ids[i];
                }
            }
            return null;
        }

        private Color[] GetCurrentColorsForCategory()
        {
            return _category switch
            {
                AvatarFeatureColorCategory.FlairEyebrow => new[]
                {
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyebrowsBase) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyebrowsR) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyebrowsG) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyebrowsB) ?? Color.black,
                },
                AvatarFeatureColorCategory.FlairEyelash => new[]
                {
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyelashesBase) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyelashesR) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyelashesG) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.EyelashesB) ?? Color.black,
                },
                AvatarFeatureColorCategory.Hair => new[]
                {
                    CurrentCustomizableAvatar.GetColor(GenieColor.HairBase) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.HairR) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.HairG) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.HairB) ?? Color.black,
                },
                AvatarFeatureColorCategory.FacialHair => new[]
                {
                    CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairBase) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairR) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairG) ?? Color.black,
                    CurrentCustomizableAvatar.GetColor(GenieColor.FacialhairB) ?? Color.black,
                },
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private static ColorPresetType CategoryToColorPresetType(AvatarFeatureColorCategory category)
        {
            return category switch
            {
                AvatarFeatureColorCategory.FlairEyebrow => ColorPresetType.FlairEyebrow,
                AvatarFeatureColorCategory.FlairEyelash => ColorPresetType.FlairEyelash,
                AvatarFeatureColorCategory.Hair => ColorPresetType.Hair,
                AvatarFeatureColorCategory.FacialHair => ColorPresetType.FacialHair,
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        private static FlairAssetType CategoryToFlairAssetType(AvatarFeatureColorCategory category)
        {
            return category switch
            {
                AvatarFeatureColorCategory.FlairEyebrow => FlairAssetType.Eyebrows,
                AvatarFeatureColorCategory.FlairEyelash => FlairAssetType.Eyelashes,
                _ => throw new InvalidOperationException($"Category {category} is not a Flair category.")
            };
        }

        public static Color[] SafeGetColorsArray(GradientColorUiData uiData)
        {
            if (uiData == null)
            {
                return new[] { Color.black, Color.black, Color.black, Color.black };
            }

            return uiData.GetColorsArray();
        }

        /// <summary>
        /// Map 4 colors to GenieColorEntry[] for the current category (equip command).
        /// </summary>
        public static GenieColorEntry[] MapToGenieColors(Color[] colors, AvatarFeatureColorCategory category)
        {
            return category switch
            {
                AvatarFeatureColorCategory.FlairEyebrow => new[]
                {
                    new GenieColorEntry(GenieColor.EyebrowsBase, colors[0]),
                    new GenieColorEntry(GenieColor.EyebrowsR, colors[1]),
                    new GenieColorEntry(GenieColor.EyebrowsG, colors[2]),
                    new GenieColorEntry(GenieColor.EyebrowsB, colors[3]),
                },
                AvatarFeatureColorCategory.FlairEyelash => new[]
                {
                    new GenieColorEntry(GenieColor.EyelashesBase, colors[0]),
                    new GenieColorEntry(GenieColor.EyelashesR, colors[1]),
                    new GenieColorEntry(GenieColor.EyelashesG, colors[2]),
                    new GenieColorEntry(GenieColor.EyelashesB, colors[3]),
                },
                AvatarFeatureColorCategory.Hair => new[]
                {
                    new GenieColorEntry(GenieColor.HairBase, colors[0]),
                    new GenieColorEntry(GenieColor.HairR, colors[1]),
                    new GenieColorEntry(GenieColor.HairG, colors[2]),
                    new GenieColorEntry(GenieColor.HairB, colors[3]),
                },
                AvatarFeatureColorCategory.FacialHair => new[]
                {
                    new GenieColorEntry(GenieColor.FacialhairBase, colors[0]),
                    new GenieColorEntry(GenieColor.FacialhairR, colors[1]),
                    new GenieColorEntry(GenieColor.FacialhairG, colors[2]),
                    new GenieColorEntry(GenieColor.FacialhairB, colors[3]),
                },
                _ => throw new ArgumentOutOfRangeException(nameof(category), category, null)
            };
        }

        /// <summary>
        /// For Flair controllers: map colors by FlairAssetType (compatibility with FlairColorItemPickerDataSource).
        /// </summary>
        public static GenieColorEntry[] MapToFlairColors(Color[] colors, FlairAssetType flairAssetType)
        {
            var category = flairAssetType == FlairAssetType.Eyebrows
                ? AvatarFeatureColorCategory.FlairEyebrow
                : AvatarFeatureColorCategory.FlairEyelash;
            return MapToGenieColors(colors, category);
        }

        /// <summary>
        /// For Hair controllers: map to hair channels (compatibility with HairColorItemPickerDataSource).
        /// </summary>
        public static GenieColorEntry[] MapToHairColors(Color[] colors)
        {
            return MapToGenieColors(colors, AvatarFeatureColorCategory.Hair);
        }

        /// <summary>
        /// For Hair controllers: map to facial hair channels (compatibility with HairColorItemPickerDataSource).
        /// </summary>
        public static GenieColorEntry[] MapToFacialHairColors(Color[] colors)
        {
            return MapToGenieColors(colors, AvatarFeatureColorCategory.FacialHair);
        }

        private Material GetSwatchMaterial(Color color)
        {
            var iconMaterial = new Material(_ColorShader);
            var mainColor = color;
            mainColor.a = 1f;
            iconMaterial.SetFloat(s_border, Border);
            iconMaterial.SetColor(s_innerColor, mainColor);
            iconMaterial.SetColor(s_midColor, Color.white);
            return iconMaterial;
        }
    }
}
