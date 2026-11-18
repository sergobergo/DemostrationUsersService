# UserService

![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)
![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-Web%20API-512BD4?style=flat-square)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=flat-square)
![Redis](https://img.shields.io/badge/Redis-DC382D?style=flat-square)
![RabbitMQ](https://img.shields.io/badge/RabbitMQ-FF6600?style=flat-square)
![Docker](https://img.shields.io/badge/Docker-2496ED?style=flat-square)
![MassTransit](https://img.shields.io/badge/MassTransit-8.3.6-blue?style=flat-square)

Микросервис регистрации и аутентификации пользователей на **.NET 8 / ASP.NET Core Web API** с Clean Architecture, JWT-авторизацией, подтверждением email по коду, асинхронной отправкой писем через RabbitMQ и полным покрытием тестами.

## Features

- **Регистрация с подтверждением email**
  - Генерация 6-значного буквенно-цифрового кода (`AuthCodeGenerator`)
  - Хранение кода в Redis с TTL, повторная отправка кода
  - Проверка уникальности логина на этапе верификации
- **Авторизация**
  - JWT Access Token + Refresh Token с ротацией
  - Хранение `JTI` в БД, `JtiCheckMiddleware` для отзыва токенов
  - Проверка пароля на утечку через Have I Been Pwned API (k-Anonymity, SHA1)
- **Асинхронные уведомления**
  - `MessageExchangerService` -> RabbitMQ (MassTransit) -> `EmailConsumer` -> `SmtpConnectionService`
  - Автоматическое переподключение к SMTP, retry-логика отправки
  - `EmailService<T>` с валидацией DTO через FluentValidation
- **Инфраструктура**
  - Глобальный обработчик исключений с маппингом бизнес-исключений в HTTP-статусы
  - Валидация входных данных FluentValidation
  - Структурированное логирование Serilog
  - Multi-stage Dockerfile: `development` с `dotnet watch` и `production`
  - Docker Compose с healthcheck'ами и `depends_on`
  - CI/CD на GitHub Actions: тесты + сборка образа в GHCR

## Tech Stack

| Слой | Технологии |
|---|---|
| **Backend** | .NET 8, C#, ASP.NET Core Web API, MediatR, FluentValidation |
| **Data** | Entity Framework Core, PostgreSQL (Npgsql), Redis (StackExchange.Redis) |
| **Messaging / Mail** | RabbitMQ, MassTransit, MailKit (SMTP) |
| **Auth** | JWT Bearer, Refresh Token Rotation, JTI |
| **Logging** | Serilog |
| **Tests** | xUnit, NSubstitute, FluentAssertions, WebApplicationFactory, Testcontainers |
| **DevOps** | Docker, Docker Compose, GitHub Actions, GHCR |

## Architecture

flowchart LR
    Client[Client] --> API[UserService.API]
    API --> App[UsersService.Application]
    App --> DB[(PostgreSQL)]
    App --> Redis[(Redis)]
    App --> MQ[RabbitMQ / MassTransit]
    MQ --> Consumer[EmailConsumer]
    Consumer --> SMTP[SmtpConnectionService]
    SMTP --> MailPit[MailPit SMTP]

**Clean Architecture:**

- `UserService.API` — Controllers, Middleware (`GlobalExceptionHandler`, `JtiCheckMiddleware`), DI-расширения, `Program.cs`
- `UsersService.Application` — Сервисы (`AuthService`, `UserVerificationService`, `JtiCheckService`, `PwnedPasswordService`, `RedisService`), Features (Validation, Mappers), NotificationService
- `UsersService.Domain` — `UserServiceDbContext`, `UserEntity`, конфигурации EF Core, миграции

## Project Structure

```
UsersService/
├── UserService.API/                  # Web API, entrypoint
│   ├── Controllers/                  # AuthController
│   ├── Middleware/                   # GlobalExceptionHandler, JtiCheckMiddleware
│   ├── Behaviors/                    # LoggingPipelineBehavior
│   └── Extensions/                   # UserServiceCollectionExtensions
├── UsersService.Application/
│   ├── UsersService/                 # Auth, Verification, Redis, PwnedPassword
│   └── NotificationService/          # EmailService, MessageExchanger, SmtpConnection, EmailConsumer
├── UsersService.Domain/              # DbContext, Entities, Migrations
├── UserService.UnitTests/            # Unit-тесты сервисов и валидаторов
├── UserService.IntegrationTests/     # Integration + E2E тесты с Testcontainers
├── Dockerfile
├── docker-compose.yaml
├── .env.example
└── .github/workflows/                # ci.yml
```

## Getting Started

### Prerequisites

- .NET SDK 8.0
- Docker Desktop (для запуска через Compose и Testcontainers)

### Configuration

Локально конфигурация в `appsettings.json` / `appsettings.Development.json`:

```json
{
  "JwtSettings": {
    "Key": "your-secret-key-at-least-32-chars",
    "Issuer": "MyFavouriteApp",
    "Audience": "MyFavouriteAudience"
  },
  "ConnectionStrings": {
    "UserServiceDbContext": "Host=localhost;Port=5431;Database=UserDb;Username=UserService;Password=UserService",
    "Redis": "localhost:6379"
  },
  "RabbitMq": {
    "Host": "localhost",
    "Port": "5672",
    "VirtualHost": "/",
    "Username": "guest",
    "Password": "guest"
  },
  "NotificationSettings": {
    "Name": "UserService",
    "EmailAddress": "UserService@gmail.com",
    "EmailPassword": "UserServicePassword",
    "Host": "localhost",
    "Port": 1025
  }
}
```

В Docker все значения переопределяются через environment variables (`__` = вложенность в JSON), хосты заменяются на имена сервисов: `pg-db`, `redis`, `rabbitmq`, `mailpit`.

Создай `.env` в корне по образцу `.env.example`:

```env
POSTGRES_USER=UserService
POSTGRES_PASSWORD=UserService
POSTGRES_DB=UserDb
PGADMIN_DEFAULT_EMAIL=admin@localhost.com
PGADMIN_DEFAULT_PASSWORD=SuperSecret123
RABBITMQ_USER=guest
RABBITMQ_PASS=guest
RABBITMQ_VHOST=/
JWT_KEY=your-secret-key
JWT_ISSUER=MyFavouriteApp
JWT_AUDIENCE=MyFavouriteAudience
BUILD_TARGET=development
ASPNETCORE_ENVIRONMENT=Development
```

### Run with Docker Compose (recommended)

```bash
docker compose up --build
```

| Сервис | Адрес |
|---|---|
| **UserService API** | http://localhost:8080/swagger |
| **pgAdmin** | http://localhost:5050 |
| **RabbitMQ Management** | http://localhost:15672 (guest / guest) |
| **MailPit UI** | http://localhost:8025 |
| **PostgreSQL** | localhost:5431 |
| **Redis** | localhost:6379 |
| **RabbitMQ** | localhost:5672 |
| **MailPit SMTP** | localhost:1025 |

### Run Locally (without Docker)

```bash
dotnet run --project UserService.API/UserService.API.csproj
```

## API Endpoints

Базовый маршрут: `/api/auth`

| Method | Endpoint | Описание | Auth |
|---|---|---|---|
| POST | `/api/auth/verification` | Запрос кода подтверждения на email | No |
| POST | `/api/auth/refresh-code` | Повторная отправка кода | No |
| POST | `/api/auth/register` | Регистрация по коду из email | No |
| POST | `/api/auth/login` | Вход, получение JWT + Refresh Token | No |
| POST | `/api/auth/refresh-token` | Обновление токенов по Refresh Token | No |
| GET | `/api/auth/mainpage` | Защищенный эндпоинт, проверка JWT + JTI | Yes |

**Пример регистрации:**

```http
POST /api/auth/verification
Content-Type: application/json

{
  "name": "NewUser",
  "email": "user@example.com",
  "password": "StrongPass1!"
}
```

Код придет в MailPit (http://localhost:8025), затем:

```http
POST /api/auth/register
{
  "email": "user@example.com",
  "code": "JCAPQU"
}
```

## Testing

```bash
# Unit-тесты
dotnet test UserService.UnitTests/UserService.UnitTests.csproj

# Integration + E2E тесты (требуется Docker для Testcontainers)
dotnet test UserService.IntegrationTests/UserService.IntegrationTests.csproj
```

**Что покрыто:**

- **Unit:** `AuthService`, `UserVerificationService`, `JtiCheckService`, `PwnedPasswordService`, `EmailService<T>`, `MessageExchangerService`, `SmtpConnectionService`, `EmailConsumer`, валидаторы
- **Integration:** `AuthController` — 12 тестов на `WebApplicationFactory` с InMemory БД и моками
- **E2E:** полный сценарий `verification -> refresh-code -> register -> login -> refresh-token -> mainpage` с реальными PostgreSQL, Redis, RabbitMQ и MailPit через Testcontainers

## CI/CD

GitHub Actions (`.github/workflows/`):

- **CI (`ci.yml`)** — на каждый push/PR: restore, build, unit-тесты, integration-тесты, проверка сборки Dockerfile
- **CD (`cd.yml`)** — на push в `main` / tag `v*`: сборка `production`-образа и публикация в `ghcr.io/<owner>/userservice`

## Exception Mapping

| Exception | HTTP Status |
|---|---|
| `WrongPasswordException` | 400 Bad Request |
| `UserAlreadyExistException` | 409 Conflict |
| `UserNotFoundException` | 404 Not Found |
| `UnauthorizedException` | 401 Unauthorized |
| `ValidateRefreshTokenException` | 401 Unauthorized |
| `ServiceUnavailableException` | 503 Service Unavailable |
| `DbSaveChangesException` / `DbAccessDeniedException` | 500 Internal Server Error |

