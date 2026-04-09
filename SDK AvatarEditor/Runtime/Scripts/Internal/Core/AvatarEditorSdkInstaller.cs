using System;
using System.Collections.Generic;
using Genies.AvatarEditor.Core;
using Genies.Avatars.Services;
using Genies.ServiceManagement;
using VContainer;

namespace Genies.Sdk.AvatarEditor.Core
{
    /// <summary>
    /// Installer for Avatar Editor SDK services.
    /// Registers the Avatar Editor SDK service and UserColorSource with the service manager.
    /// Requires <see cref="AvatarSdkInstaller"/> to ensure Avatar SDK services are installed first.
    /// </summary>
    internal class AvatarEditorSdkInstaller : IGeniesInstaller,
        IRequiresInstaller<AvatarSdkInstaller>
    {
        private AvatarSdkInstaller AvatarSdkInstallerOverride { get; set; } = new();

        public int OperationOrder => AvatarSdkInstallerOverride.OperationOrder + 1;

        public void Install(IContainerBuilder builder)
        {
            Register();
        }

        public void Register()
        {
            new AvatarEditorSdkService().RegisterSelf().As<IAvatarEditorSdkService>();
            new UserColorSource().RegisterSelf().As<IUserColorSource>();
        }

        public IEnumerable<IGeniesInstaller> GetRequiredInstallers()
        {
            return new IGeniesInstaller[] { AvatarSdkInstallerOverride };
        }
    }
}
