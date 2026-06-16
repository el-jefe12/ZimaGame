using Godot;

[GlobalClass]
public partial class AttackAction : ItemAction
{
    // Path from the player node to the AnimationPlayer.
    // Example:
    // PlayerHead/yawPivot/pitchPivot/cameraBob/CharacterCamera3D/WeaponsHolder/Weapons/AnimationPlayer
    [Export] public NodePath AnimationPlayerPath = "";

    // Path from the player node to the RayCast3D.
    // Example:
    // PlayerHead/yawPivot/pitchPivot/cameraBob/CharacterCamera3D/WeaponsHolder/ViewRayCast3D
    [Export] public NodePath AttackRaycastPath = "";

    // Animation that plays when attacking.
    [Export] public string AttackAnimationName = "knife_attack";

    // How much damage this attack deals.
    [Export] public float Damage = 10.0f;

    // Method name that damaged objects should have.
    // The hit object, or one of its parents, needs a method with this name.
    [Export] public string DamageMethodName = "TakeDamage";

    // Prevents attack spam.
    [Export] public float AttackCooldown = 0.4f;

    // If true, attack will not continue when AnimationPlayer is missing.
    [Export] public bool RequireAnimationPlayer = false;

    // If true, attack will not continue when RayCast3D is missing.
    [Export] public bool RequireRaycast = true;

    private bool _canAttack = true;

    public override ItemActionResult Execute(Node player, ItemInstance item)
    {
        GD.Print("AttackAction: Execute called.");

        if (!_canAttack)
        {
            GD.Print("AttackAction: Still on cooldown.");
            return ItemActionResult.None;
        }

        if (player == null)
        {
            GD.PrintErr("AttackAction: Player is null.");
            return ItemActionResult.None;
        }

        if (item == null)
        {
            GD.PrintErr("AttackAction: Item is null.");
            return ItemActionResult.None;
        }

        if (item.Definition == null)
        {
            GD.PrintErr("AttackAction: Item definition is null.");
            return ItemActionResult.None;
        }

        _canAttack = false;

        GD.Print($"AttackAction: Attacking with '{item.Definition.ItemName}'.");

        PlayAttackAnimation(player);
        TryDealDamage(player);

        StartCooldown(player);

        return ItemActionResult.RefreshPrompt;
    }

    private void PlayAttackAnimation(Node player)
    {
        if (AnimationPlayerPath == null || AnimationPlayerPath.IsEmpty)
        {
            GD.PrintErr("AttackAction: AnimationPlayerPath is empty.");

            if (RequireAnimationPlayer)
            {
                GD.PrintErr("AttackAction: Attack requires AnimationPlayer, but no path was set.");
            }

            return;
        }

        AnimationPlayer animationPlayer = player.GetNodeOrNull<AnimationPlayer>(AnimationPlayerPath);

        if (animationPlayer == null)
        {
            GD.PrintErr($"AttackAction: AnimationPlayer not found at path '{AnimationPlayerPath}' from '{player.GetPath()}'.");

            if (RequireAnimationPlayer)
            {
                GD.PrintErr("AttackAction: Attack requires AnimationPlayer, but it was not found.");
            }

            return;
        }

        if (string.IsNullOrWhiteSpace(AttackAnimationName))
        {
            GD.PrintErr("AttackAction: AttackAnimationName is empty.");
            return;
        }

        if (!animationPlayer.HasAnimation(AttackAnimationName))
        {
            GD.PrintErr($"AttackAction: AnimationPlayer does not have animation '{AttackAnimationName}'.");

            string[] animationList = animationPlayer.GetAnimationList();

            GD.Print("AttackAction: Available animations:");

            foreach (string animationName in animationList)
            {
                GD.Print($"- {animationName}");
            }

            return;
        }

        // Restart the animation every attack.
        animationPlayer.Stop(true);
        animationPlayer.Play(AttackAnimationName);

        GD.Print($"AttackAction: Playing animation '{AttackAnimationName}'.");
    }

    private void TryDealDamage(Node player)
    {
        if (AttackRaycastPath == null || AttackRaycastPath.IsEmpty)
        {
            GD.PrintErr("AttackAction: AttackRaycastPath is empty.");
            return;
        }

        RayCast3D raycast = player.GetNodeOrNull<RayCast3D>(AttackRaycastPath);

        if (raycast == null)
        {
            GD.PrintErr($"AttackAction: RayCast3D not found at path '{AttackRaycastPath}' from '{player.GetPath()}'.");

            if (RequireRaycast)
            {
                GD.PrintErr("AttackAction: Attack requires RayCast3D, but it was not found.");
            }

            return;
        }

        // Makes sure the raycast result is fresh this frame.
        raycast.ForceRaycastUpdate();

        if (!raycast.IsColliding())
        {
            GD.Print("AttackAction: Raycast hit nothing.");
            return;
        }

        GodotObject collider = raycast.GetCollider();

        if (collider == null)
        {
            GD.PrintErr("AttackAction: Raycast collider is null.");
            return;
        }

        Node hitNode = collider as Node;

        if (hitNode == null)
        {
            GD.PrintErr($"AttackAction: Collider is not a Node. Collider type: {collider.GetType().Name}");
            return;
        }

        GD.Print($"AttackAction: Raycast hit '{hitNode.Name}' at '{hitNode.GetPath()}'.");

        Node damageReceiver = FindDamageReceiver(hitNode);

        if (damageReceiver == null)
        {
            GD.Print($"AttackAction: Hit object has no '{DamageMethodName}' method.");
            return;
        }

        damageReceiver.Call(DamageMethodName, Damage);

        GD.Print($"AttackAction: Dealt {Damage} damage to '{damageReceiver.Name}'.");
    }

    private Node FindDamageReceiver(Node startNode)
    {
        Node currentNode = startNode;

        while (currentNode != null)
        {
            // Checks the hit node first, then its parents.
            if (currentNode.HasMethod(DamageMethodName))
            {
                return currentNode;
            }

            currentNode = currentNode.GetParent();
        }

        return null;
    }

    private async void StartCooldown(Node player)
    {
        SceneTree tree = player.GetTree();

        if (tree == null)
        {
            GD.PrintErr("AttackAction: SceneTree is null. Cooldown reset immediately.");
            _canAttack = true;
            return;
        }

        SceneTreeTimer timer = tree.CreateTimer(AttackCooldown);

        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);

        _canAttack = true;

        GD.Print("AttackAction: Cooldown finished.");
    }
}