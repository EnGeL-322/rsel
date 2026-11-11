using Comercium.Application;
using Comercium.Domain;
using Microsoft.EntityFrameworkCore;

namespace Comercium.Infrastructure.Persistence;

public sealed class Strategy(StoreDatabase database) : IStrategy
{
    public PlanProfile Profile(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Plan);
        var plan = db.Plans.Find(1) ?? new StrategyPlan();
        return new PlanProfile(plan.Mission, plan.Vision, plan.Values, plan.Strengths, plan.Opportunities, plan.Weaknesses, plan.Threats);
    }

    public void SaveProfile(Session session, PlanProfile input)
    {
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Plan);
        var plan = EnsurePlan(db);
        plan.Mission = Text(input.Mission);
        plan.Vision = Text(input.Vision);
        plan.Values = Text(input.Values);
        plan.Strengths = Text(input.Strengths);
        plan.Opportunities = Text(input.Opportunities);
        plan.Weaknesses = Text(input.Weaknesses);
        plan.Threats = Text(input.Threats);
        StoreAccess.Log(db, session.UserId, DateTime.UtcNow, "Plan actualizado", "Identidad y FODA");
        db.SaveChanges();
        transaction.Commit();
    }

    public IReadOnlyList<Goal> Goals(Session session)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Plan);
        return db.Objectives.OrderBy(goal => goal.Completed).ThenBy(goal => goal.Due)
            .Select(goal => new Goal(goal.Id, goal.Name, goal.Strategy, goal.Evaluation, goal.Indicator, goal.Unit, goal.Target, goal.Due, goal.Completed)).ToList();
    }

    public long SaveGoal(Session session, Goal input)
    {
        var name = StoreAccess.Required(input.Name, "El objetivo", 140);
        var indicator = StoreAccess.Required(input.Indicator, "El indicador", 120);
        var unit = StoreAccess.Required(input.Unit, "La unidad", 40);
        if (input.Due == default || input.Target is < -1_000_000_000_000m or > 1_000_000_000_000m)
            throw new BusinessException("Revisa la meta y su fecha límite.");
        using var db = database.Open();
        using var transaction = db.Database.BeginTransaction();
        StoreAccess.Demand(db, session, Permission.Plan);
        EnsurePlan(db);
        var goal = input.Id == 0 ? new Objective() : db.Objectives.Find(input.Id) ?? throw new BusinessException("El objetivo no existe.");
        goal.Name = name;
        goal.Strategy = Text(input.Strategy);
        goal.Evaluation = Text(input.Evaluation);
        goal.Indicator = indicator;
        goal.Unit = unit;
        goal.Target = input.Target;
        goal.Due = input.Due;
        goal.Completed = input.Completed;
        if (input.Id == 0)
            db.Objectives.Add(goal);
        db.SaveChanges();
        StoreAccess.Log(db, session.UserId, DateTime.UtcNow, "Objetivo guardado", $"Objetivo {goal.Id}");
        db.SaveChanges();
        transaction.Commit();
        return goal.Id;
    }

    public IReadOnlyList<Reading> Readings(Session session, long goal)
    {
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Plan);
        return (from row in db.Set<IndicatorReading>() join user in db.Users on row.UserId equals user.Id
                where row.ObjectiveId == goal orderby row.Id descending select new Reading(row.AtUtc, row.Value, row.Note, user.Name)).ToList();
    }

    public void Measure(Session session, long goal, decimal value, string note)
    {
        if (value is < -1_000_000_000_000m or > 1_000_000_000_000m)
            throw new BusinessException("Ingresa una medición válida.");
        using var db = database.Open();
        StoreAccess.Demand(db, session, Permission.Plan);
        if (!db.Objectives.Any(row => row.Id == goal))
            throw new BusinessException("Guarda el objetivo antes de medirlo.");
        var now = DateTime.UtcNow;
        db.Add(new IndicatorReading { ObjectiveId = goal, AtUtc = now, UserId = session.UserId, Value = value, Note = Text(note) });
        StoreAccess.Log(db, session.UserId, now, "Indicador medido", $"Objetivo {goal}");
        db.SaveChanges();
    }

    private static StrategyPlan EnsurePlan(StoreContext db) => db.Plans.Find(1) ?? db.Plans.Add(new StrategyPlan { Id = 1 }).Entity;

    private static string Text(string text) => text.Length <= 4000 ? text.Trim()
        : throw new BusinessException("Cada campo descriptivo admite hasta 4.000 caracteres.");
}
