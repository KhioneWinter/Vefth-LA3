# JustGo.Flights — Airport Flight Operations Platform

JustGo.Flights simulates airport flight operations using **event-driven architecture**. The system is a set of independent .NET services that communicate only through asynchronous events over a message broker (RabbitMQ with Rebus). No service calls another service directly.


## Getting Started

**Prerequisites:** Docker Desktop (with Docker Compose).

1. From the **project root** (the folder containing `docker-compose.yml`), create the `.env` file from the example:
   ```bash
   cp .env.example .env
   ```
   On Windows PowerShell:
   ```powershell
   Copy-Item .env.example .env
   ```
   The user and password in `FLIGHTSDB_CONNECTION` must match `POSTGRES_USER`/`POSTGRES_PASSWORD`, and the ones in `RABBITMQ_CONNECTION` must match `RABBITMQ_USER`/`RABBITMQ_PASS`. Docker Compose passes these to every service as `ConnectionStrings__FlightsDb` and `ConnectionStrings__RabbitMq`. `.env` is excluded from Git.

2. Start everything:
   ```bash
   docker compose up --build
   ```
   This builds and starts PostgreSQL, RabbitMQ, the Operations Console, the four worker services and the Dashboard. The services wait until PostgreSQL and RabbitMQ are healthy, and migrations are applied on startup.

3. Open:

   | What | URL |
   |---|---|
   | Operations Console (Swagger UI) | http://localhost:5249/swagger |
   | Dashboard | http://localhost:5117 |
   | RabbitMQ management | http://localhost:15672 (user and password from `.env`) |
   | PostgreSQL | `localhost:5433` |

4. Use Swagger to create a flight and move it through its lifecycle, and watch the events appear on the Dashboard. To see how each worker reacted:
   ```bash
   docker compose logs groundoperations
   docker compose logs notification
   ```

5. To test that no events are lost while a worker is down:
   ```bash
   docker compose stop notification
   # delay a flight in Swagger; the event waits in justgo.flights.notification.input
   docker compose start notification
   # the delay notice is sent as soon as the service is back
   ```

6. To stop everything:
   ```bash
   docker compose down        # keeps the database
   docker compose down -v     # also deletes the database volume (fresh start)
   ```



## Actors & Triggers

There is a single human actor in this system: **Airport Operations Staff**. All actions are started through one public API, the **Operations Console**, which is the only service exposing HTTP endpoints. Every other service is a background worker that reacts to events. None of them expose a public API.

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

The Operations Console stores each flight's status and only allows it to move forward: `Scheduled → CheckInOpen → Boarding → BoardingCompleted → Departed`. A flight can be cancelled until boarding is completed. It can be delayed (to a later departure time only) until it is cancelled or departed. The gate can be assigned or changed until the flight is cancelled or departed.

| Response | Meaning | Event published? |
|---|---|---|
| `202 Accepted` | State changed (create also returns `{ flightId }`) | Yes |
| `200 OK` | The flight is already in (or past) the requested state, so nothing changed | No |
| `404 Not Found` | Unknown `flightId` | No |
| `409 Conflict` | Not allowed from the flight's current status | No |

Every event in the system starts from a deliberate staff action on the Operations Console. There is no randomness, no scheduled job and no simulated data generation.

## Core Components

| Service | Responsibility | Public API? | Template |
|---|---|---|---|
| **Operations Console** | Owns flight and gate data; entry point for all staff actions | Yes (the only service with one) | ASP.NET Core Web API (Swagger) |
| **Check-in / Boarding Service** | Tracks gate, departure time and status per flight; enforces valid state transitions | No | Worker Service |
| **Ground Operations Service** | Manages the turnaround and its tasks (fueling, catering, cleaning) per flight | No | Worker Service |
| **Baggage Handling Service** | Tracks bag load status and loading time per flight | No | Worker Service |
| **Notification Service** | Sends delay and cancellation notices to staff (written to the log and stored in the database) | No | Worker Service |
| **Dashboard** *(provided)* | Subscribes to every event and pushes it to the browser over SignalR | N/A | Blazor Web App (WebAssembly interactivity) |

All services are implemented in **.NET 10**. They use **Rebus** over **RabbitMQ** for messaging and **PostgreSQL** (via EF Core) to persist their own state. All services share one database, `justgo_flights`, but each service has its **own schema** and never reads another service's tables:

| Service | Database | Schema |
|---|---|---|
| Operations Console | `justgo_flights` | `operations_console` |
| Check-in / Boarding | `justgo_flights` | `checkin_boarding` |
| Ground Operations | `justgo_flights` | `ground_operations` |
| Baggage Handling | `justgo_flights` | `baggage_handling` |
| Notification | `justgo_flights` | `notification` |

The schemas are created by the PostgreSQL init script `db/init/01-schemas.sql` when the database container first starts. Each service keeps its own `__EFMigrationsHistory` table in its own schema and applies its EF Core migrations on startup. The Dashboard stores nothing.

## Solution Structure
```
.
├── docker-compose.yml                  (all services + RabbitMQ + PostgreSQL)
├── .env.example                        (template for .env: credentials and connection strings)
├── db/
│   └── init/
│       └── 01-schemas.sql              (creates one schema per service on first start)
└── JustGo.Flights/
    ├── JustGo.Flights.sln
    ├── .dockerignore                   (keeps bin/ and obj/ out of Docker builds)
    ├── JustGo.Flights.Contracts/       (shared event definitions only)
    ├── JustGo.Flights.OperationsConsole/   (Web API: Controllers, Models, Data, Migrations, Dockerfile)
    ├── JustGo.Flights.CheckInBoarding/     (Worker: Handlers, Models, Data, Migrations, Dockerfile)
    ├── JustGo.Flights.GroundOperations/    (Worker: Handlers, Models, Data, Migrations, Dockerfile)
    ├── JustGo.Flights.BaggageHandling/     (Worker: Handlers, Models, Data, Migrations, Dockerfile)
    ├── JustGo.Flights.Notification/        (Worker: Handlers, Models, Data, Migrations, Dockerfile)
    └── JustGo.Flights.Dashboard/           (Blazor Web App, provided, + Dockerfile)
        ├── JustGo.Flights.Dashboard/         (server: Rebus subscriber + SignalR hub)
        └── JustGo.Flights.Dashboard.Client/  (WebAssembly UI)
```

Every project references `JustGo.Flights.Contracts`. It contains event record definitions only, with no shared logic and no shared database context.

Each worker service follows the same layout:
- `Program.cs`: registers EF Core and Rebus (input queue, error queue, retry strategy, subscriptions) and applies migrations on startup.
- `Handlers/`: one handler class implementing `IHandleMessages<T>` for every event the service reacts to.
- `Data/`: the `DbContext`, configured with the service's own schema.
- `Models/`: entities and status enums.
- `Migrations/`: EF Core migrations.

## Messaging Topology

- **One input queue per service.** The number of queues grows with the number of services, not the number of event types.
- **Publish/subscribe only.** Events are distributed with `bus.Publish` and `bus.Subscribe<T>`. `bus.Send` is never used.
- A service subscribes to an event by binding its single input queue to that event's topic. A service reacting to several event types still has only one queue, with several bindings.
- The Operations Console is a **publish-only** client (`UseRabbitMqAsOneWayClient`). It never subscribes to events and has no input queue of its own.
- Every subscribing service has an input queue `justgo.flights.<service>.input` and an error queue `justgo.flights.<service>.error`:

| Service | Input queue | Error queue |
|---|---|---|
| Check-in / Boarding | `justgo.flights.checkinboarding.input` | `justgo.flights.checkinboarding.error` |
| Ground Operations | `justgo.flights.groundoperations.input` | `justgo.flights.groundoperations.error` |
| Baggage Handling | `justgo.flights.baggagehandling.input` | `justgo.flights.baggagehandling.error` |
| Notification | `justgo.flights.notification.input` | `justgo.flights.notification.error` |
| Dashboard | `justgo.flights.dashboard.input` | `justgo.flights.dashboard.error` |

The worker queue names are set in each worker's `appsettings.Development.json`, and the Dashboard's are in its `appsettings.json`.

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

All fields are `string` other then `FlightId` which is a `Guid` and all times which are `DateTimeOffset`.

## Domain Events & Reactions

### `FlightScheduled`

| Service | Reaction |
|---|---|
| Ground Operations | Create the turnaround (status `Open`) with three `Pending` tasks, planned relative to departure: fueling −60 min, catering −45 min, cleaning −30 min |
| Dashboard | Display new flight |

### `GateAssigned`

| Service | Reaction |
|---|---|
| Ground Operations | Store the gate on the flight's turnaround (creates the turnaround without tasks if `FlightScheduled` has not arrived yet) |
| Check-in/Boarding | Record the assigned gate |
| Dashboard | Display gate assignment |

### `FlightDelayed`

| Service | Reaction |
|---|---|
| Notification | Send a delay notice to staff (once per flight and new departure time) |
| Ground Operations | Move every `Pending` task later by the difference between the new and previous departure time, then publish `TurnaroundRescheduled` with the earliest `Pending` task time. Nothing is published if no tasks are pending. Once the turnaround is `Finalized` (boarding has started), only the departure time is updated. |
| Check-in/Boarding | Record the new departure time (the boarding window always ends at departure) |
| Dashboard | Display delay |

### `TurnaroundRescheduled`

| Service | Reaction |
|---|---|
| Baggage Handling | Set the loading time to the new turnaround time |
| Dashboard | Display updated turnaround time |

### `CheckInOpened`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Set the status to `CheckInOpen` |
| Ground Operations | Mark cleaning/catering crew access as scheduled |
| Dashboard | Display check-in status |

### `BoardingStarted`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Set the status to `Boarding` |
| Ground Operations | Set the turnaround to `Finalized` (delays no longer move its tasks) |
| Baggage Handling | Set the bag load status to `Verified` |
| Dashboard | Display boarding status |

### `BoardingCompleted`

| Service | Reaction |
|---|---|
| Ground Operations | Set all `Pending` tasks to `Completed` and mark the gate as cleared for departure |
| Baggage Handling | Set the bag load status to `Finalized` |
| Dashboard | Display boarding completed |

### `FlightCancelled`

| Service | Reaction |
|---|---|
| Check-in/Boarding | Set the status to `Cancelled` (closes check-in, cancels boarding) |
| Ground Operations | Set all `Pending` tasks to `Cancelled`, release the gate and set the turnaround to `Cancelled` |
| Baggage Handling | Set the bag load status to `Offloaded` |
| Notification | Send a cancellation notice to staff (once per flight) |
| Dashboard | Display cancellation live |

### `FlightDeparted`

| Service | Reaction |
|---|---|
| Ground Operations | Release the gate and set the turnaround to `Closed` |
| Check-in/Boarding | Set the status to `Closed` (closes out the flight record) |
| Dashboard | Display flight departed |

## Requirements

- **Idempotent handlers.** Every handler checks the current state before changing anything, so duplicate delivery of the same event does not corrupt state. For example, a repeated `FlightDelayed` does not move already-rescheduled tasks again, and a second `FlightCancelled` does nothing.
- **Out-of-order events.** Events may arrive out of order, for example `GateAssigned` before `FlightScheduled`, or `BoardingStarted` before `CheckInOpened`. A worker that receives an event for an unknown flight creates the record from that event and continues. Statuses never move backwards or out of a final state (`Cancelled`/`Closed`/`Offloaded`), and a stored departure time is never replaced by an earlier one.
- **No duplicate notices.** The Notification service records every notice it sends. A unique index on `(FlightId, Type, NewDepartureUtc)` enforces one notice per cancellation and one per distinct new departure time, so a redelivered event never notifies staff twice.
- **Retries and error queues.** Each worker uses the Rebus retry strategy with the default settings. A message that keeps failing is moved to that service's error queue.
- **No lost events on restart.** Queues are durable, so a worker can be stopped while events are published and will handle them when it starts again.
- **Logging.** Every handled event is logged together with its outcome.
- Services never call each other's APIs directly; all cross-service communication happens through published events.
- Each service owns and persists only its own domain state.



