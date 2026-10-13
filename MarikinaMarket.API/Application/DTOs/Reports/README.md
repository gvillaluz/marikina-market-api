# Reports API

All endpoints require a bearer JWT with `HeadAdmin` or `AdminOfficer` role.
Responses use snake_case. Examples below use illustrative data, not database records.

## Rules

- Dates use `YYYY-MM-DD`, are inclusive in Asia/Manila, and filter ticket `IssuedAt`.
- `type` is exactly `Warning`, `Ticket`, or `Warning & Ticket`.
- A record is one warning/ticket, regardless of its ordinance count.
- Tickets with `Paid`, `Cleared`, or `Waived` status are resolved; all others are pending.
- Warnings count as resolved when issued and have zero assessed penalties.
- Assessed penalties sum `TotalPaymentAmount`, including waived tickets; null amounts contribute zero.
- Monetary assessed penalties do not include community service hours.
- Generated reports preserve their counts, penalties, generator ID and name. Detail never recalculates from live tickets.
- Empty periods return zero metrics. The unused breakdown is null for a single-type report.
- Report generation metadata uses UTC. The authenticated administrator ID is never read from the body.

## Generate and save

`POST /api/admin/reports`

Request body (all three fields required):

```json
{
  "from": "2026-10-01",
  "to": "2026-10-12",
  "type": "Warning & Ticket"
}
```

Response: `200 OK`.

```json
{
  "id": 1,
  "type": "Warning & Ticket",
  "date_range": {
    "from": "2026-10-01",
    "to": "2026-10-12"
  },
  "date_generated": "2026-10-12T01:00:00Z",
  "generated_by": {
    "id": 7,
    "name": "Juan Dela Cruz"
  },
  "summary": {
    "total": { "total_records": 7, "assessed_penalties": 1200 },
    "resolved": { "total_records": 5, "assessed_penalties": 700 },
    "pending": { "total_records": 2, "assessed_penalties": 500 },
    "warnings_issued": {
      "total": { "total_records": 3, "assessed_penalties": 0 },
      "resolved": { "total_records": 3, "assessed_penalties": 0 },
      "pending": { "total_records": 0, "assessed_penalties": 0 }
    },
    "tickets_issued": {
      "total": { "total_records": 4, "assessed_penalties": 1200 },
      "resolved": { "total_records": 2, "assessed_penalties": 700 },
      "pending": { "total_records": 2, "assessed_penalties": 500 }
    }
  }
}
```

## View a saved report

`GET /api/admin/reports/1`

No request body. Response: `200 OK`, with the same complete JSON structure as generation above.
Returns the saved snapshot, even if underlying tickets or generator names change later.

## Preview a selected date

`GET /api/admin/reports/preview?date=2026-10-12`

No request body. `date` is required. Optional `type` defaults to `Warning & Ticket`.
To specify both explicitly, URL-encode the value as `Warning%20%26%20Ticket`.
This endpoint reads live data and does not create a history entry.

Response: `200 OK`.

```json
{
  "type": "Warning & Ticket",
  "date_range": { "from": "2026-10-12", "to": "2026-10-12" },
  "summary": {
    "total": { "total_records": 7, "assessed_penalties": 1200 },
    "resolved": { "total_records": 5, "assessed_penalties": 700 },
    "pending": { "total_records": 2, "assessed_penalties": 500 },
    "warnings_issued": {
      "total": { "total_records": 3, "assessed_penalties": 0 },
      "resolved": { "total_records": 3, "assessed_penalties": 0 },
      "pending": { "total_records": 0, "assessed_penalties": 0 }
    },
    "tickets_issued": {
      "total": { "total_records": 4, "assessed_penalties": 1200 },
      "resolved": { "total_records": 2, "assessed_penalties": 700 },
      "pending": { "total_records": 2, "assessed_penalties": 500 }
    }
  }
}
```

## Reports history

`GET /api/admin/reports?offset=0`

No request body. Offset defaults to zero. Page size is 10, ordered by generation timestamp descending,
then ID descending. Use offset 10 for the next page. `total` counts all saved reports.

Response: `200 OK`.

```json
{
  "items": [
    {
      "id": 1,
      "type": "Warning & Ticket",
      "date_range": { "from": "2026-10-01", "to": "2026-10-12" },
      "date_generated": "2026-10-12T01:00:00Z",
      "generated_by": { "id": 7, "name": "Juan Dela Cruz" }
    }
  ],
  "has_more": false,
  "total": 1
}
```

Empty history returns `{"items":[],"has_more":false,"total":0}`.

## Error cases

| Status | Cases |
| --- | --- |
| 400 | Missing/malformed dates, reversed range, unsupported type, invalid ID, negative/malformed offset, malformed JSON |
| 401 | Missing or invalid authentication |
| 403 | Role is not an administrator; missing/invalid administrator claim or account during generation |
| 404 | Positive report ID does not exist |
| 500 | Unexpected server/storage failure, using the existing middleware response |

The extreme dates `0001-01-01` as a start and `9999-12-31` as an end are rejected,
because the inclusive range must be convertible to UTC with an exclusive next-day endpoint.

Service validation example (`400`):

```json
{ "message": "From must be on or before to." }
```

Missing report example (`404`):

```json
{ "message": "Report not found." }
```

Malformed JSON, model binding, and missing required body fields use ASP.NET's existing
ValidationProblemDetails response (`type`, `title`, `status`, `errors`, `traceId`).
Authentication/role failures may have an empty body, following existing authentication middleware.

## Files and schema

| File | Change |
| --- | --- |
| `Presentation/Controllers/AdminReportController.cs` | Four admin-only endpoints; reads generator ID from JWT |
| `Application/Services/ReportService.cs` | Validation, Manila conversion, metrics, transactional generation, history and saved detail |
| `Application/Interfaces/Services/IReportService.cs` | Report service contract |
| `Application/Interfaces/Repositories/IReportRepository.cs` | Report persistence contract |
| `Infrastructure/Repositories/ReportRepository.cs` | Saved-report insert/read/count/paging |
| `Application/Interfaces/Repositories/ITicketRepository.cs` | Ticket-owned aggregation contract |
| `Infrastructure/Repositories/TicketRepository.cs` | Date/type/status aggregation on ticket records |
| `Application/DTOs/Tickets/Internal/TicketReportSummary.cs` | Internal aggregation result |
| `Application/DTOs/Reports/Request/GenerateReportRequest.cs` | Required from/to/type request |
| `Application/DTOs/Reports/Response/ReportSummaryResponse.cs` | History metadata, date range and generator DTOs |
| `Application/DTOs/Reports/Response/ReportDetailResponse.cs` | Saved detail, daily preview and metric DTOs |
| `Domain/Entities/Report.cs` | Metadata and counts/penalty snapshot in one row |
| `Infrastructure/Persistence/Configurations/ReportConfiguration.cs` | Lengths, decimal precision, indexes and generator relationship |
| `Infrastructure/Persistence/AppDbContext.cs` | Reports DbSet |
| `Program.cs` | Report repository/service DI registration |
| `Infrastructure/Migrations/20261011190817_AddReports.cs` | Creates reports table; rollback drops it |
| `Infrastructure/Migrations/20261011190817_AddReports.Designer.cs` | Generated migration model |
| `Infrastructure/Migrations/AppDbContextModelSnapshot.cs` | Current model including reports |
| `MarikinaMarket.Tests/ReportTests.cs` | Service/HTTP validation, authorization, snapshots, transactions, pagination and JSON tests |
| `MarikinaMarket.Tests/ReportRepositoryTests.cs` | Actual LINQ aggregation, PostgreSQL translation and offline migration/model tests |

The migration adds only `reports`: identity ID; report type; date range; UTC generation timestamp;
generator ID/name; warning count; resolved/pending ticket counts; resolved/pending monetary sums.
Monetary columns use `numeric(18,2)`. The generator foreign key references `AspNetUsers`
with restricted deletion. Indexes support generation date/ID ordering and generator lookup.
The migration has been generated, not applied.

## Manual checks after you apply the migration

1. Preview a known Manila date; confirm midnight boundaries and that history is unchanged.
2. Generate each of the three types and compare counts/amounts with the source inspections.
3. Confirm zero metrics for a period without inspections.
4. Change a ticket status after generation; verify saved detail remains unchanged.
5. Generate more than ten reports; check both history pages and the View action's report ID.
6. Check both administrator roles, vendor/enforcer rejection, invalid dates/type and missing report ID.

Automated checks use an HTTP test host and replace database boundaries; PostgreSQL SQL and migrations
are checked offline. No live database or frontend end-to-end session is run.
