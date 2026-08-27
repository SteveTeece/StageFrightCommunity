---
name: ef-migration
description: Add, update, or remove an EF Core migration for StageFrightDbContext with the correct --project/--startup-project flags. User-invoked only, since it mutates migration history and the local database.
disable-model-invocation: true
---

# ef-migration

Wraps the `dotnet ef` commands from CLAUDE.md so the `--project`/`--startup-project` flags (easy
to get wrong or omit) are always correct. `StageFright.Data` owns the DbContext and migrations;
`StageFright.App` (the MAUI host) is the startup project EF Core needs to resolve the runtime
configuration.

## Usage

`/ef-migration add <MigrationName>` — create a new migration after changing an entity or
`OnModelCreating` configuration.

`/ef-migration update` — apply pending migrations to the local SQLite database at
`FileSystem.AppDataDirectory/stagefright.db` (the MAUI app-data directory).

`/ef-migration remove` — remove the most recent, not-yet-applied migration (e.g. to fix a mistake
before re-adding it).

## Steps

1. Confirm the entity/configuration change this migration is for has already been made in
   `StageFright.Core/Entities/` and/or `StageFright.Data/Configurations/` — a migration should
   describe a real model change, not be generated speculatively.

2. Run the corresponding command from the repo root:

   ```bash
   dotnet ef migrations add <MigrationName> --project src/StageFright.Data/ --startup-project src/StageFright.App/
   dotnet ef database update             --project src/StageFright.Data/ --startup-project src/StageFright.App/
   dotnet ef migrations remove           --project src/StageFright.Data/ --startup-project src/StageFright.App/
   ```

3. After `add`, open the generated migration under `src/StageFright.Data/Migrations/` and sanity
   check the `Up`/`Down` methods match the intended schema change — EF's scaffolding is usually
   right but double-check for unintended column drops or renames it can misdetect as a
   drop-and-recreate.

4. Remember the soft-delete and financial-immutability rules from CLAUDE.md: new entity columns
   should include `IsDeleted`/`DeletedAt`/`DeletedBy` unless the entity is `Fee`, `Payment`, or
   `Transaction` (which are explicitly exempt and must never gain delete columns).

5. Run `dotnet build` and `dotnet test tests/StageFright.Data.Tests/` to confirm the migration
   applies cleanly against the SQLite in-memory test fixtures.
