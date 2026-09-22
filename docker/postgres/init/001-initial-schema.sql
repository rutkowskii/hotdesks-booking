CREATE TABLE "Hotdesks" (
    "Id" uuid NOT NULL,
    "Name" character varying(64) NOT NULL,
    "IsAvailable247" boolean NOT NULL,
    "IsEnabled" boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT "PK_Hotdesks" PRIMARY KEY ("Id")
);

CREATE TABLE "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

CREATE TABLE "Users" (
    "Id" uuid NOT NULL,
    "Name" text NOT NULL,
    CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
);

CREATE TABLE "Reservations" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "HotdeskId" uuid NOT NULL,
    "From" timestamp with time zone NOT NULL,
    "To" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_Reservations" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Reservations_Hotdesks_HotdeskId"
        FOREIGN KEY ("HotdeskId") REFERENCES "Hotdesks" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_Reservations_Users_UserId"
        FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_Reservations_HotdeskId" ON "Reservations" ("HotdeskId");
CREATE INDEX "IX_Reservations_UserId" ON "Reservations" ("UserId");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
    ('20260922103938_InitialCreate', '10.0.12'),
    ('20260922134210_AddIsEnabledToHotdesk', '10.0.12'),
    ('20260922143303_AddUsersAndReservations', '10.0.12');
