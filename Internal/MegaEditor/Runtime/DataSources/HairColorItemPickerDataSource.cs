using UnityEngine;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Data source for hair color picker. Uses <see cref="AvatarFeatureColorItemPickerDataSource"/> with category Hair.
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "HairColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/HairColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class HairColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#else
    public class HairColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#endif
    {
        protected override void ConfigureProvider()
        {
            SetCategoryAndConfigureProvider(AvatarFeatureColorCategory.Hair);
        }
    }
}
