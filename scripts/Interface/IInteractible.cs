using Godot;
using System;

public interface IInteractable
{
    void Interact(Node player);
    string GetInteractionText();
    bool CanInteract(Node player);
}