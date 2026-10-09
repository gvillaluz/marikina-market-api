# Database backups

The API uses the existing Controller → Service → Repository layers, PostgreSQL
`pg_dump`, built-in AES-256-GCM, and the existing B2 S3-compatible storage service.
Only one API instance may run the automatic scheduler. Files are never served by
public/presigned URLs. The private B2 bucket is supplied by the existing configuration.

## Required setup

1. Generate/review an EF migration for the two entities below and apply it yourself
   before using these endpoints. No migration or database operation was performed
   during implementation. Existing migration files and the snapshot are unchanged.
2. Keep `BackBlazeB2:Endpoint`, `BackBlazeB2:Buckets:Backups`, and the existing
   `BackBlazeB2:Backups:KeyId` / `ApplicationKey` structure. The bucket must be private.
   Its application key needs upload, download, file-version listing, and deletion
   permissions. Do not put credentials into source-controlled settings.
3. Set `Backups:EncryptionKey` in .NET User Secrets (development), or the deployment's
   protected configuration (production), to Base64 encoding of **32 random bytes**.
   Generate it locally without printing or committing it. Keep a separate secure
   copy: encrypted backups cannot be restored without the original key. This version
   supports one key; replacing it does not re-encrypt older backups.
4. Optional configuration: `Backups:PgDumpPath` (default `pg_dump` on PATH) and
   `Backups:TimeoutMinutes` (default 15; range 1–120). Use a pg_dump version compatible
   with the PostgreSQL server. The local executable found during inspection was
   `D:\PostgreSQL\Data\bin\pg_dump.exe`; the API does not hardcode this path.
5. The API account needs temporary disk space and permission to execute pg_dump.
   The 256 MiB limit applies to the unencrypted custom-format dump; whole-file GCM
   can require over 512 MiB of available memory. Temporary files are outside webroot,
   under the account's temp directory in `MarikinaMarket-backups`.

The hosted service retries unavailable schema checks every 30 seconds, logging only
exception types. It does not create tables or apply migrations at startup.

## Schema to generate and review

Follow the project's snake_case convention. All timestamps use PostgreSQL
`timestamp with time zone` and UTC values.

**backups**: `id` integer identity primary key; `date_time` required timestamp;
`type` varchar(16) (Automatic/Manual); `size` bigint (encrypted file bytes);
`status` varchar(16) (Completed/Failed); `b2_key` required varchar(250);
nullable `completed_at`, `expires_at`, `deleted_at` timestamps; nullable
`error_message` varchar(500); `is_finalized` and `storage_cleanup_pending` booleans.
Indexes: `date_time`; `(is_finalized, status, expires_at)`; unique `b2_key`.
No encryption keys or B2 credentials are stored in this table.

**backup_schedules**: singleton `id` integer primary key, no identity (must equal 1);
`enabled` boolean; `frequency` varchar(16); nullable `day_of_week` varchar(16);
`time` time without time zone; `retention_days` integer; `updated_at` required timestamp;
nullable `last_attempt_scheduled_at` and `next_retry_at` timestamps. Check constraints enforce singleton
ID, retention 1–365, and Daily/no weekday or Weekly/defined weekday. The timezone
is fixed in the service to Asia/Manila, rather than a client-controlled column.

The service creates the singleton row on first use: enabled, Daily, 00:00, 30 days.
Its creation timestamp is the effective start; it does not replay schedules from
before setup. Schedule edits take effect for future occurrences. Retention-only
edits preserve pending occurrences and update expiration for existing undeleted
completed backups. Increasing retention cannot recover files already deleted.

Before work, the service commits an unfinished attempt together with the automatic
scheduled timestamp in one transaction. Unfinished rows are excluded from history.
On restart they become Failed and their possible B2 objects become cleanup candidates.
An upload/persistence failure triggers compensating deletion of all versions of that
exact object. Failed cleanup retries through hourly background maintenance. If the
database itself is unavailable, an attempt cannot be durably recorded; unfinished
persisted attempts are recovered on restart. Backups contain the database snapshot
at dump time, before their own completion history update.

## Encrypted file format and restoration

`.mmbak` is a binary envelope, not a SQL script or a pg_restore input:

| Byte range | Meaning |
|---|---|
| 0–3 | ASCII `MMBK` |
| 4 | Format version, currently 1 |
| 5–16 | Fresh random 12-byte GCM nonce |
| 17–32 | 16-byte GCM authentication tag |
| 33 onward | AES-256-GCM ciphertext |

The first five bytes are also supplied as GCM associated data. To restore, a trusted
offline tool must validate magic/version/length and decrypt using the original
32-byte key, nonce, tag, and header. Any authentication failure must reject the
file without restoring it. Write decrypted output only after authentication
succeeds, then use `pg_restore` against a disposable PostgreSQL database. Remove
the decrypted dump afterward. No restore endpoint or frontend encryption settings
are included in this feature.

## Scheduling and health display

The scheduler reads current database settings every 30 seconds. Times are Manila
local times converted to UTC for comparisons. Daily and Weekly schedules catch up
once for the latest missed occurrence under the active settings, without replaying
every missed day. Failed automatic occurrences retry five minutes after failure,
on the first available poll. Retry timing is persisted in `next_retry_at`; an
unfinished attempt also persists a provisional retry time for restart recovery.
Successful occurrences do not repeat. On first startup, an eligible legacy failed
automatic occurrence without retry state gets a five-minute retry delay.
A newer scheduled occurrence takes precedence over an older retry. Scheduling
changes clear retry state; retention-only changes preserve it. Completion of an
older attempt cannot overwrite retry state belonging to edited settings.
A shared process gate prevents concurrent manual/automatic work. Maintenance
can also briefly return a 409 to a manual request.

For the card, show `is_running` first, then a failed `latest_attempt` when relevant;
otherwise show pending `next_scheduled_at` ahead of an older successful backup.
`next_scheduled_at` also includes the next retry time after an automatic failure.
An overdue timestamp represents pending catch-up work or a due retry. Once that occurrence has
completed, display the successful backup's `completed_at`, encrypted `size`, and
`retention_days`. Convert UTC timestamps to `Asia/Manila` and display
**Philippine Time (UTC+8)**. Disabled automatic backups have no next scheduled time.
The API supplies data; this repository contains no frontend to edit.

## Backup now and retry schema update

POST `/api/admin/backups` dumps the database, encrypts the dump, uploads the encrypted
file to B2, saves completed history, and returns metadata. It does not run retention
cleanup or delete older backups. Temporary files are still removed, and failures
still compensate by deleting only that attempt's possible uploaded object.
Retention cleanup belongs to the existing hourly background maintenance flow.

Before deploying this version, add a nullable `next_retry_at` column of type
`timestamp with time zone` to `backup_schedules`, with no default. Existing rows
start with null. The entity and EF configuration now expect this column. Generate,
review, and apply the corresponding migration yourself; no migrations or real
database operations were performed for this change, and the snapshot is untouched.
Run only one API instance with the scheduler enabled. Keep the encryption key in
User Secrets for Development and protected deployment configuration in production.

## Scheduled backup fix: results (2026-10-09)

The original local configuration was missing `Backups:EncryptionKey`, which stopped
execution at the configuration stage before pg_dump. The key is now present and
valid. Actual deployment credentials and connectivity have not been tested.
Previously, automatic attempts consumed their occurrence even when they failed,
so correcting configuration did not retry it. Successful manual requests also
awaited retention cleanup before responding. These two behaviors are corrected.

Changes in this revision:

| File | Change |
|---|---|
| `Application/Services/BackupService.cs` | Persistent five-minute automatic retries, restart reconciliation, guarded schedule updates, retry-aware health, and no retention work after backup creation |
| `Domain/Entities/BackupSchedule.cs` | Nullable UTC `NextRetryAt` state |
| `Infrastructure/Persistence/Configurations/BackupScheduleConfiguration.cs` | Explicit PostgreSQL UTC timestamp mapping for retry state |
| `Application/Interfaces/Repositories/IBackupRepository.cs` | Latest automatic attempt query contract |
| `Infrastructure/Repositories/BackupRepository.cs` | Latest finalized automatic attempt query, independent of manual history |
| `BACKUPS.md` | Schema prerequisite, corrected behavior, and verification results |

Controller routes and service public signatures remain unchanged. No endpoint defect
was reproduced: the existing controller already delegates Backup now to the service.
No migration, snapshot, application secrets, or unrelated source files were edited.

Verification: 27 isolated test groups passed against the real service and controller.
They cover daily/weekly Manila boundaries; catch-up and duplicate prevention;
persisted retry/recovery; edits during execution; manual encryption/decryption and
history; cancellation and concurrency; missing/invalid keys and dump executable;
dump/upload/history-save failures and compensation; separate retention maintenance;
validation/paging; EF column metadata; and local HTTP access, save, schedule, health,
history, and download responses (200/400/401/403/404/409/500).
HTTP authentication used a test handler to exercise Admin/Vendor/Enforcer roles;
real JWT issuance and signature validation were not tested. Database and storage
were fakes; pg_dump was a fixture executable. Restart was simulated by resetting
the service's process initialization state while preserving repository data.
Temporary-file tests ran under an isolated temp root. No real PostgreSQL/B2 calls
or restore operations were performed.

The temporary harness is retained outside the repository at
`C:\Users\Kian Gabriel\AppData\Local\Temp\MarikinaMarket-backup-tests-83cd9f587f474f33b0f83c8d2af89d3f`.
Before rerunning its executable, set its process TEMP and TMP to the harness's
`isolated-temp` directory, so service startup cleanup cannot touch application files.
The final `dotnet build MarikinaMarket.slnx --no-restore` passed with zero errors
and zero reported warnings. An earlier recompilation exposed three existing,
unrelated warnings: two CS8981 warnings for `updatedticketevidence` migration
types and CS0618 for `Message.Token` in `PushNotificationService`; these remain
unchanged.

Manual checks after applying the schema update in a disposable environment:
create a manual backup and verify decrypt/restore; schedule a backup a few minutes
ahead in Manila time; induce an automatic failure and verify no retry before five
minutes, then successful retry; restart during the retry delay; change/disable the
schedule during an attempt; verify retention runs separately. Confirm actual B2
permissions, PostgreSQL/pg_dump compatibility, and production key configuration.

## Manual verification on a disposable environment

- Confirm anonymous access returns 401 and Vendor/Enforcer access returns 403 for
  every backup route. An authenticated Admin can use all routes.
- GET schedule; confirm default values. PUT Daily and Weekly schedules, toggle
  enabled, and verify changes apply without restarting. Reject missing fields,
  undefined enum values, invalid `HH:mm`, unexpected weekday, and retention outside
  1–365. Test a schedule just ahead of the current Manila time.
- POST a manual backup, confirm Completed metadata, positive encrypted size, and
  authenticated download route. Download by ID and decrypt offline; inspect or
  restore the custom dump into a disposable database with pg_restore. Check that
  the bucket contains only the encrypted envelope and the temp folder is empty.
- Confirm automatic records use Automatic. Restart after a missed scheduled time:
  exactly one latest missed occurrence runs. A failed occurrence must not run
  again before its five-minute retry delay, then retry until successful. A schedule
  edit must cancel old retries and must not trigger old occurrences.
- Start two manual requests simultaneously: one succeeds and one returns 409.
  Automatic work must wait for a later poll when the gate is occupied.
- Simulate missing pg_dump, an invalid encryption key, timeout/cancellation, and
  upload failure. Verify terminal Failed history with sanitized errors and no
  leftover unencrypted dumps. Simulate termination during a backup and check
  restart recovery. Do not use real credentials/data for failure fixtures.
- Test history offsets and `has_more` with over ten records. Failed, unfinished,
  or expired backups cannot download; missing IDs/files return 404. Inspect every
  JSON response for absence of B2 keys, credentials, and encryption keys.
- With test records older than retention, run cleanup and confirm all versions of
  each exact key disappear while history remains with `can_download = false`.
  Deny deletion temporarily and confirm retry; re-enable permissions afterward.

## Changes by file

Paths below are relative to `MarikinaMarket.API`, except `.gitignore` at the repository root.

| File | Change |
|---|---|
| `Domain/Entities/Backup.cs` | Backup history, unfinished-attempt tracking, expiration, and storage cleanup state |
| `Domain/Entities/BackupSchedule.cs` | Persisted singleton schedule and attempted occurrence |
| `Domain/Enums/BackupType.cs` | Automatic and Manual |
| `Domain/Enums/BackupStatus.cs` | Completed and Failed |
| `Domain/Enums/BackupFrequency.cs` | Daily and Weekly |
| `Infrastructure/Persistence/Configurations/BackupConfiguration.cs` | EF column lengths, enum conversions, and history/cleanup indexes |
| `Infrastructure/Persistence/Configurations/BackupScheduleConfiguration.cs` | EF local-time mapping and schedule constraints |
| `Infrastructure/Persistence/AppDbContext.cs` | Two new DbSets; existing assembly configuration discovery remains in use |
| `Application/Interfaces/Repositories/IBackupRepository.cs` | Backup persistence contract |
| `Infrastructure/Repositories/BackupRepository.cs` | History paging, recovery, expiration updates, schedule reload, and cleanup queries |
| `Application/Interfaces/Services/IBackupService.cs` | Backup, schedule, health, download, and background operation contracts |
| `Application/Services/BackupService.cs` | Dump/encryption pipeline, validation, scheduling, gate, cleanup, and response mapping |
| `Application/Interfaces/Services/IStorageService.cs` | Stream upload/download and all-version deletion contracts |
| `Application/Services/StorageService.cs` | Existing B2 implementation extended for encrypted transfers and exact-key version deletion |
| `Application/DTOs/Backups/Request/UpdateBackupScheduleRequest.cs` | Required schedule request fields |
| `Application/DTOs/Backups/Response/BackupResponse.cs` | Public metadata without B2 object keys or secrets |
| `Application/DTOs/Backups/Response/BackupScheduleResponse.cs` | Schedule and fixed timezone display |
| `Application/DTOs/Backups/Response/BackupHealthResponse.cs` | Latest attempt/success, running state, retention, and next occurrence |
| `Infrastructure/BackgroundServices/AutomaticBackupService.cs` | Fresh-scope polling and hourly maintenance |
| `Presentation/Controllers/AdminBackupController.cs` | JWT/Admin routes and streamed attachment response |
| `Program.cs` | Repository/service DI and hosted-service registration |
| `.gitignore` | Narrow exception for the new Backups DTO source folder |
| `BACKUPS.md` | Setup, schema, encrypted format, manual tests, file summary, and API contract |

## API contract

Every route requires JWT authentication and the Admin role. Responses use the
project's snake_case JSON naming and string enums.

| Method / route | Request | Response |
|---|---|---|
| POST `/api/admin/backups` | No body | Backup metadata with ID and authenticated download route |
| GET `/api/admin/backups?offset=0` | Nonnegative offset | `items`, `has_more`, `total`; page size 10 |
| GET `/api/admin/backups/{id}/download` | Positive ID | `.mmbak` attachment, octet-stream, Cache-Control: no-store |
| GET `/api/admin/backups/schedule` | None | Persisted schedule and fixed timezone/display |
| PUT `/api/admin/backups/schedule` | See below | Saved schedule |
| GET `/api/admin/backups/health` | None | Latest attempt/success, running state, enabled, retention, next UTC occurrence |

Example schedule request:

```json
{
  "enabled": true,
  "frequency": "Weekly",
  "day_of_week": "Sunday",
  "time": "00:00",
  "retention_days": 30
}
```

Daily requests omit `day_of_week` or send null. Metadata contains `id`, `date_time`,
`type`, `size`, `status`, `completed_at`, `expires_at`, `error_message`,
`can_download`, and `download_route`. It never includes internal B2 keys.

Errors: 400 invalid fields/IDs/offset or failed/expired/unfinished downloads; 401
unauthenticated; 403 non-admin; 404 missing record/object; 409 execution gate busy;
500 sanitized backup/configuration/storage/persistence failure. Request cancellation
may end the HTTP connection while the server records failure and cleans up.
