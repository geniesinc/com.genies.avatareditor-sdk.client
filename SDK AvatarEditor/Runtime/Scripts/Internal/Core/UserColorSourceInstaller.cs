using Genies.AvatarEditor.Core;
using Genies.Avatars.Services;
using Genies.ServiceManagement;
using VContainer;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Registers <see cref="UserColorSource"/> as <see cref="IUserColorSource"/> with the ServiceManager singleton container.
    /// Called from <see cref="AvatarEditorSDK.InitializeAsync"/> after <see cref="IAvatarCustomizationService"/> is registered.
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class UserColorSourceInstaller : IGeniesInstaller
#else
    public class UserColorSourceInstaller : IGeniesInstaller
#endif
    {
        public void Install(IContainerBuilder builder)
        {
            Register();
        }

        /// <summary>
        /// Registers <see cref="UserColorSource"/> with the ServiceManager singleton container.
        /// </summary>
        public void Register()
        {
            new UserColorSource().RegisterSelf().As<IUserColorSource>();
        }
    }
}
