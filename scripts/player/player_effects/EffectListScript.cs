using Godot;
using System;
using System.Collections.Generic;


public partial class EffectListScript : Node
{

	public static EffectListScript Instance { get; private set; }
    public List<Effect> EffectsList { get; set; } = new();

    [Export(PropertyHint.Dir)]
    public string EffectsFolder = "res://Effects";

    // Called automatically when the node enters the scene tree.
    public override void _Ready()
    {
       LoadAllEffects(EffectsFolder);
    }

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this) Instance = null;
	}

    // Recursively loads all Effect resources from the given folder.
    private void LoadAllEffects(string folder)
    {
        // Clear the list so we don't duplicate entries
        // if this method is called multiple times.
        EffectsList.Clear();

        // Try to open the directory.
        var dir = DirAccess.Open(folder);

        // If the folder cannot be opened, print an error and stop.
        if (dir == null)
        {
            GD.PushError($"EffectDatabase: Cannot open folder: {folder}");
            return;
        }

        // Start iterating through the directory contents.
        dir.ListDirBegin();

        while (true)
        {
            // Get the next file or folder name.
            var fileName = dir.GetNext();

            // When there is nothing left, GetNext() returns an empty string.
            if (fileName == "")
                break;

            // If the current item is a directory, we go into it (recursion).
            if (dir.CurrentIsDir())
            {
                // Skip the special entries "." and ".."
                if (fileName != "." && fileName != "..")
                    LoadAllEffects($"{folder}/{fileName}");

                continue;
            }

            // We only want resource files.
            // Ignore anything that is not .tres or .res.
            if (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res"))
                continue;

            // Build the full path to the file.
            var path = $"{folder}/{fileName}";

            // Load the resource from disk.
            var res = ResourceLoader.Load(path);

            // Check if the loaded resource is actually an Effect.
            if (res is Effect effect)
            {
                // Add it to our runtime list.
                EffectsList.Add(effect);
				GD.Print($"Effect [{effect.EffectName}] added from {EffectsFolder}");
            }
        }

        // Stop directory iteration (good practice / frees handles).
        dir.ListDirEnd();

        // Print how many effects were loaded (for debugging).
        GD.Print($"EffectDatabase loaded {EffectsList.Count} effects from {EffectsFolder}");
    }
}