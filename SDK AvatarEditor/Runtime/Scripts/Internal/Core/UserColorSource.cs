using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Genies.Avatars.Customization;
using Genies.Avatars.Services;
using Genies.ServiceManagement;
using UnityEngine;

namespace Genies.AvatarEditor.Core
{
    /// <summary>
    /// Adapts <see cref="IAvatarCustomizationService"/> to <see cref="IUserColorSource"/> for use by different
    /// ColorItemPickerDataSources and color services. Exposes all user color methods (get, create, update, delete).
    /// </summary>
#if GENIES_SDK && !GENIES_INTERNAL
    internal class UserColorSource : IUserColorSource
#else
    public class UserColorSource : IUserColorSource
#endif
    {
        private IAvatarCustomizationService AvatarCustomizationService => ServiceManager.Get<IAvatarCustomizationService>();

        private static UserColorType IColorTypeToUserColorType(IColorType colorType)
        {
            return colorType switch
            {
                IColorType.Hair => UserColorType.Hair,
                IColorType.Eyebrow => UserColorType.Eyebrow,
                IColorType.Eyelash => UserColorType.Eyelash,
                IColorType.Skin => UserColorType.Skin,
                IColorType.FacialHair => UserColorType.FacialHair,
                _ => throw new System.ArgumentException($"Unknown ColorType: {colorType}")
            };
        }

        public async UniTask<List<UserColorEntry>> GetUserColorsAsync(IColorType color, CancellationToken cancellationToken = default)
        {
            var service = AvatarCustomizationService;
            if (service == null)
            {
                return new List<UserColorEntry>();
            }

            var userColorType = IColorTypeToUserColorType(color);
            var iColors = await service.GetUserColorsAsync(userColorType, cancellationToken);
            if (iColors == null || iColors.Count == 0)
            {
                return new List<UserColorEntry>();
            }

            var result = new List<UserColorEntry>(iColors.Count);
            foreach (var iColor in iColors)
            {
                var id = iColor.InstanceId;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }
                var colors = ExpandHexesToFour(iColor.Hexes);
                result.Add(new UserColorEntry { Id = id, Colors = colors });
            }
            return result;
        }

        public async UniTask<UserColorEntry?> GetUserColorByIdAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            var service = AvatarCustomizationService;
            if (service == null || string.IsNullOrEmpty(instanceId))
            {
                return null;
            }

            var all = await service.GetUserColorsByCategoryAsync(null, cancellationToken);
            if (all == null)
            {
                return null;
            }
            var found = all.FirstOrDefault(c => c.InstanceId == instanceId);
            if (found == null)
            {
                return null;
            }
            var colors = ColorsHexToArray(found.ColorsHex);
            return new UserColorEntry { Id = found.InstanceId, Colors = colors };
        }

        public async UniTask<UserColorEntry?> CreateUserColorAsync(IColorType colorType, List<Color> colors, CancellationToken cancellationToken = default)
        {
            var service = AvatarCustomizationService;
            if (service == null || colors == null || colors.Count == 0)
            {
                return null;
            }

            var userColorType = IColorTypeToUserColorType(colorType);
            var iColor = await service.CreateUserColorAsync(userColorType, colors, cancellationToken);
            if (iColor == null)
            {
                return null;
            }
            var id = iColor.InstanceId;
            var colorArr = ExpandHexesToFour(iColor.Hexes);
            return new UserColorEntry { Id = id, Colors = colorArr };
        }

        public async UniTask UpdateUserColorAsync(string instanceId, List<Color> colors, CancellationToken cancellationToken = default)
        {
            var service = AvatarCustomizationService;
            if (service == null || string.IsNullOrEmpty(instanceId))
            {
                return;
            }
            await service.UpdateUserColorAsync(instanceId, colors ?? new List<Color>(), cancellationToken);
        }

        public async UniTask DeleteUserColorAsync(string instanceId, CancellationToken cancellationToken = default)
        {
            var service = AvatarCustomizationService;
            if (service == null || string.IsNullOrEmpty(instanceId))
            {
                return;
            }
            await service.DeleteUserColorAsync(instanceId, cancellationToken);
        }

        private static Color[] ExpandHexesToFour(Color[] hexes)
        {
            if (hexes == null || hexes.Length == 0)
            {
                var c = Color.black;
                return new[] { c, c, c, c };
            }
            var baseColor = hexes[0];
            var second = hexes.Length > 1 ? hexes[1] : baseColor;
            return new[] { baseColor, second, second, second };
        }

        private static Color[] ColorsHexToArray(List<string> colorsHex)
        {
            if (colorsHex == null || colorsHex.Count == 0)
            {
                var c = Color.black;
                return new[] { c, c, c, c };
            }
            var list = new List<Color>();
            foreach (var hex in colorsHex)
            {
                if (ColorUtility.TryParseHtmlString(hex, out var color))
                {
                    list.Add(color);
                }
            }
            if (list.Count >= 4)
            {
                return new[] { list[0], list[1], list[2], list[3] };
            }
            if (list.Count == 2)
            {
                return new[] { list[0], list[1], list[1], list[1] };
            }
            if (list.Count == 1)
            {
                var c = list[0];
                return new[] { c, c, c, c };
            }
            var black = Color.black;
            return new[] { black, black, black, black };
        }
    }
}
