using System;

namespace zavod.Execution;

public static class RuntimeProfileResolver
{
    public static RuntimeProfile ResolveDefault()
    {
        return RuntimeProfileCatalog.Default;
    }

    public static RuntimeProfile ResolveByProfileId(string? profileId)
    {
        return string.IsNullOrWhiteSpace(profileId)
            ? ResolveDefault()
            : RuntimeProfileCatalog.GetRequired(profileId);
    }

    public static RuntimeProfile ResolveByFamily(RuntimeFamily? family)
    {
        return family is null
            ? ResolveDefault()
            : RuntimeProfileCatalog.GetDefaultFor(family.Value);
    }

    public static RuntimeProfile Resolve(RuntimeProfile? explicitProfile, string? profileId = null, RuntimeFamily? family = null)
    {
        if (explicitProfile is not null)
        {
            explicitProfile.Validate();
            return explicitProfile.Normalize();
        }

        if (!string.IsNullOrWhiteSpace(profileId))
        {
            return ResolveByProfileId(profileId);
        }

        if (family is not null)
        {
            return ResolveByFamily(family);
        }

        return ResolveDefault();
    }
}
