# הוראות הפעלה

## דרישות
- .NET 8.0 SDK או חדש יותר
- ניתן להוריד מ: https://dotnet.microsoft.com/download

## הפעלה

### שיטה 1: Visual Studio
1. פתח את `ShiftScheduler.sln` ב-Visual Studio
2. לחץ F5 או "Start Debugging"
3. הדפדפן ייפתח אוטומטית

### שיטה 2: שורת פקודה (Command Line)

פתח PowerShell או Command Prompt בתיקיית הפרויקט והרץ:

```bash
cd backend/ShiftScheduler.API
dotnet restore
dotnet run
```

### שיטה 3: Visual Studio Code
1. פתח את התיקייה ב-VS Code
2. לחץ F5 או פתח את Terminal והרץ:
```bash
cd backend/ShiftScheduler.API
dotnet run
```

## גישה לאפליקציה

לאחר ההפעלה, פתח דפדפן וגש ל:
```
http://localhost:5000
```

## פתרון בעיות

אם יש שגיאות:
1. ודא ש-.NET 8.0 SDK מותקן: `dotnet --version`
2. הרץ `dotnet restore` בתיקיית הפרויקט
3. ודא שכל הקבצים קיימים בתיקייה `backend/ShiftScheduler.API/`
4. ודא שתיקיית `frontend/` קיימת עם הקבצים: index.html, styles.css, app.js
