USE AppointmentDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Provider utilization in a date range: hours booked vs hours available
CREATE OR ALTER PROCEDURE sp_GetProviderUtilization
    @FromUtc DATETIME2,
    @ToUtc   DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH BookedHours AS (
        SELECT a.ProviderId,
               SUM(DATEDIFF(MINUTE, a.StartUtc, a.EndUtc)) AS MinutesBooked,
               COUNT(*) AS AppointmentCount
        FROM Appointments a
        WHERE a.Status IN ('Confirmed','Completed')
          AND a.StartUtc >= @FromUtc AND a.StartUtc < @ToUtc
        GROUP BY a.ProviderId
    )
    SELECT u.Id, u.Username, u.FullName,
           ISNULL(b.MinutesBooked, 0) AS MinutesBooked,
           ISNULL(b.AppointmentCount, 0) AS AppointmentCount,
           ISNULL((SELECT AVG(CAST(Stars AS FLOAT)) FROM Ratings WHERE ProviderId = u.Id), 0) AS AverageRating,
           ISNULL((SELECT COUNT(*) FROM Ratings WHERE ProviderId = u.Id), 0) AS RatingCount
    FROM Users u
    LEFT JOIN BookedHours b ON b.ProviderId = u.Id
    WHERE u.Role = 'Provider' AND u.IsActive = 1
    ORDER BY MinutesBooked DESC;
END
GO

-- Daily appointment counts for the admin dashboard
CREATE OR ALTER PROCEDURE sp_GetDailyAppointmentCounts
    @FromUtc DATETIME2,
    @ToUtc   DATETIME2
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CAST(StartUtc AS DATE) AS Day,
           COUNT(*) AS Total,
           SUM(CASE WHEN Status = 'Pending'   THEN 1 ELSE 0 END) AS Pending,
           SUM(CASE WHEN Status = 'Confirmed' THEN 1 ELSE 0 END) AS Confirmed,
           SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS Completed,
           SUM(CASE WHEN Status = 'Cancelled' THEN 1 ELSE 0 END) AS Cancelled
    FROM Appointments
    WHERE StartUtc >= @FromUtc AND StartUtc < @ToUtc
    GROUP BY CAST(StartUtc AS DATE)
    ORDER BY Day;
END
GO
