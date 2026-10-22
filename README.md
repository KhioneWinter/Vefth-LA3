# JustGo.Flights — Airport Flight Operations Platform

JustGo.Flights is a bachelor-level integration assignment that simulates airport flight operations using **event-driven architecture**. Students implement a set of independent .NET services that communicate exclusively through asynchronous events over a message broker — no direct service-to-service calls.

## Learning Objectives

- Designing decoupled services that communicate via events rather than synchronous APIs
- Understanding publish/subscribe messaging with a message broker
- Implementing idempotent event handlers
- Modeling a realistic multi-step business process (flight lifecycle) across independently owned services
- Practicing the "service owns its own data" principle (database-per-service)

## Actors & Triggers

There is a single human actor in this system: **Airport Operations Staff**. All actions are initiated through one public API — the **Operations Console** — which is the only service exposing HTTP endpoints. Every other service is a pure background worker that reacts to events; none of them expose a public API.

| Action | Endpoint (Operations Console) |
|---|---|
| Create a flight | `POST /FlightScheduling` |
| Assign a gate | `POST /FlightScheduling/{flightId}/gate` |
| Delay a flight | `POST /FlightScheduling/{flightId}/delay` |
| Cancel a flight | `POST /FlightScheduling/{flightId}/cancel` |
| Open check-in | `POST /FlightScheduling/{flightId}/checkin/open` |
| Start boarding | `POST /FlightScheduling/{flightId}/boarding/start` |
| Complete boarding | `POST /FlightScheduling/{flightId}/boarding/complete` |
| Depart a flight | `POST /FlightScheduling/{flightId}/depart` |

Request bodies:

| Endpoint | Body |
|---|---|
| `POST /FlightScheduling` | `{ flightNumber, origin, destination, departureUtc }` |
| `.../gate` | `{ gate }` |
| `.../delay` | `{ newDepartureUtc, reason }` |
| `.../cancel` | `{ reason }` |
| `.../checkin/open`, `.../boarding/start`, `.../boarding/complete`, `.../depart` | none |

The Operations Console stores each flight's status and only allows it to move forward: `Scheduled → CheckInOpen → Boarding → BoardingCompleted → Departed`. A flight can be cancelled until boarding is completed, and delayed (to a later departure time only) until it is cancelled or departed. The gate can be assigned or changed at any time.

| Response | Meaning | Event published? |
|---|---|---|
| `202 Accepted` | State changed (create also returns `{ flightId }`) | Yes |
| `200 OK` | Flight is already in (or past) the requested state; nothing changed | No |
| `404 Not Found` | Unknown `flightId` | No |
| `409 Conflict` | Not allowed from the flight's current status | No |

No randomness, scheduled jobs, or simulated data generation is part of the baseline requirements — every event in the system originates from a deliberate staff action on the Operations Console.

## Core Components

| Service | Responsibility | Public API? | Template |
|---|---|---|---|
| **Operations Console** | Owns flight and gate data; entry point for all staff actions | Yes (only service with one) | ASP.NET Core Web API |
| **Check-in / Boarding Service** | Tracks passenger check-in and boarding progress; enforces valid state transitions | No | Worker Service |
| **Ground Operations Service** | Manages turnaround tasks (fueling, catering, cleaning) per flight | No | Worker Service |
| **Baggage Handling Service** | Tracks bag tagging/loading status per flight | No | Worker Service |
| **Notification Service** | Sends simulated passenger/staff notifications | No | Worker Service |
| **Dashboard** *(provided)* | Subscribes to every event and pushes it to the browser over SignalR, for setup verification | N/A | Blazor Web App (WebAssembly interactivity) |

All student-built services are implemented in **.NET 10**, use **Rebus** over **RabbitMQ** for messaging, and **PostgreSQL** (via EF Core) for any service that needs to persist its own state. Each service owns its own data — no service reads another service's tables:

| Service | Database | Schema |
|---|---|---|
| Operations Console | `justgo_operationsconsole` | `public` |
| Check-in / Boarding | `justgo_flights` | `checkinboarding` |
| Ground Operations | `justgo_flights` | `groundoperations` |
| Baggage Handling | `justgo_flights` | `baggagehandling` |
| Notification | `justgo_flights` | `notification` |

Each worker keeps its own `__EFMigrationsHistory` table in its own schema. Every service applies its EF Core migrations on startup when running in the `Development` environment. The Dashboard stores nothing.

## Solution Structure
```
JustGo.Flights/
├── JustGo.Flights.sln
├── JustGo.Flights.Contracts/ (shared event definitions only)
├── JustGo.Flights.OperationsConsole/ (Web API)
├── JustGo.Flights.CheckInBoarding/ (Worker Service)
├── JustGo.Flights.GroundOperations/ (Worker Service)
├── JustGo.Flights.BaggageHandling/ (Worker Service)
├── JustGo.Flights.Notification/ (Worker Service)
├── JustGo.Flights.Dashboard/ (Blazor Web App, provided)
│ ├── JustGo.Flights.Dashboard/ (server: Rebus subscriber + SignalR hub)
│ └── JustGo.Flights.Dashboard.Client/ (WebAssembly UI)
├── docker/postgres-init/ (creates justgo_operationsconsole on first start)
└── docker-compose.yml (RabbitMQ + PostgreSQL)
```


`JustGo.Flights.Contracts` is the only project referenced by every service. It contains event record definitions exclusively — no shared logic, no shared database context.

## Messaging Topology

- **One input queue per service.** Queue count scales with the number of services, not the number of event types.
- **One exchange per event type**, created automatically by Rebus when a service publishes or subscribes to that event.
- A service subscribes to an event by binding its single input queue to that event's exchange — a service reacting to multiple event types still has only one queue, with multiple bindings.
- The Operations Console is a **publish-only** client (`UseRabbitMqAsOneWayClient`) — it never subscribes to events and has no input queue of its own.
- Every subscribing service has an input queue `justgo.flights.<service>.input` and an error queue `justgo.flights.<service>.error` (for example `justgo.flights.groundoperations.input`), configured in its `appsettings.json`.

## Event Contracts

| Event | Payload | Published by |
|---|---|---|
| `FlightScheduled` | `FlightId, FlightNumber, Origin, Destination, DepartureTime` | Operations Console |
| `GateAssigned` | `FlightId, Gate` | Operations Console |
| `FlightDelayed` | `FlightId, NewDepartureTime, Reason` | Operations Console |
| `TurnaroundRescheduled` | `FlightId, NewTurnaroundTime` | Ground Operations |
| `CheckInOpened` | `FlightId, OpenedAt` | Operations Console |
| `BoardingStarted` | `FlightId, StartedAt` | Operations Console |
| `BoardingCompleted` | `FlightId, CompletedAt` | Operations Console |
| `FlightCancelled` | `FlightId, Reason` | Operations Console |
| `FlightDeparted` | `FlightId, DepartedAt` | Operations Console |

`FlightId` is a `Guid`; all times are `DateTimeOffset`; all other fields are `string`.

## Domain Events & Reactions

### `FlightScheduled`

| Service | Reaction |
|---|---|
| Ground Operations | Create default turnaround tasks, planned relative to departure: cleaning −60 min, catering −50 min, fueling −40 min |
| Dashboard | Display new flight |

### `GateAssigned`

| Service | Reaction |
|---|---|
| Ground Operations | Attach gate to the flight's turnaround (creates the turnaround if `FlightScheduled` has not arrived yet) |
| Check-in/Boarding | Record assigned gate |
| Dashboard | Display gate assignment |

### `FlightDelayed`

| Service | Reaction |
|---|---|
| Notification | Send delay notice to passengers |
| Ground Operations | Shift pending turnaround tasks by the delay and publish `TurnaroundRescheduled` (with the new departure time); ignored once boarding has started |
| Check-in/Boarding | Record the new departure time (the boarding window follows it) |
| Dashboard | Display delay |

### `TurnaroundRescheduled`

| Service | Reaction |
|---|---|
| Baggage Handling | Reschedule bag loading to the new turnaround time |
| Dashboard | Display updated turnaround time |

### `CheckInOpened`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Mark check-in as open |
| Ground Operations | Schedule cleaning/catering crew access |
| Dashboard | Display check-in status |

### `BoardingStarted`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Mark boarding as started |
| Ground Operations | Finalize the turnaround (delays no longer move its tasks); log a warning if tasks are unfinished |
| Baggage Handling | Mark the bag load as verified (status change only; there is no bag data) |
| Dashboard | Display boarding status |

### `BoardingCompleted`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Mark boarding as completed |
| Ground Operations | Mark remaining turnaround tasks and the turnaround as completed (the gate stays assigned until departure) |
| Baggage Handling | Finalize baggage load confirmation |
| Dashboard | Display boarding completed |

### `FlightCancelled`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Close check-in, cancel boarding |
| Ground Operations | Cancel pending turnaround tasks, release gate |
| Baggage Handling | Mark the bag load as offloaded |
| Notification | Send cancellation/refund notice |
| Dashboard | Display cancellation live |

### `FlightDeparted`

| Service | Reaction |
|---|---|
| Ground Operations | Release gate, mark the turnaround closed |
| Check-in/Boarding | Close out flight record |
| Dashboard | Display flight departed |

## Requirements

- All services must be idempotent — duplicate delivery of the same event must not corrupt state (e.g., a repeated `FlightDelayed` must not double-reschedule already-rescheduled tasks).
- Services must not call each other's APIs directly; all cross-service communication happens via published events.
- Each service owns and persists only its own domain state.
- Events may arrive out of order (for example `GateAssigned` before `FlightScheduled`, or `BoardingStarted` before `CheckInOpened`). Workers that never receive `FlightScheduled` create their record from the first event they see for a flight, and never move a flight backwards or out of a final state (cancelled/closed).
- Notification records every notice it sends (one per cancellation, one per distinct new departure time) so a redelivered event does not notify passengers twice.

## Out of Scope (Baseline)

- Sagas / stateful process orchestration (optional bonus only)
- Randomized or simulated flight data generation
- Authentication / staff roles
- Real flight data integration

## Getting Started

1. From the `JustGo.Flights/` folder, run `docker compose up` to start RabbitMQ (management UI at `http://localhost:15672`, `user`/`password`) and PostgreSQL (`postgres`/`postgres`).
2. Build the solution: `dotnet build JustGo.Flights.sln`.
3. Run each service (Operations Console + the four worker services) individually in the `Development` environment; migrations are applied on startup.
4. Run the Dashboard to observe live events as you exercise the Operations Console API via Swagger UI at `/swagger`.