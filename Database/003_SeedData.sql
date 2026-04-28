USE AppointmentDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Hash NVARCHAR(512) = 'dNenHFzqIK7wTHP3rNRkWw/tqSBIttAjKbks5Tgt5KVD9Rhdnnwqsbtos28hfQ3dpOGciFK1kHO1PAYqGmSETw==';
DECLARE @Salt NVARCHAR(512) = 'y21nmTHP1Vwrtv6X7V+mLm30Xrh74VS6yVJTPjX6qGQO1qmlAUqyDPEODItndn+hacqZNPjczFgVk7qVBK8oOn3/QUfZgz0tuMJ5Jde9nBzQik2ZW8nEgIctMjS8ypPqqliYaB/CA2FJNmBqoOx7vypsuOmR6C8EyzIOst+sXQw=';

-- Password for ALL users below: "password123"
INSERT INTO Users (Username, Email, PasswordHash, PasswordSalt, Role, FullName, Phone) VALUES
('admin',  'admin@appt.test',   @Hash, @Salt, 'Admin',    'Site Admin',     '+90 212 555 0000'),
('drsmith','smith@appt.test',   @Hash, @Salt, 'Provider', 'Dr. Sarah Smith','+90 212 555 0101'),
('drlee',  'lee@appt.test',     @Hash, @Salt, 'Provider', 'Dr. James Lee',  '+90 212 555 0102'),
('alice',  'alice@appt.test',   @Hash, @Salt, 'Customer', 'Alice Yılmaz',   '+90 532 555 0001'),
('bob',    'bob@appt.test',     @Hash, @Salt, 'Customer', 'Bob Demir',      '+90 532 555 0002');

-- Provider profiles
INSERT INTO ProviderProfiles (UserId, Bio, Specialty) VALUES
(2, 'Board-certified general practitioner with 15 years of experience.',         'General Medicine'),
(3, 'Dental surgeon specializing in cosmetic and restorative procedures.',       'Dentistry');

-- Services
INSERT INTO Services (Name, Description, DurationMinutes, Price) VALUES
('General Consultation',     'Standard 30-minute health check-up',     30, 75.00),
('Extended Consultation',    'In-depth 60-minute consultation',        60, 130.00),
('Dental Cleaning',          'Professional teeth cleaning + polish',   45, 95.00),
('Dental Check-up',          'Routine dental examination',             30, 60.00),
('Follow-up Visit',          'Quick 15-minute follow-up appointment',  15, 40.00);

-- Provider <-> Service offerings
INSERT INTO ProviderServices (ProviderId, ServiceId) VALUES
(2, 1), (2, 2), (2, 5),  -- Dr. Smith: general consult + extended + follow-up
(3, 3), (3, 4), (3, 5);  -- Dr. Lee:   dental cleaning + check-up + follow-up

-- Working hours (Mon-Fri 09:00-17:00 with lunch 12:00-13:00)
-- DayOfWeek: 0=Sun, 1=Mon ... 6=Sat
INSERT INTO WorkingHours (ProviderId, DayOfWeek, StartMinutes, EndMinutes) VALUES
-- Dr. Smith
(2, 1, 540, 720), (2, 1, 780, 1020),  -- Mon
(2, 2, 540, 720), (2, 2, 780, 1020),  -- Tue
(2, 3, 540, 720), (2, 3, 780, 1020),  -- Wed
(2, 4, 540, 720), (2, 4, 780, 1020),  -- Thu
(2, 5, 540, 720), (2, 5, 780, 1020),  -- Fri
-- Dr. Lee (Tue-Sat 10:00-18:00, no lunch break)
(3, 2, 600, 1080),
(3, 3, 600, 1080),
(3, 4, 600, 1080),
(3, 5, 600, 1080),
(3, 6, 600, 1080);

-- A few sample appointments (relative to today)
INSERT INTO Appointments (ProviderId, CustomerId, ServiceId, StartUtc, EndUtc, Status, Notes) VALUES
(2, 4, 1, DATEADD(hour, 10, CAST(CAST(DATEADD(day, 1, GETUTCDATE()) AS DATE) AS DATETIME2)),
        DATEADD(minute, 30, DATEADD(hour, 10, CAST(CAST(DATEADD(day, 1, GETUTCDATE()) AS DATE) AS DATETIME2))),
        'Confirmed', 'Annual check-up'),
(3, 5, 3, DATEADD(hour, 11, CAST(CAST(DATEADD(day, 2, GETUTCDATE()) AS DATE) AS DATETIME2)),
        DATEADD(minute, 45, DATEADD(hour, 11, CAST(CAST(DATEADD(day, 2, GETUTCDATE()) AS DATE) AS DATETIME2))),
        'Pending', NULL),
(2, 5, 5, DATEADD(hour, 14, CAST(CAST(DATEADD(day, -3, GETUTCDATE()) AS DATE) AS DATETIME2)),
        DATEADD(minute, 15, DATEADD(hour, 14, CAST(CAST(DATEADD(day, -3, GETUTCDATE()) AS DATE) AS DATETIME2))),
        'Completed', 'Follow-up went well');

-- Sample rating for the completed appointment
INSERT INTO Ratings (AppointmentId, CustomerId, ProviderId, Stars, Comment) VALUES
(3, 5, 2, 5, 'Very thorough and professional.');

-- A welcome notification
INSERT INTO Notifications (UserId, Title, Body) VALUES
(4, 'Welcome to Appointment System', 'Browse available providers and book your first appointment.'),
(5, 'Welcome to Appointment System', 'Browse available providers and book your first appointment.');
GO
