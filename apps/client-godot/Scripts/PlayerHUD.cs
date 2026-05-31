using Godot;

/// <summary>
/// Displays player constraint state as a HUD overlay.
/// Hunger, Thirst, Fatigue, Infection, Morale, Temperature bars.
/// </summary>
public partial class PlayerHUD : Control
{
    private Label _dayLabel;
    private Label _timeLabel;
    private Label _weatherLabel;
    private Label _populationLabel;
    private Label _currencyLabel;

    // Need bars
    private TextureProgressBar _hungerBar;
    private TextureProgressBar _thirstBar;
    private TextureProgressBar _fatigueBar;
    private TextureProgressBar _infectionBar;
    private TextureProgressBar _moraleBar;
    private TextureProgressBar _tempBar;

    private float _flashTimer = 0f;

    public override void _Ready()
    {
        _dayLabel = GetNode<Label>("VBoxContainer/DayLabel");
        _timeLabel = GetNode<Label>("VBoxContainer/TimeLabel");
        _weatherLabel = GetNode<Label>("VBoxContainer/WeatherLabel");
        _populationLabel = GetNode<Label>("VBoxContainer/PopulationLabel");
        _currencyLabel = GetNode<Label>("VBoxContainer/CurrencyLabel");

        _hungerBar = GetNode<TextureProgressBar>("VBoxContainer/HungerBar");
        _thirstBar = GetNode<TextureProgressBar>("VBoxContainer/ThirstBar");
        _fatigueBar = GetNode<TextureProgressBar>("VBoxContainer/FatigueBar");
        _infectionBar = GetNode<TextureProgressBar>("VBoxContainer/InfectionBar");
        _moraleBar = GetNode<TextureProgressBar>("VBoxContainer/MoraleBar");
        _tempBar = GetNode<TextureProgressBar>("VBoxContainer/TempBar");
    }

    public override void _Process(double delta)
    {
        int day = ConstraintField.Day;
        float time = ConstraintField.TimeOfDay;
        string period = time < 6.0f || time > 20.0f ? "Night" : "Day";

        _dayLabel.Text = $"Day {day}";
        _timeLabel.Text = $"Time: {time:F1}h ({period})";
        _weatherLabel.Text = $"Weather: {ConstraintField.Weather}";
        _populationLabel.Text = $"Population: {ConstraintField.Population}";

        if (ConstraintField.Day >= 8)
        {
            _currencyLabel.Text = "WARNING: CURRENCY TRUST BROKEN (DAY 8)";
            _flashTimer += (float)delta * 5.0f;
            float alpha = (Mathf.Sin(_flashTimer) + 1.0f) * 0.5f;
            _currencyLabel.Modulate = new Color(1, 0, 0, alpha); // Flashing red
        }
        else
        {
            _currencyLabel.Text = $"Cash: {(ConstraintField.CurrencyTrust * 100):F0}%";
            _currencyLabel.Modulate = new Color(1, 1, 1, 1);
        }

        _hungerBar.Value = ConstraintField.Hunger * 100;
        _thirstBar.Value = ConstraintField.Thirst * 100;
        _fatigueBar.Value = ConstraintField.Fatigue * 100;
        _infectionBar.Value = ConstraintField.Infection * 100;
        _moraleBar.Value = ConstraintField.Morale * 100;
        _tempBar.Value = ConstraintField.Temperature * 100;
    }
}
