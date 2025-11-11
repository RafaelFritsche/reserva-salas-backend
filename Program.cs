using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.Cookie.Name = ".ReservaSalas.Sessao";
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()
        .SetIsOriginAllowed(_ => true)
    );
});
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.WriteIndented = true);

var app = builder.Build();

app.UseCors();
app.UseSession();

app.MapGet("/", () => Results.Ok(new { ok = true, api = "ReservaSalas" }));

app.MapGet("/api/salas", (HttpContext ctx) =>
    Results.Ok(SessionStore.GetSalas(ctx.Session)));

app.MapPost("/api/salas", (HttpContext ctx, [FromBody] Sala body) =>
{
    if (string.IsNullOrWhiteSpace(body.Nome) || body.Capacidade <= 0)
        return Results.BadRequest(new { erro = "Nome e capacidade são obrigatórios." });

    var salas = SessionStore.GetSalas(ctx.Session);
    var nova = body with { Id = Guid.NewGuid() };
    salas.Add(nova);
    SessionStore.SetSalas(ctx.Session, salas);
    return Results.Created($"/api/salas/{nova.Id}", nova);
});

app.MapPut("/api/salas/{id:guid}", (HttpContext ctx, Guid id, [FromBody] Sala body) =>
{
    var salas = SessionStore.GetSalas(ctx.Session);
    var idx = salas.FindIndex(s => s.Id == id);
    if (idx < 0) return Results.NotFound();

    var atualizada = body with { Id = id };
    salas[idx] = atualizada;
    SessionStore.SetSalas(ctx.Session, salas);
    return Results.Ok(atualizada);
});

app.MapDelete("/api/salas/{id:guid}", (HttpContext ctx, Guid id) =>
{
    var salas = SessionStore.GetSalas(ctx.Session);
    salas.RemoveAll(s => s.Id == id);
    SessionStore.SetSalas(ctx.Session, salas);

    var res = SessionStore.GetReservas(ctx.Session);
    res.RemoveAll(r => r.SalaId == id);
    SessionStore.SetReservas(ctx.Session, res);

    return Results.NoContent();
});

static bool Conflita(Reserva novo, Reserva existente) =>
    novo.SalaId == existente.SalaId &&
    novo.Id != existente.Id &&
    !(novo.Fim <= existente.Inicio || novo.Inicio >= existente.Fim);

app.MapGet("/api/reservas", (HttpContext ctx) =>
    Results.Ok(SessionStore.GetReservas(ctx.Session)));

app.MapPost("/api/reservas", (HttpContext ctx, [FromBody] Reserva body) =>
{
    if (body.SalaId == Guid.Empty)
        return Results.BadRequest(new { erro = "Selecione uma sala." });
    if (string.IsNullOrWhiteSpace(body.Titulo))
        return Results.BadRequest(new { erro = "Informe um título." });
    if (body.Inicio >= body.Fim)
        return Results.BadRequest(new { erro = "Data/hora de fim deve ser depois do início." });

    var salas = SessionStore.GetSalas(ctx.Session);
    if (!salas.Any(s => s.Id == body.SalaId))
        return Results.BadRequest(new { erro = "Sala inválida." });

    var reservas = SessionStore.GetReservas(ctx.Session);
    if (reservas.Any(r => Conflita(body, r)))
        return Results.BadRequest(new { erro = "Conflito de horário para a sala." });

    var nova = body with { Id = Guid.NewGuid() };
    reservas.Add(nova);
    SessionStore.SetReservas(ctx.Session, reservas);
    return Results.Created($"/api/reservas/{nova.Id}", nova);
});

app.MapPut("/api/reservas/{id:guid}", (HttpContext ctx, Guid id, [FromBody] Reserva body) =>
{
    if (body.SalaId == Guid.Empty)
        return Results.BadRequest(new { erro = "Selecione uma sala." });
    if (string.IsNullOrWhiteSpace(body.Titulo))
        return Results.BadRequest(new { erro = "Informe um título." });
    if (body.Inicio >= body.Fim)
        return Results.BadRequest(new { erro = "Data/hora de fim deve ser depois do início." });

    var salas = SessionStore.GetSalas(ctx.Session);
    if (!salas.Any(s => s.Id == body.SalaId))
        return Results.BadRequest(new { erro = "Sala inválida." });

    var reservas = SessionStore.GetReservas(ctx.Session);
    var idx = reservas.FindIndex(r => r.Id == id);
    if (idx < 0) return Results.NotFound();

    var atualizada = body with { Id = id };

    if (reservas.Any(r => Conflita(atualizada, r)))
        return Results.BadRequest(new { erro = "Conflito de horário para a sala." });

    reservas[idx] = atualizada;
    SessionStore.SetReservas(ctx.Session, reservas);
    return Results.Ok(atualizada);
});

app.MapDelete("/api/reservas/{id:guid}", (HttpContext ctx, Guid id) =>
{
    var reservas = SessionStore.GetReservas(ctx.Session);
    reservas.RemoveAll(r => r.Id == id);
    SessionStore.SetReservas(ctx.Session, reservas);
    return Results.NoContent();
});

app.Run();

public record Sala(Guid Id, string Nome, int Capacidade);
public record Reserva(Guid Id, Guid SalaId, string Titulo, DateTime Inicio, DateTime Fim);

public static class SessionStore
{
    public static List<Sala> GetSalas(ISession s) =>
        System.Text.Json.JsonSerializer.Deserialize<List<Sala>>(s.GetString("salas") ?? "[]")!;

    public static void SetSalas(ISession s, List<Sala> v) =>
        s.SetString("salas", System.Text.Json.JsonSerializer.Serialize(v));

    public static List<Reserva> GetReservas(ISession s) =>
        System.Text.Json.JsonSerializer.Deserialize<List<Reserva>>(s.GetString("reservas") ?? "[]")!;

    public static void SetReservas(ISession s, List<Reserva> v) =>
        s.SetString("reservas", System.Text.Json.JsonSerializer.Serialize(v));
}
