using Godot;
using System;

public partial class WorldTime : Node
{
    public static WorldTime Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    // =========================
    // Settings
    // =========================
    [Export(PropertyHint.Range, "0.05,10.0,0.05")]
    public float TickIntervalSeconds = 1.0f; // 1 real second = 1 game minute

    // =========================
    // Time state
    // =========================
    [Export] public int StartDay = 1;
    [Export(PropertyHint.Range, "0,23")] public int StartHour = 6;
    [Export(PropertyHint.Range, "0,59")] public int StartMinute = 0;

    public int Day { get; private set; }
    public int Hour { get; private set; }
    public int Minute { get; private set; }

    public const int MinutesPerHour = 60;
    public const int HoursPerDay = 24;
    public const int MinutesPerDay = MinutesPerHour * HoursPerDay;

    // =========================
    // Signals (Godot 4.x correct)
    // =========================
    [Signal] public delegate void MinuteTickEventHandler(int day, int hour, int minute);
    [Signal] public delegate void HourChangedEventHandler(int day, int hour);
    [Signal] public delegate void DayChangedEventHandler(int day);

    private Timer _timer;

    public override void _Ready()
    {
        Day = Math.Max(1, StartDay);
        Hour = Mathf.Clamp(StartHour, 0, 23);
        Minute = Mathf.Clamp(StartMinute, 0, 59);

        _timer = new Timer();
        _timer.WaitTime = Mathf.Max(0.05f, TickIntervalSeconds);
        _timer.OneShot = false;
        _timer.Autostart = true;

        // ✅ Godot 4.5 correct API
        _timer.ProcessCallback = Timer.TimerProcessCallback.Idle;

        _timer.Timeout += OnTick;

        AddChild(_timer);
    }

    // =========================
    // Ticking
    // =========================
    private void OnTick()
    {
        AdvanceMinute();
    }

    private void AdvanceMinute()
    {
        Minute++;

        if (Minute >= MinutesPerHour)
        {
            Minute = 0;
            Hour++;

            if (Hour >= HoursPerDay)
            {
                Hour = 0;
                Day++;
                EmitSignal(SignalName.DayChanged, Day);
            }

            EmitSignal(SignalName.HourChanged, Day, Hour);
        }

        EmitSignal(SignalName.MinuteTick, Day, Hour, Minute);
    }

    // =========================
    // Public helpers
    // =========================
    public void AdvanceMinutes(int minutes)
    {
        if (minutes <= 0)
            return;

        for (int i = 0; i < minutes; i++)
            AdvanceMinute();
    }

    public float GetDayProgress01()
    {
        return ((Hour * MinutesPerHour) + Minute) / (float)MinutesPerDay;
    }

    public bool IsNight()
    {
        return Hour < 6 || Hour >= 20;
    }

    public string GetTimeString()
    {
        return $"{Hour:00}:{Minute:00} (Day {Day})";
    }
}
