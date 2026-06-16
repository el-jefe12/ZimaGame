using System;
using System.Collections.Generic;
using Godot;

public partial class ButtonActionPromptController : Node
{
    public event Action<string> PromptHoldCompleted;

    private VBoxContainer _buttonActionVBoxContainer;
    private PanelContainer _dummyButtonAction;

    private readonly Dictionary<string, buttonActionIndicatorLogic> _activePrompts = new();

    public void Setup(
        VBoxContainer buttonActionVBoxContainer,
        PanelContainer dummyButtonAction
    )
    {
        _buttonActionVBoxContainer = buttonActionVBoxContainer;
        _dummyButtonAction = dummyButtonAction;

        if (_buttonActionVBoxContainer == null)
            GD.PushError("ButtonActionPromptController: button action container is null.");

        if (_dummyButtonAction == null)
            GD.PushError("ButtonActionPromptController: dummy button action is null.");
    }

    public void ShowButtonAction(string id, StringName actionName, string text, bool holdRequired)
    {
        id = NormalizeId(id);

        if (id == "")
        {
            GD.PushWarning("ButtonActionPromptController: Cannot show prompt with empty id.");
            return;
        }

        if (_buttonActionVBoxContainer == null)
        {
            GD.PushError("ButtonActionPromptController: _buttonActionVBoxContainer is null. Did you call Setup()?");
            return;
        }

        if (_dummyButtonAction == null)
        {
            GD.PushError("ButtonActionPromptController: _dummyButtonAction is null. Did you call Setup()?");
            return;
        }

        if (_activePrompts.TryGetValue(id, out buttonActionIndicatorLogic existingPrompt))
        {
            existingPrompt.HeldRequired = holdRequired;
            existingPrompt.SetAction(actionName, text);
            return;
        }

        buttonActionIndicatorLogic prompt = (buttonActionIndicatorLogic)_dummyButtonAction
            .Duplicate((int)Node.DuplicateFlags.UseInstantiation);

        prompt.Visible = true;
        prompt.UseInputMapKey = true;
        prompt.HeldRequired = holdRequired;
        prompt.SetAction(actionName, text);

        if (holdRequired)
        {
            string capturedId = id;

            prompt.HoldCompleted += () =>
            {
                PromptHoldCompleted?.Invoke(capturedId);
            };
        }

        _buttonActionVBoxContainer.AddChild(prompt);

        _activePrompts[id] = prompt;
    }

    public void HideButtonAction(string id)
    {
        id = NormalizeId(id);

        if (!_activePrompts.TryGetValue(id, out buttonActionIndicatorLogic prompt))
            return;

        if (IsInstanceValid(prompt))
            prompt.QueueFree();

        _activePrompts.Remove(id);
    }

    public void HideAllButtonActions()
    {
        foreach (buttonActionIndicatorLogic prompt in _activePrompts.Values)
        {
            if (IsInstanceValid(prompt))
                prompt.QueueFree();
        }

        _activePrompts.Clear();
    }

    public bool HasButtonAction(string id)
    {
        id = NormalizeId(id);
        return _activePrompts.ContainsKey(id);
    }

    private string NormalizeId(string id)
    {
        return (id ?? "").Trim().ToLowerInvariant();
    }
}