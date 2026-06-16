using Godot;
using System;

public partial class PlayerTemperatureSystem : Node
{
    [Signal]
    public delegate void PlayerTemperatureChangedEventHandler(
        float minTemp,
        float maxTemp,
        float currentPlayerTemp
    );

    [Export] public float MinTemperature = -100;
    [Export] public float MaxTemperature = 100;
    [Export] public float PlayerTemperature = 36.4f;

    private float _lastTemp = -9999;

    public override void _Ready()
    {
        NotifyIfChanged();
    }

    public void SetTemperature(float value)
    {
        PlayerTemperature = Mathf.Clamp(value, MinTemperature, MaxTemperature);
        NotifyIfChanged();
    }

    private void NotifyIfChanged()
    {
        if (!Mathf.IsEqualApprox(PlayerTemperature, _lastTemp))
        {
            _lastTemp = PlayerTemperature;

            EmitSignal(
                SignalName.PlayerTemperatureChanged,
                MinTemperature,
                MaxTemperature,
                PlayerTemperature
            );
        }
    }
}