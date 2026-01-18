# מערכת ניהול משמרות

מערכת ווב לניהול חלוקת משמרות בין חיילים עם תמיכה באילוצים מורכבים ואלגוריתם שיבוץ אוטומטי.

## טכנולוגיות

- **Backend**: C# ASP.NET Core 8.0
- **Frontend**: HTML, CSS, JavaScript
- **Data Storage**: JSON Files
- **Architecture**: SOLID Principles

## מבנה הפרויקט

```
teken/
├── src/
│   └── ShiftScheduler.API/        # Backend API
│       ├── Controllers/           # API Controllers
│       ├── Services/              # Business Logic
│       ├── Repositories/          # Data Access Layer
│       ├── Interfaces/            # Contracts & Interfaces
│       └── Models/                # Data Models
├── frontend/                      # Frontend Application
│   ├── index.html
│   ├── styles.css
│   └── app.js
└── Data/                          # JSON Data Files (auto-generated)
```

## דרישות מערכת

- .NET 8.0 SDK או חדש יותר
- דפדפן מודרני (Chrome, Firefox, Edge)

## התקנה והפעלה

1. שכפל את הפרויקט:
```bash
git clone <repository-url>
cd teken
```

2. הפעל את השרת:
```bash
cd src/ShiftScheduler.API
dotnet run
```

3. פתח דפדפן וגש ל:
```
http://localhost:5000
```

## תכונות עיקריות

### ניהול עמדות
- הגדרת עמדות שונות (ש.ג, אחורי, כ"כ, משק תורן וכו')
- סימון עמדות שדורשות מפקד
- הגדרת עמדות כ"כ (כוננות) לעומת עמדות שמירה
- מחיקה מרובה של עמדות

### ניהול חיילים
- הוספת חיילים (תמיכה בהדבקת רשימה מרובה)
- סימון חיילים כמפקדים
- ניהול אילוצים לכל חייל:
  - ימים שלמים שהחייל לא נמצא
  - שעות אסורות לפי יום (0-23)
  - עמדות אסורות

### יצירת לוח זמנים
- הגדרת טווח תאריכים
- הגדרת שעות התחלה וסיום (אופציונלי)
- אלגוריתם שיבוץ אוטומטי עם:
  - מרווח מקסימלי בין משמרות שמירה (8 שעות מינימום)
  - עדיפות לחיילים שיורדים משמירה לכוננות (כל 4 חיילים)
  - כיבוד כל האילוצים המוגדרים
  - הבטחת מילוי כל המשבצות

### צפייה ועריכה
- תצוגת לוח זמנים בטבלה מפורטת
- סטטיסטיקות לכל חייל:
  - סה"כ משמרות
  - סה"כ שמירות
  - רווח ממוצע בין משמרות
  - רווח מקסימלי בין משמרות
- עריכת שיבוצים:
  - החלפת חייל בשעה ספציפית
  - החלפת חייל בכל המשמרות שלו
- הדגשת כל ההופעות של חייל בלחיצה

### שמירה ושיתוף
- שמירת לוחות זמנים שנוצרו
- טעינת לוחות זמנים שמורים
- שיתוף לוחות זמנים עם קוד שיתוף
- טעינת לוחות זמנים משותפים

## ארכיטקטורה

הפרויקט בנוי לפי עקרונות SOLID:

- **Single Responsibility**: כל מחלקה אחראית על תפקיד אחד
- **Open/Closed**: פתוח להרחבה, סגור לשינוי
- **Liskov Substitution**: ממשקים ניתנים להחלפה
- **Interface Segregation**: ממשקים ממוקדים
- **Dependency Inversion**: תלות בממשקים, לא במימוש

### שכבות

- **Controllers**: נקודות קצה של ה-API
- **Services**: לוגיקה עסקית (שיבוץ, הגדרות)
- **Repositories**: גישה לנתונים (JSON)
- **Interfaces**: חוזים וממשקים
- **Models**: מודלי נתונים

## API Endpoints

### עמדות
- `GET /api/positions` - קבלת כל העמדות
- `POST /api/positions` - יצירת עמדה חדשה
- `PUT /api/positions/{id}` - עדכון עמדה
- `DELETE /api/positions/{id}` - מחיקת עמדה
- `DELETE /api/positions/bulk` - מחיקה מרובה

### חיילים
- `GET /api/soldiers` - קבלת כל החיילים
- `POST /api/soldiers` - יצירת חייל חדש
- `PUT /api/soldiers/{id}` - עדכון חייל
- `DELETE /api/soldiers/{id}` - מחיקת חייל

### לוח זמנים
- `POST /api/schedule/generate` - יצירת לוח זמנים
- `GET /api/schedule` - קבלת הלוח הנוכחי
- `POST /api/schedule/replace` - החלפת שיבוץ
- `POST /api/schedule/swap` - החלפת כל השיבוצים של שני חיילים

### הגדרות
- `GET /api/settings` - קבלת הגדרות
- `PUT /api/settings` - עדכון הגדרות

### לוחות זמנים שמורים
- `GET /api/savedSchedules` - קבלת כל הלוחות השמורים
- `GET /api/savedSchedules/{id}` - קבלת לוח לפי ID
- `GET /api/savedSchedules/share/{code}` - קבלת לוח לפי קוד שיתוף
- `GET /api/savedSchedules/shared` - קבלת כל הלוחות המשותפים
- `POST /api/savedSchedules` - שמירת לוח חדש
- `PUT /api/savedSchedules/{id}` - עדכון לוח
- `PUT /api/savedSchedules/{id}/share` - עדכון סטטוס שיתוף
- `DELETE /api/savedSchedules/{id}` - מחיקת לוח

## רישיון

פרויקט זה הוא קוד פתוח וזמין תחת רישיון MIT.
