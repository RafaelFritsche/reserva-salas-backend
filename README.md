# Reserva de Salas — Backend (ASP.NET Core 8)

Minimal API **sem banco** (dados em **sessão**).

## Rodar
```bash
dotnet restore
dotnet run
```
- A API sobe em `http://localhost:5080` 
- Endpoints principais: `/api/salas`, `/api/reservas`

## Decisões
- Persistência por sessão (`ISession`).
- Regra de conflito: mesma sala e sobreposição de intervalos.