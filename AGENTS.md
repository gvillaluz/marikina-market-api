# Backend rules (Marikina Market, ASP.NET)

## Workflow

- Before any feature work, read `../feature-workflow.md` and follow it.
- Commands for this repo: build `dotnet build`, tests `dotnet test`.
- Never run migrations or touch the real database. Describe schema changes instead.

## Architecture

- Layers: Controller → Service → Repository. No DbContext in services or controllers.
- Each module owns its data. Need tickets? Use the ticket repository or service, never query ticket tables from another module.
- Follow existing patterns for DTO naming, DI, interfaces, paging (PageResponse, offset, PAGE_SIZE, HasMore, Total), and exceptions (RecordNotFoundException, ValidationException, InvalidRequestException).
- Follow the existing coding pattern in Services and Repositories, such as TicketService.
- Don't add new patterns, folders, or NuGet packages without asking first.

## Security

- Never trust the client. Validate every request field, file type, size, and id on the server, even if the frontend already checks it.
- Take the current user or admin id from JWT claims, never from the request body.
- Restrict endpoints with `[Authorize(Roles = "...")]`. Use `[AllowAnonymous]` only on endpoints that don't require authentication.
- Never print or commit secrets. Use user-secrets.

## Code style

- Simple code: small methods, clear names, no speculative abstractions, no duplicated logic.
- Don't overengineer. Be highly confident before adding abstraction.
- Async all the way. Never use `.Result` or `.Wait()`.
- Dates are stored in UTC. Business dates use Asia/Manila time and are converted before querying.
- Wrap multi-step writes (user + vendor, report + snapshot) in one transaction.

## Finishing

- Run `dotnet build` and fix errors and new warnings before saying the task is done.
- Summarize changes per file and list what I should test manually.
- End with the API contract: route, request, response, and error cases.
- List unrelated bugs at the end. Don't fix them.
