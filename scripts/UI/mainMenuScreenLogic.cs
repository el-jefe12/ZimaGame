using Godot;

public partial class mainMenuScreenLogic : Node
{
	private Panel _overlayPanel;
	private Control _newGamePopup;
	private Control _quitGamePopup;

	private Control _toMainMenuPopup;

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;
		
		_overlayPanel  = GetNode<Panel>("%OverlayPanel");
		_newGamePopup  = GetNode<Control>("%NewGamePopupPanelContainer");
		_quitGamePopup = GetNode<Control>("%QuitGamePopupPanelContainer");
		_toMainMenuPopup = GetNode<Control>("%ToMainMenuPopupPanelContainer");

		// start hidden
		_overlayPanel.Visible = false;
		_newGamePopup.Visible = false;
		_quitGamePopup.Visible = false;
		_toMainMenuPopup.Visible = false;
	}

	// Connect your New Game button's "pressed" signal to this
	public void _on_new_game_button_pressed()
	{
		//HideShowControl(_overlayPanel, "show");
		HideShowControl(_newGamePopup, "show");
	}

	// Connect your Quit button's "pressed" signal to this
	public void _on_close_button_pressed()
	{
		//HideShowControl(_overlayPanel, "show");
		HideShowControl(_quitGamePopup, "show");
	}

	public void _on_to_main_menu_button_pressed()
	{
 		//HideShowControl(_overlayPanel, "show");
		HideShowControl(_toMainMenuPopup, "show");       
	}

	// Connect your overlay click OR popup X/Cancel button "pressed" to this
	public void _on_popup_close_pressed()
	{
		HideAllPopups();
	}


	public static void HideShowControl(Control selectedNode, string visibility) { 
		GD.Print("Processing Control:" + selectedNode.Name + ". Setting visibility: " + visibility);

		if (selectedNode == null) { 
			GD.PushError("HideShowNode called with null selectedNode."); 
			return; 
			} 
		
		string v = visibility.Trim().ToLowerInvariant(); 

		switch (v) { 
			case "show": 
				selectedNode.Visible = true; 
				break; 
			case "hide": 
				selectedNode.Visible = false; 
				break; 
			default: 
				GD.PushError($"Invalid visibility value: '{visibility}'. Expected 'show' or 'hide'."); 
				break; } 
	}

	private void HideAllPopups()
	{
		_newGamePopup.Visible = false;
		_quitGamePopup.Visible = false;
		_overlayPanel.Visible = false;
	}
}
