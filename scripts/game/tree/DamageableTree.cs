using Godot;

public partial class DamageableTree : Node3D
{
    [ExportCategory("Components")]
    [Export] public NodePath DamageablePath = "";
    [Export] public NodePath DropperPath = "";

    [ExportCategory("Tree Parts")]
    [Export] public NodePath ShakeNodePath = "StaticBody3D";
    [Export] public NodePath TrunkPath = "StaticBody3D/tree_full";
    [Export] public NodePath TrunkCollisionPath = "StaticBody3D/BodyCollisionShape3D";
    [Export] public NodePath StumpPath = "StaticBody3D/tree_stump";
    [Export] public NodePath StumpCollisionPath = "StaticBody3D/StumpCollisionShape3D";

    [ExportCategory("Hit Reaction")]
    [Export] public float HitScaleAmount = 0.06f;
    [Export] public float HitSquashTime = 0.05f;
    [Export] public float HitReturnTime = 0.10f;

    private Damageable _damageable;
    private WorldItemDropper _dropper;

    private Node3D _shakeNode;
    private Node3D _trunk;
    private CollisionShape3D _trunkCollision;
    private Node3D _stump;
    private CollisionShape3D _stumpCollision;

    private Vector3 _originalShakeScale = Vector3.One;
    private Tween _hitTween;

    private bool _treeDestroyed = false;

    public override void _Ready()
    {
        _damageable = FindDamageable();
        _dropper = FindDropper();

        _shakeNode = GetNodeOrNull<Node3D>(ShakeNodePath);
        _trunk = GetNodeOrNull<Node3D>(TrunkPath);
        _trunkCollision = GetNodeOrNull<CollisionShape3D>(TrunkCollisionPath);
        _stump = GetNodeOrNull<Node3D>(StumpPath);
        _stumpCollision = GetNodeOrNull<CollisionShape3D>(StumpCollisionPath);

        if (_shakeNode != null)
        {
            _originalShakeScale = _shakeNode.Scale;
        }
        else
        {
            GD.PrintErr("DamageableTree: Shake node not found.");
        }

        if (_damageable == null)
        {
            GD.PrintErr("DamageableTree: Damageable component not found.");
        }
        else
        {
            _damageable.Damaged += OnDamaged;
            _damageable.Died += OnDied;
        }

        if (_dropper == null)
        {
            GD.PrintErr("DamageableTree: WorldItemDropper not found. Tree will not spawn drops.");
        }

        if (_trunk == null)
        {
            GD.PrintErr("DamageableTree: Trunk node not found.");
        }

        if (_stump != null)
        {
            _stump.Visible = true;
        }

        if (_stumpCollision != null)
        {
            _stumpCollision.Disabled = false;
        }
    }

    // IMPORTANT:
    // Your AttackAction searches upward from the collider for a TakeDamage method.
    // Since Damageable is now separate, the tree root must forward damage to it.
    public void TakeDamage(float damage)
    {
        if (_damageable == null)
        {
            GD.PrintErr("DamageableTree: Cannot take damage because Damageable is missing.");
            return;
        }

        _damageable.TakeDamage(damage);
    }

    private void OnDamaged(float damage, float currentHealth, float maxHealth)
    {
        PlayHitReaction();
    }

    private void OnDied()
    {
        DestroyTree();
    }

    private void PlayHitReaction()
    {
        if (_treeDestroyed)
        {
            return;
        }

        if (_shakeNode == null)
        {
            return;
        }

        if (_hitTween != null && _hitTween.IsValid())
        {
            _hitTween.Kill();
        }

        Vector3 expandedScale = _originalShakeScale + new Vector3(
            HitScaleAmount,
            HitScaleAmount,
            HitScaleAmount
        );

        _hitTween = CreateTween();

        // Quick full-tree pop.
        _hitTween.TweenProperty(
            _shakeNode,
            "scale",
            expandedScale,
            HitSquashTime
        );

        // Return to normal.
        _hitTween.TweenProperty(
            _shakeNode,
            "scale",
            _originalShakeScale,
            HitReturnTime
        );
    }

    private void DestroyTree()
    {
        if (_treeDestroyed)
        {
            return;
        }

        _treeDestroyed = true;

        GD.Print("DamageableTree: Tree destroyed.");

        if (_hitTween != null && _hitTween.IsValid())
        {
            _hitTween.Kill();
        }

        if (_shakeNode != null)
        {
            _shakeNode.Scale = _originalShakeScale;
        }

        if (_trunk != null)
        {
            _trunk.Visible = false;
        }

        if (_trunkCollision != null)
        {
            _trunkCollision.Disabled = true;
        }

        if (_stump != null)
        {
            _stump.Visible = true;
        }

        if (_stumpCollision != null)
        {
            _stumpCollision.Disabled = false;
        }

        if (_dropper != null)
        {
            _dropper.DropAt(GlobalPosition);
        }
    }

    private Damageable FindDamageable()
    {
        if (DamageablePath != null && !DamageablePath.IsEmpty)
        {
            Damageable damageableFromPath = GetNodeOrNull<Damageable>(DamageablePath);

            if (damageableFromPath != null)
            {
                return damageableFromPath;
            }

            GD.PrintErr($"DamageableTree: No Damageable found at path '{DamageablePath}'.");
        }

        // Search direct children first.
        foreach (Node child in GetChildren())
        {
            if (child is Damageable childDamageable)
            {
                return childDamageable;
            }
        }

        // Then search recursively, in case you put it deeper.
        return FindDamageableRecursive(this);
    }

    private Damageable FindDamageableRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Damageable damageable)
            {
                return damageable;
            }

            Damageable found = FindDamageableRecursive(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private WorldItemDropper FindDropper()
    {
        if (DropperPath != null && !DropperPath.IsEmpty)
        {
            WorldItemDropper dropperFromPath = GetNodeOrNull<WorldItemDropper>(DropperPath);

            if (dropperFromPath != null)
            {
                return dropperFromPath;
            }

            GD.PrintErr($"DamageableTree: No WorldItemDropper found at path '{DropperPath}'.");
        }

        // Search direct children first.
        foreach (Node child in GetChildren())
        {
            if (child is WorldItemDropper childDropper)
            {
                return childDropper;
            }
        }

        // Then search recursively.
        return FindDropperRecursive(this);
    }

    private WorldItemDropper FindDropperRecursive(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is WorldItemDropper dropper)
            {
                return dropper;
            }

            WorldItemDropper found = FindDropperRecursive(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

	public void ApplyMinedState()
{
    GD.Print("DamageableTree: Applying mined/stump state.");

    if (_hitTween != null && _hitTween.IsValid())
    {
        _hitTween.Kill();
    }

    if (_shakeNode != null)
    {
        _shakeNode.Scale = _originalShakeScale;
    }

    if (_trunk != null)
    {
        _trunk.Visible = false;
    }

    if (_trunkCollision != null)
    {
        _trunkCollision.Disabled = true;
    }

    if (_stump != null)
    {
        _stump.Visible = true;
    }

    if (_stumpCollision != null)
    {
        _stumpCollision.Disabled = false;
    }
}
}