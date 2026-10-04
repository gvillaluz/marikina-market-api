# Backend rules (Marikina Market, ASP.NET)

## Architecture

- Layers: Controller → Service → Repository. No DbContext in services or controllers.
- Follow existing patterns for DTO naming, DI, interfaces, paging (PageResponse, offset, PAGE_SIZE, HasMore, Total), and exceptions (RecordNotFoundException, ValidationException, InvalidRequestException).
- Each module owns its data. Need tickets? Use the ticket repository/service, never query ticket tables from another module.
- Don't add new patterns, folders, or NuGet packages without asking first.

## Security

- Take the current user/admin id from JWT claims, never from the request body.
- Restrict endpoints with [Authorize(Roles = "...")] on the server. Use AllowAnonymous to the endpoints that are not need to be authenticate.
- Never print or commit secrets. Use user-secrets.

## Code style

- Simple code: small methods, clear names, no speculative abstractions, no duplicated logic.
- Async all the way. Never use .Result or .Wait().
- Dates stored in UTC. Business dates use Asia/Manila time and are converted before querying.
- Wrap multi-step writes (user + vendor, report + snapshot) in one transaction.

## Workflow

- Before coding: read the relevant files, then give a short plan (files to change, approach, questions). Wait for my OK.
- Never run migrations or touch the real database. Describe the schema change instead.
- Run `dotnet build` and fix errors and new warnings before finishing.
- Stay inside the files and areas I name. If something outside needs changing, stop and ask.
- Unrelated bugs: list them at the end, don't fix them.
- For every feature, start with a plan and wait for my approval. Skip the plan only for trivial single-file edits.

## When finished

- Summarize changes per file and what I should test manually.
- End with the API contract: route, request, response, error cases.
