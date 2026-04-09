using UnityEngine;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Data source for eyelash color picker. Uses <see cref="AvatarFeatureColorItemPickerDataSource"/> with category FlairEyelash.
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "EyelashColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/EyelashColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class EyelashColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#else
    public class EyelashColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#endif
    {
        protected override void ConfigureProvider()
        {
            SetCategoryAndConfigureProvider(AvatarFeatureColorCategory.FlairEyelash);
        }
    }
}
