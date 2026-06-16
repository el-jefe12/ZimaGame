using Godot;
using System;

public partial class DeathScreenLogic : Control
{
	[ExportCategory("Fade")]
	[Export] public float BackgroundFadeDuration = 0.45f;

	[Export] public float TitleDelayAfterBlack = 0.05f;
	[Export] public float TitleFadeDuration = 0.12f;

	[Export] public float DetailsDelayAfterTitle = 0.10f;
	[Export] public float DetailsFadeDuration = 0.22f;

	private ColorRect _colorBackground = null!;
	private Control _buttonsContainer = null!;

	private RichTextLabel _deathLabel = null!;
	private RichTextLabel _deathCauseLabel = null!;
	private RichTextLabel _daysSurvivedLabel = null!;

	public override void _Ready()
	{
		_colorBackground   = GetNode<ColorRect>("ColorBackground");

		_deathLabel        = GetNode<RichTextLabel>("%DeathLabel");
		_deathCauseLabel   = GetNode<RichTextLabel>("%DeathCauseLabel");
		_daysSurvivedLabel = GetNode<RichTextLabel>("%DaysSurvivedLabel");

		// Your ButtonsContainer might be VBoxContainer; use Control so it's flexible.
		_buttonsContainer  = GetNode<Control>("%ButtonsContainer");

		// Initial state: background transparent, UI invisible
		SetAlpha(_colorBackground, 0.0f);
		SetAlpha(_deathLabel, 0.0f);
		SetAlpha(_deathCauseLabel, 0.0f);
		SetAlpha(_daysSurvivedLabel, 0.0f);
		SetAlpha(_buttonsContainer, 0.0f);

		PlayDeathSequence();
	}

	private void PlayDeathSequence()
	{
		Tween t = CreateTween();
		t.SetTrans(Tween.TransitionType.Sine);
		t.SetEase(Tween.EaseType.Out);

		// 1) Fade to black
		t.TweenProperty(_colorBackground, "modulate:a", 1.0f, BackgroundFadeDuration);

		// 2) DeathLabel quickly
		t.TweenInterval(TitleDelayAfterBlack);
		t.TweenProperty(_deathLabel, "modulate:a", 1.0f, TitleFadeDuration);

		// 3) Cause + days + buttons together
		t.TweenInterval(DetailsDelayAfterTitle);
		t.TweenProperty(_deathCauseLabel, "modulate:a", 1.0f, DetailsFadeDuration);
		t.Parallel().TweenProperty(_daysSurvivedLabel, "modulate:a", 1.0f, DetailsFadeDuration);
		t.Parallel().TweenProperty(_buttonsContainer, "modulate:a", 1.0f, DetailsFadeDuration);
	}

	private static void SetAlpha(CanvasItem item, float a)
	{
		Color c = item.Modulate;
		c.A = Mathf.Clamp(a, 0.0f, 1.0f);
		item.Modulate = c;
	}
}
