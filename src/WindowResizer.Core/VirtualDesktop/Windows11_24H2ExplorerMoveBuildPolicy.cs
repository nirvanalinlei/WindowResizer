using System;

namespace WindowResizer.Core.VirtualDesktop;

internal static class Windows11_24H2ExplorerMoveBuildPolicy
{
    public const int MinSupportedBuild = 26200;
    public const int MaxExclusiveSupportedBuild = 26900;

    public static bool Supports(Version osVersion)
    {
        if (osVersion is null)
        {
            throw new ArgumentNullException(nameof(osVersion));
        }

        return osVersion.Build >= MinSupportedBuild
            && osVersion.Build < MaxExclusiveSupportedBuild;
    }

    public static string BuildUnsupportedMessage(Version osVersion)
    {
        if (osVersion is null)
        {
            throw new ArgumentNullException(nameof(osVersion));
        }

        return $"Windows 11 24H2 Explorer move API is unsupported on build {osVersion.Build}.";
    }
}
