USE master;
GO
IF EXISTS (SELECT name FROM sys.databases WHERE name = 'AppointmentDb')
BEGIN
    ALTER DATABASE AppointmentDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE AppointmentDb;
END
CREATE DATABASE AppointmentDb;
GO
USE AppointmentDb;
GO
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

CREATE TABLE Users (
    Id           INT           IDENTITY(1,1) PRIMARY KEY,
    Username     NVARCHAR(50)  NOT NULL,
    Email        NVARCHAR(150) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    PasswordSalt NVARCHAR(512) NOT NULL,
    Role         NVARCHAR(20)  NOT NULL DEFAULT 'Customer',
    FullName     NVARCHAR(150),
    Phone        NVARCHAR(50),
    IsActive     BIT           NOT NULL DEFAULT 1,
    CreatedAt    DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Users_Username UNIQUE (Username),
    CONSTRAINT UQ_Users_Email    UNIQUE (Email),
    CONSTRAINT CK_Users_Role     CHECK (Role IN ('Admin','Provider','Customer'))
);

CREATE TABLE ProviderProfiles (
    UserId    INT PRIMARY KEY,
    Bio       NVARCHAR(2000),
    Specialty NVARCHAR(150),
    CONSTRAINT FK_ProviderProfiles_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE
);

CREATE TABLE Services (
    Id              INT           IDENTITY(1,1) PRIMARY KEY,
    Name            NVARCHAR(150) NOT NULL,
    Description     NVARCHAR(1000),
    DurationMinutes INT           NOT NULL,
    Price           DECIMAL(18,2) NOT NULL DEFAULT 0,
    IsActive        BIT           NOT NULL DEFAULT 1,
    CONSTRAINT CK_Services_Duration CHECK (DurationMinutes > 0 AND DurationMinutes <= 480)
);

CREATE TABLE ProviderServices (
    ProviderId INT NOT NULL,
    ServiceId  INT NOT NULL,
    CONSTRAINT PK_ProviderServices PRIMARY KEY (ProviderId, ServiceId),
    CONSTRAINT FK_PS_Provider FOREIGN KEY (ProviderId) REFERENCES Users(Id)    ON DELETE CASCADE,
    CONSTRAINT FK_PS_Service  FOREIGN KEY (ServiceId)  REFERENCES Services(Id) ON DELETE CASCADE
);

-- Recurring weekly working hours
CREATE TABLE WorkingHours (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    ProviderId INT NOT NULL,
    DayOfWeek  TINYINT NOT NULL,  -- 0=Sunday .. 6=Saturday
    StartMinutes SMALLINT NOT NULL, -- minutes from midnight (e.g. 540 = 09:00)
    EndMinutes   SMALLINT NOT NULL,
    CONSTRAINT FK_WH_Provider FOREIGN KEY (ProviderId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT CK_WH_Day      CHECK (DayOfWeek BETWEEN 0 AND 6),
    CONSTRAINT CK_WH_Range    CHECK (StartMinutes < EndMinutes AND StartMinutes >= 0 AND EndMinutes <= 1440)
);
CREATE INDEX IX_WH_Provider_Day ON WorkingHours(ProviderId, DayOfWeek);

-- Specific date-range time off (overrides working hours)
CREATE TABLE TimeOff (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    ProviderId  INT NOT NULL,
    StartUtc    DATETIME2 NOT NULL,
    EndUtc      DATETIME2 NOT NULL,
    Reason      NVARCHAR(500),
    CONSTRAINT FK_TO_Provider FOREIGN KEY (ProviderId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT CK_TO_Range    CHECK (StartUtc < EndUtc)
);
CREATE INDEX IX_TO_Provider_Range ON TimeOff(ProviderId, StartUtc, EndUtc);

CREATE TABLE Appointments (
    Id          INT          IDENTITY(1,1) PRIMARY KEY,
    ProviderId  INT          NOT NULL,
    CustomerId  INT          NOT NULL,
    ServiceId   INT          NOT NULL,
    StartUtc    DATETIME2    NOT NULL,
    EndUtc      DATETIME2    NOT NULL,
    Status      NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    Notes       NVARCHAR(2000),
    CreatedAt   DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt   DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Appt_Provider FOREIGN KEY (ProviderId) REFERENCES Users(Id),
    CONSTRAINT FK_Appt_Customer FOREIGN KEY (CustomerId) REFERENCES Users(Id),
    CONSTRAINT FK_Appt_Service  FOREIGN KEY (ServiceId)  REFERENCES Services(Id),
    CONSTRAINT CK_Appt_Status   CHECK (Status IN ('Pending','Confirmed','Completed','Cancelled')),
    CONSTRAINT CK_Appt_Range    CHECK (StartUtc < EndUtc)
);
-- Critical indexes for fast slot-conflict lookups and calendar queries
CREATE INDEX IX_Appt_Provider_Range ON Appointments(ProviderId, StartUtc, EndUtc) INCLUDE (Status);
CREATE INDEX IX_Appt_Customer_Start ON Appointments(CustomerId, StartUtc DESC);
-- Unique covering index helps the booking transaction; partial idx active appts only
CREATE UNIQUE INDEX UX_Appt_Provider_Start_Active
    ON Appointments(ProviderId, StartUtc)
    WHERE Status IN ('Pending','Confirmed');

CREATE TABLE Notifications (
    Id                  INT IDENTITY(1,1) PRIMARY KEY,
    UserId              INT          NOT NULL,
    Title               NVARCHAR(200) NOT NULL,
    Body                NVARCHAR(2000) NOT NULL,
    RelatedAppointmentId INT,
    IsRead              BIT          NOT NULL DEFAULT 0,
    CreatedAt           DATETIME2    NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT FK_Notif_User FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Notif_Appt FOREIGN KEY (RelatedAppointmentId) REFERENCES Appointments(Id) ON DELETE SET NULL
);
CREATE INDEX IX_Notif_User_Created ON Notifications(UserId, CreatedAt DESC);

CREATE TABLE Ratings (
    Id            INT IDENTITY(1,1) PRIMARY KEY,
    AppointmentId INT NOT NULL,
    CustomerId    INT NOT NULL,
    ProviderId    INT NOT NULL,
    Stars         TINYINT NOT NULL,
    Comment       NVARCHAR(1000),
    CreatedAt     DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    CONSTRAINT UQ_Ratings_Appointment UNIQUE (AppointmentId),
    CONSTRAINT CK_Ratings_Stars CHECK (Stars BETWEEN 1 AND 5),
    CONSTRAINT FK_Rating_Appt     FOREIGN KEY (AppointmentId) REFERENCES Appointments(Id) ON DELETE CASCADE,
    CONSTRAINT FK_Rating_Customer FOREIGN KEY (CustomerId)    REFERENCES Users(Id),
    CONSTRAINT FK_Rating_Provider FOREIGN KEY (ProviderId)    REFERENCES Users(Id)
);
CREATE INDEX IX_Ratings_Provider ON Ratings(ProviderId);
GO
