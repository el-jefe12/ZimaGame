using Godot;

[GlobalClass]
public partial class EatAction : ItemAction
{
    public override ItemActionResult Execute(Node player, ItemInstance item)
    {
        GD.Print("EatAction: Execute called.");

        if (player == null)
        {
            GD.PrintErr("EatAction: Player is null.");
            return ItemActionResult.None;
        }

        if (item == null)
        {
            GD.PrintErr("EatAction: Item is null.");
            return ItemActionResult.None;
        }

        if (item.Definition == null)
        {
            GD.PrintErr("EatAction: Item definition is null.");
            return ItemActionResult.None;
        }

        GD.Print($"EatAction: Trying to eat {item.Definition.ItemName}");

        if (item.Definition.ConsumableData == null)
        {
            GD.PrintErr($"EatAction: Item {item.Definition.ItemName} has no ConsumableData.");
            return ItemActionResult.None;
        }

        if (player is not PlayerController p)
        {
            GD.PrintErr($"EatAction: Player is not PlayerController. Actual type: {player.GetType().Name}");
            return ItemActionResult.None;
        }

        ItemConsumableData data = item.Definition.ConsumableData;

        PlayerNeedsSystem needs = p.GetNodeOrNull<PlayerNeedsSystem>("%PlayerNeeds");
        PlayerHealthSystem health = p.GetNodeOrNull<PlayerHealthSystem>("%PlayerHealth");

        if (needs == null)
        {
            GD.PrintErr("EatAction: PlayerNeedsSystem not found on player.");
            return ItemActionResult.None;
        }

        needs.SetHunger(needs.Hunger + data.HungerRestore, true);
        needs.SetThirst(needs.Thirst + data.ThirstRestore, true);
        needs.SetTired(needs.Tired + data.TiredRestore, true);

        if (health != null)
            health.SetHealth(health.Health + data.HealthRestore);
        else
            GD.PrintErr("EatAction: PlayerHealthSystem not found on player.");

        GD.Print($"{item.Definition.ItemName} eaten.");

        return ItemActionResult.RemoveItem |
               ItemActionResult.RefreshPrompt;
    }
}