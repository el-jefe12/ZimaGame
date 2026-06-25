using Godot;

public partial class playerHUD : Control
{
	[Export] public PlayerController Player;
	[Export] public PlayerInteractor Player_Interactor;
	[Export] public PlayerInventory PlayerInventory;

	private PlayerStats _stats;
	private PlayerNeedsSystem _needs;
	private PlayerHealthSystem _health;
	private PlayerTemperatureSystem _temperature;
	private PlayerEffects _playerEffects;

	private HotBarScript _hotBarScript;

	private TextureProgressBar _HealthBar;
	private TextureProgressBar _StaminaBar;

	private RichTextLabel _TemperatureIndicatorOutside;
	private RichTextLabel _TemperatureIndicatorPlayer;
	private RichTextLabel _DigitalClock;

	private Control _MainUI;
	private Control _InventoryUI;

	private PanelContainer _ChestInventoryContainer;
	private PanelContainer _InventoryContainer;

	private VBoxContainer _ButtonActionVBoxContainer;
	private VBoxContainer _EffectsList;
	private HBoxContainer _EffectsHBoxContainer;

	private PanelContainer _DummyEffectSlot;
	private Control _DummyEffectIcon;
	private PanelContainer _DummyButtonAction;

	private PlayerHudEffects _effectsController;
	private ButtonActionPromptController _buttonActionController;

	public override void _Ready()
	{
		if (Player == null)
		{
			GD.PushError("playerHUD: Player not assigned in inspector.");
			return;
		}

		CacheHudNodes();
		CachePlayerSystems();
		SetupHudControllers();
		SubscribeToSignals();
		DrawInitialValues();

		UpdateButtonActionContainerVisibility();
	}

	public override void _ExitTree()
	{
		UnsubscribeFromSignals();
	}

	private void CacheHudNodes()
	{
		_HealthBar = GetNode<TextureProgressBar>("%HealthBar");
		_StaminaBar = GetNode<TextureProgressBar>("%StaminaBar");

		_MainUI = GetNode<Control>("%MainUI");
		_InventoryUI = GetNode<Control>("%InventoryUI");

		_ChestInventoryContainer = GetNode<PanelContainer>("%ChestInventoryContainer");
		_InventoryContainer = GetNode<PanelContainer>("%InventoryContainer");

		_TemperatureIndicatorOutside = GetNode<RichTextLabel>("%TemperatureIndicatorOutside");
		_TemperatureIndicatorPlayer = GetNode<RichTextLabel>("%TemperatureIndicatorPlayer");

		_DigitalClock = GetNode<RichTextLabel>("%DigitalClock");

		_EffectsList = GetNode<VBoxContainer>("%EffectsList");
		_EffectsHBoxContainer = GetNode<HBoxContainer>("%EffectsHBoxContainer");

		_DummyEffectSlot = GetNode<PanelContainer>("%DummyEffectSlot");
		_DummyEffectIcon = GetNode<Control>("%DummyEffectIcon");

		_DummyButtonAction = GetNode<PanelContainer>("%DummyButtonAction");
		_ButtonActionVBoxContainer = GetNode<VBoxContainer>("%ButtonActionVBoxContainer");

		_hotBarScript = GetNodeOrNull<HotBarScript>("HotBar");

		if (_hotBarScript == null)
		{
			GD.PushError("playerHUD: HotBarScript not found under playerHUD.");
		}
	}

	private void CachePlayerSystems()
	{
		_stats = Player.GetNodeOrNull<PlayerStats>("%PlayerStats");
		_needs = Player.GetNodeOrNull<PlayerNeedsSystem>("%PlayerNeeds");
		_health = Player.GetNodeOrNull<PlayerHealthSystem>("%PlayerHealth");
		_temperature = Player.GetNodeOrNull<PlayerTemperatureSystem>("%PlayerTemperature");
        
        _playerEffects = Player.GetNodeOrNull<PlayerEffects>("PlayerScriptsAttached/PlayerEffects");

        if (_playerEffects == null)
        {
            GD.PushError("playerHUD: PlayerEffects node missing or does not have the PlayerEffects.cs script attached.");
}

		if (_stats == null)
		{
			GD.PushError("playerHUD: PlayerStats node missing.");
		}

		if (_needs == null)
		{
			GD.PushError("playerHUD: PlayerNeedsSystem node missing.");
		}

		if (_health == null)
		{
			GD.PushError("playerHUD: PlayerHealthSystem node missing.");
		}

		if (_temperature == null)
		{
			GD.PushError("playerHUD: PlayerTemperatureSystem node missing.");
		}

		if (_playerEffects == null)
		{
			GD.PushError("playerHUD: PlayerEffects node missing. HUD effects will show, but gameplay modifiers will not run.");
		}
	}

	private void SetupHudControllers()
	{
		_effectsController = GetNodeOrNull<PlayerHudEffects>("PlayerHudEffects");

		if (_effectsController == null)
		{
			_effectsController = new PlayerHudEffects();
			_effectsController.Name = "PlayerHudEffects";
			AddChild(_effectsController);
		}

		// Important:
		// PlayerHudEffects now needs the gameplay PlayerEffects system too.
		_effectsController.Setup(
			_EffectsList,
			_EffectsHBoxContainer,
			_DummyEffectSlot,
			_DummyEffectIcon,
			_playerEffects
		);

		_buttonActionController = GetNodeOrNull<ButtonActionPromptController>("ButtonActionPromptController");

		if (_buttonActionController == null)
		{
			_buttonActionController = new ButtonActionPromptController();
			_buttonActionController.Name = "ButtonActionPromptController";
			AddChild(_buttonActionController);
		}

		_buttonActionController.Setup(
			_ButtonActionVBoxContainer,
			_DummyButtonAction
		);

		_buttonActionController.PromptHoldCompleted += OnPromptHoldCompleted;
	}

	private void SubscribeToSignals()
	{
		if (WorldTime.Instance != null)
		{
			WorldTime.Instance.MinuteTick += UpdateTime;
		}
		else
		{
			GD.PushError("playerHUD: WorldTime.Instance is null.");
		}

		if (_health != null)
		{
			_health.HealthChanged += OnHealthChanged;
		}

		if (_stats != null)
		{
			_stats.StaminaChanged += OnStaminaChanged;
		}

		if (_temperature != null)
		{
			_temperature.PlayerTemperatureChanged += OnTemperatureChanged;
		}

		if (_needs != null)
		{
			_needs.HungerChanged += OnHungerChanged;
			_needs.ThirstChanged += OnThirstChanged;
			_needs.TiredChanged += OnTiredChanged;
		}

		if (Player_Interactor != null)
		{
			Player_Interactor.ShowInteractPrompt += OnShowInteractPrompt;
			Player_Interactor.HideInteractPrompt += OnHideInteractPrompt;
		}
		else
		{
			GD.PushError("playerHUD: Player_Interactor not assigned.");
		}

		if (PlayerInventory != null)
		{
			PlayerInventory.ItemAdded += OnItemAdded;
		}
		else
		{
			GD.PushError("playerHUD: PlayerInventory not assigned.");
		}

		if (InventoryManager.Instance != null)
		{
			InventoryManager.Instance.InventoryUi += OnInventoryUi;
		}
		else
		{
			GD.PushError("playerHUD: InventoryManager.Instance is null.");
		}
	}

	private void UnsubscribeFromSignals()
	{
		if (WorldTime.Instance != null)
		{
			WorldTime.Instance.MinuteTick -= UpdateTime;
		}

		if (_health != null)
		{
			_health.HealthChanged -= OnHealthChanged;
		}

		if (_stats != null)
		{
			_stats.StaminaChanged -= OnStaminaChanged;
		}

		if (_temperature != null)
		{
			_temperature.PlayerTemperatureChanged -= OnTemperatureChanged;
		}

		if (_needs != null)
		{
			_needs.HungerChanged -= OnHungerChanged;
			_needs.ThirstChanged -= OnThirstChanged;
			_needs.TiredChanged -= OnTiredChanged;
		}

		if (Player_Interactor != null)
		{
			Player_Interactor.ShowInteractPrompt -= OnShowInteractPrompt;
			Player_Interactor.HideInteractPrompt -= OnHideInteractPrompt;
		}

		if (PlayerInventory != null)
		{
			PlayerInventory.ItemAdded -= OnItemAdded;
		}

		if (InventoryManager.Instance != null)
		{
			InventoryManager.Instance.InventoryUi -= OnInventoryUi;
		}

		if (_buttonActionController != null)
		{
			_buttonActionController.PromptHoldCompleted -= OnPromptHoldCompleted;
		}
	}

	private void DrawInitialValues()
	{
		if (WorldTime.Instance != null)
		{
			UpdateTime(
				WorldTime.Instance.Day,
				WorldTime.Instance.Hour,
				WorldTime.Instance.Minute
			);
		}

		if (_health != null)
		{
			OnHealthChanged(_health.Health, _health.MaxHealth);
		}

		if (_stats != null)
		{
			OnStaminaChanged(_stats.Stamina, _stats.MaxStamina);
		}
	}

	private void OnHealthChanged(float current, float max)
	{
		if (_HealthBar == null)
		{
			return;
		}

		_HealthBar.MaxValue = max;
		_HealthBar.Value = current;
	}

	private void OnStaminaChanged(float current, float max)
	{
		if (_StaminaBar == null)
		{
			return;
		}

		_StaminaBar.MaxValue = max;
		_StaminaBar.Value = current;
	}

	private void OnHungerChanged(float current, float max)
	{
		GD.Print($"HUD hunger signal: {current}/{max}");
		_effectsController?.HandleStatEffect(Effect.EType.Hunger, current, max);
	}

	private void OnThirstChanged(float current, float max)
	{
		GD.Print($"HUD thirst signal: {current}/{max}");
		_effectsController?.HandleStatEffect(Effect.EType.Thirst, current, max);
	}

	private void OnTiredChanged(float current, float max)
	{
		GD.Print($"HUD tired signal: {current}/{max}");
		_effectsController?.HandleStatEffect(Effect.EType.Tired, current, max);
	}

	private void OnTemperatureChanged(float MinTemperature, float MaxTemperature, float PlayerTemperature)
	{
		if (_TemperatureIndicatorPlayer == null)
		{
			return;
		}

		string tempDec = PlayerTemperature.ToString("F1").Replace(",", ".");

		string color;

		if (PlayerTemperature >= 42.0f)
		{
			color = "ff2c27";
		}
		else if (PlayerTemperature >= 39.5f)
		{
			color = "ff2c27";
		}
		else if (PlayerTemperature >= 37.6f)
		{
			color = "gold";
		}
		else if (PlayerTemperature <= 28.0f)
		{
			color = "ff2c27";
		}
		else if (PlayerTemperature <= 32.0f)
		{
			color = "ff2c27";
		}
		else if (PlayerTemperature < 35.6f)
		{
			color = "gold";
		}
		else
		{
			color = "light_green";
		}

		_TemperatureIndicatorPlayer.Text = $"[color={color}]{tempDec}°C[/color]";
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

		GD.Print($"playerHUD: Prompt hold completed for id '{id}', but no action is mapped.");
	}

	public void ShowButtonAction(string id, StringName actionName, string text, bool holdRequired)
	{
		_buttonActionController?.ShowButtonAction(id, actionName, text, holdRequired);

		if (_ButtonActionVBoxContainer != null)
		{
			_ButtonActionVBoxContainer.Visible = true;
		}
	}

	public void HideButtonAction(string id)
	{
		_buttonActionController?.HideButtonAction(id);

		UpdateButtonActionContainerVisibility();
	}

	private void UpdateButtonActionContainerVisibility()
	{
		if (_ButtonActionVBoxContainer == null)
		{
			return;
		}

		bool hasVisiblePrompt = false;

		for (int i = 0; i < _ButtonActionVBoxContainer.GetChildCount(); i++)
		{
			if (_ButtonActionVBoxContainer.GetChild(i) is Control child && child.Visible)
			{
				hasVisiblePrompt = true;
				break;
			}
		}

		_ButtonActionVBoxContainer.Visible = hasVisiblePrompt;
	}

	private void OnItemAdded(ItemInstance item)
	{
		if (item == null || item.Definition == null)
		{
			GD.PushWarning("playerHUD: OnItemAdded received null item or item definition.");
			return;
		}

		GD.Print($"Item added to inventory: {item.Definition.ItemName}");
	}

	private void OnHotbarItemAdded(ItemInstance item, int slot)
	{
		if (item == null || item.Definition == null)
		{
			GD.PushWarning("playerHUD: OnHotbarItemAdded received null item or item definition.");
			return;
		}

		GD.Print($"Hotbar slot {slot} now contains {item.Definition.ItemName}");

		_hotBarScript?.UpdateSlot(slot, item);
	}

	private void UpdateTime(int day, int hour, int minute)
	{
		if (_DigitalClock == null)
		{
			return;
		}

		string timeText = $"{hour:00}:{minute:00}";
		_DigitalClock.Text = timeText;
	}

	private void OnInventoryUi(bool open, Node container)
	{
		bool itemUiOpen = robinsonGlobals.Instance != null &&
						  robinsonGlobals.Instance.OpenItemUI;

		// If item UI is open, inventory is not allowed to open,
		// and inventory close events must NOT restore movement.
		if (itemUiOpen)
		{
			if (_InventoryUI != null)
			{
				_InventoryUI.Visible = false;
			}

			if (_ChestInventoryContainer != null)
			{
				_ChestInventoryContainer.Visible = false;
			}

			if (robinsonGlobals.Instance != null)
			{
				robinsonGlobals.Instance.OpenInventory = false;
				robinsonGlobals.Instance.CanMove = false;
			}

			Input.MouseMode = Input.MouseModeEnum.Visible;

			GD.Print("playerHUD: Inventory UI ignored because item UI is open.");
			return;
		}

		if (_MainUI != null)
		{
			_MainUI.Visible = !open;
		}

		if (_InventoryUI != null)
		{
			_InventoryUI.Visible = open;
		}

		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.OpenInventory = open;
			robinsonGlobals.Instance.CanMove = !open;
		}

		if (_ChestInventoryContainer != null)
		{
			_ChestInventoryContainer.Visible = open && container != null;
		}

		Input.MouseMode = open
			? Input.MouseModeEnum.Visible
			: Input.MouseModeEnum.Captured;

		if (open && container != null)
		{
			// Bind chest UI to this container here later.
			// InventoryUI.BindContainer(container);
		}
	}
}