# Guard Duty Scheduler

Web application for managing guard duty assignments among soldiers, with support for complex constraints and an automatic scheduling algorithm.

## Tech Stack

- **Backend**: C# ASP.NET Core 8.0
- **Frontend**: HTML, CSS, JavaScript
- **Data Storage**: JSON files
- **Architecture**: SOLID principles

## Project Structure

```
teken/
├── backend/                       # Backend API
│   ├── Controllers/               # API controllers
│   ├── Services/                  # Business logic (scheduling, settings)
│   ├── Repositories/             # Data access (JSON)
│   ├── Interfaces/               # Contracts and interfaces
│   └── Models/                   # Data models
├── frontend/                     # Frontend application
│   ├── index.html
│   ├── styles.css
│   └── app.js
└── Data/                         # JSON data files (auto-generated at runtime)
```

## Requirements

- .NET 8.0 SDK or newer
- Modern browser (Chrome, Firefox, Edge)

## Installation & Run

1. Clone the repository:
```bash
git clone <repository-url>
cd teken
```

2. Run the server:
```bash
cd backend
dotnet run
```

3. Open a browser and go to:
```
http://localhost:5000
```

## Features

### Positions
- Define positions (e.g. Sh.G., Achori, standby, etc.)
- Mark positions that require a commander
- Distinguish standby (continuous) vs guard (requires gap between shifts) positions

### Soldiers
- Add soldiers (supports pasting a list)
- Mark soldiers as commanders
- Per-soldier constraints:
  - Forbidden days of the week
  - Forbidden hours by day (0–23)
  - Forbidden positions

### Schedule Generation
- Set date range
- Optional start/end hours
- Automatic scheduling with:
  - Maximum gap between guard shifts
  - Preference for soldiers coming off guard into standby
  - All constraints respected
  - All slots filled

### View & Edit
- Schedule view in a detailed table
- Per-soldier stats: total shifts, guard count, night guards, average/min/max gap
- Edit assignments: replace a soldier in a slot or swap all assignments between two soldiers
- Highlight all occurrences of a soldier on click

## Architecture

The project follows SOLID principles:

- **Single Responsibility**: Each class has one responsibility
- **Open/Closed**: Open for extension, closed for modification
- **Liskov Substitution**: Interfaces are substitutable
- **Interface Segregation**: Focused interfaces
- **Dependency Inversion**: Depend on interfaces, not implementations

### Layers

- **Controllers**: API endpoints
- **Services**: Business logic (scheduling, settings)
- **Repositories**: Data access (JSON)
- **Interfaces**: Contracts and interfaces
- **Models**: Data models

## API Endpoints

### Positions
- `GET /api/positions` – List all positions
- `POST /api/positions` – Create position
- `PUT /api/positions/{id}` – Update position
- `DELETE /api/positions/{id}` – Delete position
- `DELETE /api/positions/bulk` – Bulk delete

### Soldiers
- `GET /api/soldiers` – List all soldiers
- `POST /api/soldiers` – Create soldier
- `PUT /api/soldiers/{id}` – Update soldier
- `DELETE /api/soldiers/{id}` – Delete soldier

### Schedule
- `POST /api/schedule/generate` – Generate schedule
- `GET /api/schedule` – Get current schedule
- `POST /api/schedule/replace` – Replace assignment
- `POST /api/schedule/swap` – Swap all assignments of two soldiers

### Settings
- `GET /api/settings` – Get settings
- `PUT /api/settings` – Update settings

### Excel
- `POST /api/ScheduleExcel/upload` – Upload Excel file for statistics
- `GET /api/ScheduleExcel/template` – Download empty Excel template
