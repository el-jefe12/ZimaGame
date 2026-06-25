using System.Collections.Generic;
using Godot;

public partial class EffectListScript : Node
{
	public static EffectListScript? Instance { get; private set; }

	public List<Effect> EffectsList { get; private set; } = new();

	[ExportCategory("Export-safe Effect List")]
	[Export] public Godot.Collections.Array<Effect> ExportedEffects { get; set; } = new();

	[ExportCategory("Optional Folder Fallback")]
	[Export(PropertyHint.Dir)]
	public string EffectsFolder { get; set; } = "res://Effects";

	private bool _loaded = false;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public override void _Ready()
	{
		ReloadEffects();
	}

	public void EnsureLoaded()
	{
		if (_loaded && EffectsList.Count > 0)
		{
			return;
		}

		ReloadEffects();
	}

	public void ReloadEffects()
	{
		EffectsList.Clear();

		LoadExportedEffects();

		// Folder fallback only if exported array is empty.
		// In exported builds, folder scanning can be unreliable, so exported array is preferred.
		if (EffectsList.Count == 0)
		{
			LoadEffectsRecursive(EffectsFolder);
		}

		_loaded = true;

		GD.Print($"EffectDatabase: Total loaded effects = {EffectsList.Count}");
		PrintLoadedEffectsDebug();
	}

	private void LoadExportedEffects()
	{
		foreach (Effect effect in ExportedEffects)
		{
			if (effect == null)
			{
				continue;
			}

			if (!EffectsList.Contains(effect))
			{
				EffectsList.Add(effect);
			}
		}

		GD.Print($"EffectDatabase: Loaded {EffectsList.Count} effects from ExportedEffects.");
	}

	private void LoadEffectsRecursive(string folder)
	{
		DirAccess? dir = DirAccess.Open(folder);

		if (dir == null)
		{
			GD.PushWarning($"EffectDatabase: Cannot open folder fallback: {folder}");
			return;
		}

		dir.ListDirBegin();

		while (true)
		{
			string fileName = dir.GetNext();

			if (string.IsNullOrEmpty(fileName))
			{
				break;
			}

			if (fileName == "." || fileName == "..")
			{
				continue;
			}

			string path = $"{folder}/{fileName}";

			if (dir.CurrentIsDir())
			{
				LoadEffectsRecursive(path);
				continue;
			}

			if (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res"))
			{
				continue;
			}

			Resource? resource = ResourceLoader.Load(path);

			if (resource is not Effect effect)
			{
				GD.Print($"EffectDatabase: Skipped non-Effect resource: {path}");
				continue;
			}

			if (!EffectsList.Contains(effect))
			{
				EffectsList.Add(effect);
			}

			GD.Print(
				$"EffectDatabase: Loaded '{effect.EffectName}' " +
				$"Type={effect.Type}, Severity={effect.EffectSeverity}, Id='{effect.EffectId}' from {path}"
			);
		}

		dir.ListDirEnd();
	}

	private void PrintLoadedEffectsDebug()
	{
		GD.Print("========== LOADED EFFECTS ==========");

		foreach (Effect effect in EffectsList)
		{
			if (effect == null)
			{
				GD.Print("Effect: NULL");
				continue;
			}

			string iconPath = effect.Icon == null
				? "NULL"
				: effect.Icon.ResourcePath;

			int modifierCount = effect.Modifiers == null
				? 0
				: effect.Modifiers.Count;

			GD.Print(
				$"Effect: Name='{effect.EffectName}', " +
				$"Type={effect.Type}, " +
				$"Severity={effect.EffectSeverity} ({(int)effect.EffectSeverity}), " +
				$"Id='{effect.EffectId}', " +
				$"Modifiers={modifierCount}, " +
				$"Icon='{iconPath}'"
			);
		}

		GD.Print("====================================");
	}
}