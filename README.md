# FinHub — Backend

## Description
Backend of a thematic social network focused on finance. Handles user authentication, posts, comments, hubs, chat, and notifications.

## Features
- Secure registration and login (JWT, Google OAuth)
- User profile management
- Posts and nested comments
- Hubs creation and moderation
- Real-time chat (message grouping, read markers, contacts)
- Notification system for key events

## Tech Stack
- **Backend:** .NET Web API
- **Database:** PostgreSQL
- **Patterns:** Clean Architecture, CQRS + MediatR
- **File Storage:** Azure Storage
- **Containerization:** Docker

## Setup & Run
1. Clone the repository:
```bash
git clone https://github.com/yaryna-laiter/FinanceHub.git
```

2. Open the project in Visual Studio or VS Code.

3. Run the backend:
```bash
dotnet run
```
