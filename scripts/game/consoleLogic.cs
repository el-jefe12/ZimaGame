using Godot;
using System;

public partial class consoleLogic : Control
{
    private TextEdit _ConsoleInput;

    private VBoxContainer _ConsoleTextVBoxContainer;
    private RichTextLabel _ConsoleEntryExample;
    private ScrollContainer _ConsoleScrollContainer;

    public override void _Ready()
    {
        GD.Print("ConsoleLogic initialized.");

        _ConsoleInput = GetNodeOrNull<TextEdit>("%ConsoleInput");
        _ConsoleEntryExample = GetNodeOrNull<RichTextLabel>("%ConsoleEntryExample");
        _ConsoleTextVBoxContainer = GetNodeOrNull<VBoxContainer>("%ConsoleTextVBoxContainer");
        _ConsoleScrollContainer = GetNodeOrNull<ScrollContainer>("%ConsoleScrollContainer");

        if (_ConsoleInput == null)
        {
            GD.PushError("ConsoleInput node not found (%ConsoleInput).");
            return;
        }

        // Handle input on the TextEdit itself so we can stop it from inserting a newline after clearing
        _ConsoleInput.GuiInput += OnConsoleInputGuiInput;
    }

    private void OnConsoleInputGuiInput(InputEvent @event)
    {
        if (!robinsonGlobals.Instance.ConsoleActive)
            return;

        if (!@event.IsActionPressed("game_console_send_command"))
            return;

        string command = _ConsoleInput.Text ?? "";

        // Stop TextEdit from processing Enter (newline) even if command is empty
        GetViewport().SetInputAsHandled();

        if (string.IsNullOrWhiteSpace(command))
            return;

        GD.Print("Command Sent: ", command);
        
        CallCommand(command);

        // Clear reliably
        _ConsoleInput.Text = "";

        // Optional: put caret at start (correct Godot C# API)
        _ConsoleInput.SetCaretLine(0);
        _ConsoleInput.SetCaretColumn(0);
    }

    private void CallCommand(string command)
    {
        string commandStringLong = $"[color=#e9844e] Called Command: [color=#fa4c72]{command}[/color].";
        GD.Print("Command Called: ", command);
        CreateConsoleCommandEntry(commandStringLong);
    }

    private void CreateConsoleCommandEntry(string text)
    {
        if (_ConsoleEntryExample == null || _ConsoleTextVBoxContainer == null)
        {
            GD.PushError("Console entry template or VBoxContainer missing.");
            return;
        }

        // Duplicate the template
        RichTextLabel entry = (RichTextLabel)_ConsoleEntryExample.Duplicate();

        // Make sure it is visible
        entry.Visible = true;

        // Set text
        entry.Text = text;

        // Add to container
        _ConsoleTextVBoxContainer.AddChild(entry);

        // Optional: scroll to bottom
        CallDeferred(nameof(ScrollToBottom));
    }

    private void ScrollToBottom()
    {
        if (_ConsoleScrollContainer == null)
            return;

        _ConsoleScrollContainer.ScrollVertical =
            (int)_ConsoleScrollContainer.GetVScrollBar().MaxValue;
    }
}
