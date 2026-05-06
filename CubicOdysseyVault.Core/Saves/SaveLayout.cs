namespace CubicOdysseyVault.Core.Saves;

public sealed record SaveLayout(
    IReadOnlyList<SaveAccount> Accounts,
    IReadOnlyList<SaveSlot> Slots);
