namespace CamoSwitcher.Core;

public enum AimGameMode { Both, Multiplayer, Zombies }
public enum AimTargetBone { Head, UpperChest, CenterMass }
public enum AimActivation { AimButton, FireButton }

// Configuration only. These values do not activate or redirect shots in a game.
public sealed record SilentAimSettings(
    int FieldOfView = 15,
    int HitChance = 100,
    int MaxDistance = 100,
    AimTargetBone TargetBone = AimTargetBone.Head,
    AimActivation Activation = AimActivation.AimButton,
    AimGameMode GameMode = AimGameMode.Both,
    bool VisibleOnly = true)
{
    public static SilentAimSettings Default => new();

    public static void Validate(SilentAimSettings? settings)
    {
        if (settings is null || settings.FieldOfView is < 1 or > 180 ||
            settings.HitChance is < 0 or > 100 || settings.MaxDistance is < 1 or > 1000 ||
            !Enum.IsDefined(settings.TargetBone) || !Enum.IsDefined(settings.Activation) || !Enum.IsDefined(settings.GameMode))
            throw new InvalidDataException("Silent aim settings contain an invalid value.");
    }
}
