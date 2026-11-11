using System.Globalization;
using System.Windows.Input;
using Comercium.Application;
using Comercium.Domain;

namespace Comercium.App.ViewModels;

public sealed class StrategyModel : ViewModel
{
    private readonly IStrategy service;
    private readonly Session session;
    private IReadOnlyList<Goal> goals = [];
    private IReadOnlyList<Reading> readings = [];
    private Goal? selected;
    private GoalForm form = new();
    private string value = "";
    private string note = "";

    public StrategyModel(Session session, DesktopDialogs dialogs, IStrategy service) : base(dialogs)
    {
        this.service = service;
        this.session = session;
        SaveProfile = Action(() =>
        {
            service.SaveProfile(session, new PlanProfile(Fields[0].Text, Fields[1].Text, Fields[2].Text, Fields[3].Text,
                Fields[4].Text, Fields[5].Text, Fields[6].Text));
            Show("Identidad y FODA guardados.");
        });
        New = Action(() => Selected = null);
        SaveGoal = Action(() =>
        {
            var date = Form.Due ?? throw new BusinessException("Selecciona una fecha límite.");
            var id = service.SaveGoal(session, new Goal(Form.Id, Form.Name, Form.Strategy, Form.Evaluation, Form.Indicator,
                Form.Unit, Number(Form.Target), DateOnly.FromDateTime(date), Form.Completed));
            Refresh(id);
            Show("Objetivo guardado.");
        });
        Measure = Action(() =>
        {
            if (Selected is not { } goal) return;
            service.Measure(session, goal.Id, Number(Value), Note);
            Value = "";
            Note = "";
            Readings = service.Readings(session, goal.Id);
            Notify(nameof(Progress));
            Show("Medición guardada.");
        }, () => Selected != null);
        Run(() =>
        {
            var profile = service.Profile(session);
            Fields = [new("Misión", profile.Mission), new("Visión", profile.Vision), new("Valores", profile.Values),
                new("Fortalezas", profile.Strengths), new("Oportunidades", profile.Opportunities),
                new("Debilidades", profile.Weaknesses), new("Amenazas", profile.Threats)];
            Notify(nameof(Fields));
            Refresh();
        });
    }

    public IReadOnlyList<PlanField> Fields { get; private set; } = [];
    public IReadOnlyList<Goal> Goals { get => goals; private set => Set(ref goals, value); }
    public IReadOnlyList<Reading> Readings { get => readings; private set => Set(ref readings, value); }
    public GoalForm Form { get => form; private set => Set(ref form, value); }
    public string Value { get => value; set => Set(ref this.value, value); }
    public string Note { get => note; set => Set(ref note, value); }
    public string Progress => Selected is { } goal && Readings.FirstOrDefault() is { } latest
        ? $"Última medición {latest.Value:N2} {goal.Unit} · Meta {goal.Target:N2} {goal.Unit}" : "Selecciona un objetivo con mediciones para consultar el avance.";
    public Goal? Selected
    {
        get => selected;
        set
        {
            Set(ref selected, value);
            Form = value == null ? new GoalForm() : new GoalForm { Id = value.Id, Name = value.Name, Strategy = value.Strategy,
                Evaluation = value.Evaluation, Indicator = value.Indicator, Unit = value.Unit, Target = value.Target.ToString(CultureInfo.CurrentCulture),
                Due = value.Due.ToDateTime(TimeOnly.MinValue), Completed = value.Completed };
            Run(() => { Readings = value == null ? [] : service.Readings(session, value.Id); Notify(nameof(Progress)); });
        }
    }
    public ICommand SaveProfile { get; }
    public ICommand New { get; }
    public ICommand SaveGoal { get; }
    public ICommand Measure { get; }

    private void Refresh(long? id = null)
    {
        Goals = service.Goals(session);
        Selected = Goals.FirstOrDefault(row => row.Id == id);
    }

    private static decimal Number(string text) => decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint
        | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, CultureInfo.CurrentCulture, out var number)
        ? number : throw new BusinessException("Ingresa un número válido para la meta o la medición.");
}

public sealed class PlanField(string name, string text)
{
    public string Name { get; } = name;
    public string Text { get; set; } = text;
}

public sealed class GoalForm
{
    public long Id { get; init; }
    public string Name { get; set; } = "";
    public string Strategy { get; set; } = "";
    public string Evaluation { get; set; } = "";
    public string Indicator { get; set; } = "";
    public string Unit { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTime? Due { get; set; } = DateTime.Today.AddMonths(1);
    public bool Completed { get; set; }
}
