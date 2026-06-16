using Godot;
using System;

public partial class PlayerInteractor : Node
{
    [Export] RayCast3D Ray;

	private IInteractable _currentInteractable;

	[Signal] public delegate void ShowInteractPromptEventHandler(StringName action, string text);
	[Signal] public delegate void HideInteractPromptEventHandler();

	public override void _PhysicsProcess(double delta)
	{
		DetectInteractable();

		if (Input.IsActionJustPressed("game_interact") && _currentInteractable != null)
		{
			_currentInteractable.Interact(GetParent());
		}
	}

	void DetectInteractable()
	{
		if (!Ray.IsColliding())
		{
			ClearCurrent();
			return;
		}

		var collider = Ray.GetCollider();

		if (collider is IInteractable interactable && interactable.CanInteract(GetParent()))
		{
			if (_currentInteractable != interactable)
			{
				ClearCurrent();
				_currentInteractable = interactable;

				EmitSignal(
					SignalName.ShowInteractPrompt,
					"game_interact",
					interactable.GetInteractionText()
				);
			}
		}
		else
		{
			ClearCurrent();
		}
	}

	void ClearCurrent()
	{
		if (_currentInteractable != null)
		{
			EmitSignal(SignalName.HideInteractPrompt);
			_currentInteractable = null;
		}
	}

    void TryInteract()
    {
        if (!Ray.IsColliding())
            return;

        var collider = Ray.GetCollider();

        if (collider is IInteractable interactable)
        {
            interactable.Interact(GetParent());
        }
    }
}