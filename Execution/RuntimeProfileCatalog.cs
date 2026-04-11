using System;
using System.Collections.Generic;
using System.Linq;

namespace zavod.Execution;

public static class RuntimeProfileCatalog
{
    private static readonly IReadOnlyList<RuntimeProfile> Profiles = new[]
    {
        new RuntimeProfile(
            "local-unsafe",
            RuntimeFamily.LocalUnsafe,
            RuntimeIsolationLevel.None,
            UsesSandbox: false,
            TrustedOnly: true,
            "Direct local host runtime for trusted development scenarios."),
        RuntimeProfile.ScopedLocalDefault,
        new RuntimeProfile(
            "container-heavy",
            RuntimeFamily.Container,
            RuntimeIsolationLevel.Container,
            UsesSandbox: true,
            TrustedOnly: false,
            "Container runtime for heavy isolation and risky toolchains."),
        new RuntimeProfile(
            "vm-sandbox-hard",
            RuntimeFamily.VmOrSandbox,
            RuntimeIsolationLevel.VirtualMachine,
            UsesSandbox: true,
            TrustedOnly: false,
            "VM or sandbox runtime for hard isolation scenarios."),
        new RuntimeProfile(
            "remote-ephemeral",
            RuntimeFamily.Remote,
            RuntimeIsolationLevel.RemoteEphemeral,
            UsesSandbox: true,
            TrustedOnly: false,
            "Remote ephemeral runtime for detached or long-running execution.")
    }
    .Select(static profile => profile.Normalize())
    .ToArray();

    public static RuntimeProfile Default => GetRequired("scoped-local-default");

    public static IReadOnlyList<RuntimeProfile> ListAll() => Profiles;

    public static RuntimeProfile? TryGet(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        return Profiles.FirstOrDefault(profile =>
            string.Equals(profile.ProfileId, profileId.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public static RuntimeProfile GetRequired(string profileId)
    {
        var profile = TryGet(profileId);
        if (profile is null)
        {
            throw new InvalidOperationException($"Unknown runtime profile: {profileId}.");
        }

        return profile;
    }

    public static RuntimeProfile GetDefaultFor(RuntimeFamily family)
    {
        return family switch
        {
            RuntimeFamily.LocalUnsafe => GetRequired("local-unsafe"),
            RuntimeFamily.ScopedLocalWorkspace => Default,
            RuntimeFamily.Container => GetRequired("container-heavy"),
            RuntimeFamily.VmOrSandbox => GetRequired("vm-sandbox-hard"),
            RuntimeFamily.Remote => GetRequired("remote-ephemeral"),
            _ => throw new InvalidOperationException($"Unsupported runtime family: {family}.")
        };
    }
}
