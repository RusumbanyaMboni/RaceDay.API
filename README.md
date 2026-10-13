# RaceDay API

## Overview

RaceDay API is a RESTful Web API developed for the RaceDay event management system.

The API supports two main user roles:

- **Organiser** – creates and manages race events, categories and race results.
- **Participant** – views events, selects categories and enrols in events, and views their own results.

The system is designed around the RaceDay functional requirements and uses authenticated user information to protect personal data and event ownership.

## Main Features

- User registration and login
- JWT-based authentication
- Role-based authorization
- User profile management
- Race event management
- Event category management
- Participant event enrolment
- Race result management
- Ownership checks for organiser operations
- Validation of input data
- Duplicate enrolment prevention
- Entity Framework Core database access
- Swagger/OpenAPI API documentation
- GitHub Actions continuous integration

## System Roles

| Role | Main Permissions |
|---|---|
| Organiser | Create, update and delete owned events |
| Organiser | Create, update and delete categories for owned events |
| Organiser | View enrolments for owned events |
| Organiser | Record, update and delete results for owned events |
| Participant | View events and categories |
| Participant | Enrol in an event |
| Participant | View their own enrolments |
| Participant | View their own results |
| Both | View and update their own profile |

## API Endpoints

### Authentication

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Register a new user |
| POST | `/api/auth/login` | Public | Authenticate a user and return a JWT |
| GET | `/api/auth/me` | Authenticated | Return the currently authenticated user |

### User Profile

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/profile` | Organiser / Participant | View the logged-in user's profile |
| PUT | `/api/profile` | Organiser / Participant | Update the logged-in user's profile |

### Events

| GET | `/api/Event` | Organiser / Participant | View available events |
| GET | `/api/Event/{id}` | Organiser / Participant | View one event |
| POST | `/api/Event` | Organiser | Create an event |
| PUT | `/api/Event/{id}` | Organiser | Update an owned event |
| DELETE | `/api/Event/{id}` | Organiser | Delete an owned event |
| GET | `/api/Event/my-events` | Organiser | View events created by the logged-in organiser |

Event types are restricted to:

- `run`
- `walk`
- `cycle`

### Categories

| Method | Endpoint | Access | Description |
|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | Organiser / Participant | View categories for an event |
| GET | `/api/categories/{id}` | Organiser / Participant | View a category |
| POST | `/api/events/{eventId}/categories` | Organiser | Create a category for an owned event |
| PUT | `/api/categories/{id}` | Organiser | Update a category belonging to an owned event |
| DELETE | `/api/categories/{id}` | Organiser | Delete a category belonging to an owned event |

### Enrolments

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/events/{eventId}/enrolments` | Participant | Enrol in an event using a selected category |
| GET | `/api/enrolments/my-enrolments` | Participant | View the participant's own enrolments |
| GET | `/api/events/{eventId}/enrolments` | Organiser | View enrolments for an owned event |
| DELETE | `/api/enrolments/{id}` | Participant / Organiser | Cancel or remove an authorised enrolment |

When an enrolment is created, the authenticated Participant ID and event ID are determined by the server. The selected category is supplied by the request.

### Results

| Method | Endpoint | Access | Description |
|---|---|---|---|
| POST | `/api/enrolments/{enrolmentId}/results` | Organiser | Record a participant's result |
| GET | `/api/results/my-results` | Participant | View the participant's own results |
| PUT | `/api/results/{id}` | Organiser | Update an authorised result |
| DELETE | `/api/results/{id}` | Organiser | Delete an authorised result |

## Authentication and Security

RaceDay uses JWT authentication.

The authentication flow is:

1. A user registers an account.
2. The user logs in using their email and password.
3. The API verifies the password.
4. The API generates a JWT containing the authenticated user's identity and role information.
5. The client sends the token using the `Authorization: Bearer <token>` header.
6. Protected endpoints use authorization rules to determine whether the user may perform the requested operation.

Passwords are hashed before being stored in the database and are not returned as part of normal API responses.

Ownership checks are applied to organiser operations. An organiser can only modify resources belonging to events that they own.

Participants are restricted to their own enrolments and results.

## Data Model

The RaceDay system is based on the following main entities:

- **Role**
- **User**
- **Event**
- **Category**
- **Enrolment**
- **Result**

The Enrolment entity acts as the link between Participants, Events and Categories.

The main relationships are:

```text
Role
  |
  | 1 : Many
  v
User
  |
  | 1 : Many
  +--------------------> Event
  |
  | 1 : Many
  v
Enrolment <------------- Category
  |
  | 1 : 0..1
  v
Result
```

## Validation and Business Rules

The API contains validation and business rules including:

- Required user registration fields are validated.
- Email addresses are validated.
- Passwords must meet the configured minimum length.
- Event type must be `run`, `walk` or `cycle`.
- Event ownership is checked before organiser modifications.
- Category ownership is checked through the related event.
- Category distance must be greater than zero.
- Minimum age cannot be greater than maximum age.
- A participant cannot enrol in the same event more than once.
- A category selected during enrolment must belong to the selected event.
- Finish position must be greater than zero.
- Finish time must be greater than zero.
- A result can only be managed by an organiser authorised for the related event.
- Server-generated values such as enrolment dates and recorded dates are not trusted from the client.

## Technology Stack

- ASP.NET Core Web API
- C#
- Entity Framework Core
- Relational database
- JWT Bearer Authentication
- BCrypt password hashing
- Swagger / OpenAPI
- Git and GitHub
- GitHub Actions

## Project Structure

```text
RaceDay.API/
│
├── Controllers/
│   ├── AuthController.cs
│   ├── ProfileController.cs
│   ├── EventController.cs
│   ├── CategoryController.cs
│   ├── EnrolmentController.cs
│   └── ResultController.cs
│
├── Data/
│   └── RaceDayDbContext.cs
│
├── Models/
│   ├── Role.cs
│   ├── User.cs
│   ├── Event.cs
│   ├── Category.cs
│   ├── Enrolment.cs
│   └── Result.cs
│
├── Migrations/
│
├── Program.cs
├── appsettings.json
└── RaceDay.API.csproj
```

## Getting Started

### Prerequisites

Install:

- Visual Studio or another compatible .NET development environment
- The .NET SDK version targeted by the project
- SQL Server or the configured relational database
- Git

### 1. Clone the Repository

```bash
git clone <YOUR-GITHUB-REPOSITORY-URL>
cd RaceDay.API
```

### 2. Configure the Database

Update the application's database connection configuration with the connection details for your local database.

Do not commit passwords, JWT secrets or other sensitive credentials to GitHub.

### 3. Configure JWT

The API requires a JWT signing key.

The configuration supports:

```text
Jwt:Key
Jwt:Issuer
Jwt:Audience
```

Use a strong private key for the JWT signing secret.

For local development, sensitive values should preferably be stored using user secrets or environment variables rather than committed to the repository.

### 4. Restore Dependencies

```bash
dotnet restore
```

### 5. Apply Database Migrations

If the project contains pending Entity Framework Core migrations:

```bash
dotnet ef database update
```

### 6. Build the Project

```bash
dotnet build
```

### 7. Run the API

```bash
dotnet run
```

The application will display the local address in the terminal.

## Swagger / OpenAPI

When Swagger is enabled, open the Swagger interface using:

```text
/swagger
```

Swagger can be used to:

1. Register an Organiser.
2. Register a Participant.
3. Log in.
4. Copy the returned JWT token.
5. Select **Authorize**.
6. Enter the bearer token.
7. Test protected endpoints according to the user's role.

## Example Authentication Header

Protected requests use:

```http
Authorization: Bearer YOUR_JWT_TOKEN
```

Replace `YOUR_JWT_TOKEN` with the token returned by the login endpoint.

## Typical API Workflow

### Organiser

```text
Register
   ↓
Login
   ↓
Receive JWT
   ↓
Authorize Swagger
   ↓
Create Event
   ↓
Create Categories
   ↓
View Event Enrolments
   ↓
Record Results
   ↓
Update/Delete authorised resources
```

### Participant

```text
Register
   ↓
Login
   ↓
Receive JWT
   ↓
Authorize Swagger
   ↓
View Events
   ↓
View Categories
   ↓
Enrol in Event
   ↓
View Own Enrolments
   ↓
View Own Results
```

## HTTP Response Codes

| Status | Meaning |
|---|---|
| `200 OK` | Request completed successfully |
| `201 Created` | A new resource was created |
| `204 No Content` | Resource was successfully deleted |
| `400 Bad Request` | Invalid request or validation failure |
| `401 Unauthorized` | Authentication is missing or invalid |
| `403 Forbidden` | Authenticated user does not have permission |
| `404 Not Found` | Requested resource does not exist |
| `409 Conflict` | Request conflicts with existing data |

## Testing

Testing should verify both normal functionality and security rules.

Important test areas include:

- Successful registration
- Successful login
- Invalid login credentials
- Access to protected endpoints without authentication
- Organiser-only operations
- Participant-only operations
- Event ownership checks
- Category ownership checks
- Duplicate enrolment prevention
- Invalid event type
- Invalid category data
- Invalid result data
- Participant access limited to their own data
- Result access limited to authorised organisers and the participant who owns the result

## Continuous Integration

The repository includes a GitHub Actions workflow for automated project verification.

The workflow is intended to ensure that changes can be restored, built and tested consistently before submission.

## Academic Project

This project was developed as part of the **PROG 6212 Programming** coursework and implements the RaceDay functional requirements using a RESTful API architecture.

## Development Notes

The API follows a server-side trust model for important identity and ownership information.

For example:

- The authenticated user's ID is taken from the JWT rather than trusted from the request.
- The event ID is taken from the route where appropriate.
- Server-controlled dates are generated by the API.
- Organiser ownership is verified before modifying event-related resources.

This helps prevent users from modifying data belonging to other users.

### Github Action Workflow Screenshot

<img width="1486" height="882" alt="a1" src="https://github.com/user-attachments/assets/638af4e2-d939-47b3-9918-de094aef7918" />

<img width="1517" height="860" alt="a2" src="https://github.com/user-attachments/assets/cbc74577-9f51-4f6f-bd6a-d07aceeb4f15" />


### Successfull Green Build in the README Screenshot

<img width="1917" height="902" alt="b1" src="https://github.com/user-attachments/assets/3a927feb-e256-4787-989c-613c1d71b39e" />

<img width="1367" height="312" alt="b2" src="https://github.com/user-attachments/assets/8e1b1cae-381a-427b-a576-1d8f5dcc7ace" />


### YOUTUBE


## License

This project is an academic coursework project and is not intended for commercial redistribution.
