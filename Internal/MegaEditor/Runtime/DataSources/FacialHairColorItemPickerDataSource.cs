using UnityEngine;

namespace Genies.Customization.MegaEditor
{
    /// <summary>
    /// Data source for facial hair color picker. Uses <see cref="AvatarFeatureColorItemPickerDataSource"/> with category FacialHair.
    /// </summary>
#if GENIES_INTERNAL
    [CreateAssetMenu(fileName = "FacialHairColorItemPickerDataSource", menuName = "Genies/Customizer/DataSource/FacialHairColorItemPickerDataSource")]
#endif
#if GENIES_SDK && !GENIES_INTERNAL
    internal class FacialHairColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#else
    public class FacialHairColorItemPickerDataSource : AvatarFeatureColorItemPickerDataSource
#endif
    {
        protected override void ConfigureProvider()
        {
            SetCategoryAndConfigureProvider(AvatarFeatureColorCategory.FacialHair);
        }
    }
}
