using Godot;

public partial class PlayerHudButtonPrompts : VBoxContainer
{
    private PlayerInteractor _playerInteractor;
    private HotBarScript _hotBarScript;

    private VBoxContainer _buttonActionVBoxContainer;
    private PanelContainer _dummyButtonAction;

    private ButtonActionPromptController _buttonActionController;

    public void Setup(
        PlayerInteractor playerInteractor,
        HotBarScript hotBarScript,
        VBoxContainer buttonActionVBoxContainer,
        PanelContainer dummyButtonAction
    )
    {
        _playerInteractor = playerInteractor;
        _hotBarScript = hotBarScript;
        _buttonActionVBoxContainer = buttonActionVBoxContainer;
        _dummyButtonAction = dummyButtonAction;

        SetupButtonActionController();
        SubscribeToSignals();

        UpdateButtonActionContainerVisibility();
    }

    public void Shutdown()
    {
        UnsubscribeFromSignals();
    }

    private void SetupButtonActionController()
    {
        _buttonActionController = GetNodeOrNull<ButtonActionPromptController>("ButtonActionPromptController");

        if (_buttonActionController == null)
        {
            _buttonActionController = new ButtonActionPromptController();
            _buttonActionController.Name = "ButtonActionPromptController";
            AddChild(_buttonActionController);
        }

        _buttonActionController.Setup(
            _buttonActionVBoxContainer,
            _dummyButtonAction
        );
    }

    private void SubscribeToSignals()
    {
        if (_playerInteractor != null)
        {
            _playerInteractor.ShowInteractPrompt += OnShowInteractPrompt;
            _playerInteractor.HideInteractPrompt += OnHideInteractPrompt;
        }
        else
        {
            GD.PushError("PlayerHudButtonPrompts: PlayerInteractor is not assigned.");
        }

        if (_buttonActionController != null)
        {
            _buttonActionController.PromptHoldCompleted += OnPromptHoldCompleted;
        }
    }

    private void UnsubscribeFromSignals()
    {
        if (_playerInteractor != null)
        {
            _playerInteractor.ShowInteractPrompt -= OnShowInteractPrompt;
            _playerInteractor.HideInteractPrompt -= OnHideInteractPrompt;
        }

        if (_buttonActionController != null)
        {
            _buttonActionController.PromptHoldCompleted -= OnPromptHoldCompleted;
        }
    }

    private void OnShowInteractPrompt(StringName action, string text)
    {
        ShowButtonAction("interact", action, text, false);
    }

    private void OnHideInteractPrompt()
    {
        HideButtonAction("interact");
    }

    private void OnPromptHoldCompleted(string id)
    {
        if (
            id == "selected_item" ||
            id == "use_item" ||
            id == "hotbar_item" ||
            id.StartsWith("hotbar_action_")
        )
        {
            _hotBarScript?.UseSelectedItem();
            return;
        }

        GD.Print($"PlayerHudButtonPrompts: Prompt hold completed for id '{id}', but no action is mapped.");
    }

    public void ShowButtonAction(string id, StringName actionName, string text, bool holdRequired)
    {
        _buttonActionController?.ShowButtonAction(id, actionName, text, holdRequired);

        if (_buttonActionVBoxContainer != null)
        {
            _buttonActionVBoxContainer.Visible = true;
        }
    }

    public void HideButtonAction(string id)
    {
        _buttonActionController?.HideButtonAction(id);

        UpdateButtonActionContainerVisibility();
    }

    public void UpdateButtonActionContainerVisibility()
    {
        if (_buttonActionVBoxContainer == null)
            return;

        bool hasVisiblePrompt = false;

        for (int i = 0; i < _buttonActionVBoxContainer.GetChildCount(); i++)
        {
            if (_buttonActionVBoxContainer.GetChild(i) is Control child && child.Visible)
            {
                hasVisiblePrompt = true;
                break;
            }
        }

        _buttonActionVBoxContainer.Visible = hasVisiblePrompt;
    }
}