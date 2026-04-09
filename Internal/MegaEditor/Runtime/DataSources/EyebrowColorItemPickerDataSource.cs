using UnityEngine;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Data source for eyebrow color picker. Uses <see cref="AvatarFeatureColorItemPickerDataSource"/> with category FlairEyebrow.
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "EyebrowColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/EyebrowColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class EyebrowColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#else
    public class EyebrowColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#endif
    {
        protected override void ConfigureProvider()
        {
            SetCategoryAndConfigureProvider(AvatarFeatureColorCategory.FlairEyebrow);
        }
    }
}
