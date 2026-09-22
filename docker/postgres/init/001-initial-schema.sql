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

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES
    ('20260922103938_InitialCreate', '10.0.12'),
    ('20260922134210_AddIsEnabledToHotdesk', '10.0.12');
