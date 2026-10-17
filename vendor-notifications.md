# Vendor notifications

## Schema and deployment

The entity now has required `VendorId` and independent `VendorIsRead` fields. `IsRead` remains the enforcer's read state. The vendor foreign key references `vendor_profiles.id`, with restricted deletion. A `(vendor_id, created_at DESC, id DESC)` index supports owned, ordered paging.

The prepared EF migration is [AddVendorNotifications](MarikinaMarket.API/Infrastructure/Migrations/20261014120000_AddVendorNotifications.cs), with matching designer and model snapshot. The readable backfill plan is [vendor-notification-schema.sql](MarikinaMarket.API/Infrastructure/Migrations/vendor-notification-schema.sql). Neither has been applied, and database migration history has not been changed. Stop the old API/background workers, review and apply the EF migration through the approved database-change process, then deploy/restart the updated API. The SQL plan documents the same change; choose one application path.

Existing notifications get their vendor ID from the linked ticket. Existing enforcer read flags are preserved; vendor read flags start as `false`. An unmatched ticket aborts the schema transaction. The script does not create historical inspection notifications where none existed.

## Saving and push delivery

- Successful warning and violation inspections save one notification with the ticket, vendor profile and authenticated enforcer IDs, inside the inspection transaction.
- Ticket-status changes and overdue processing also save the vendor ID.
- Notifications start unread for each recipient independently.
- Vendor push delivery runs after commit and resolves `vendor profile ID -> user account ID -> device tokens`. Push failure does not roll back the inspection or notification.
- Push event types are `inspection_submitted`, `ticket_updated`, and `ticket_overdue`.
- Warnings have no ticket status, penalty, or due date in the vendor response. The stored notification status uses `Pending` for the initial warning event.
- Ticket due dates follow the existing enforcer notification calculation: issuance plus five days. The existing difference from the issued-ticket email's fifteen-day date was not changed.

## List vendor notifications

`GET /api/Notification/vendor?offset=0&filter=All`

Authorization: bearer JWT with role `MarketVendor`. The API resolves the vendor profile from the JWT user ID. There is no vendor/user ID request field and no JSON request body.

Query:

- `offset`: integer >= 0, default `0`.
- `filter`: `All` or `Unread`, default `All` (case-insensitive).
- Page size: 10. Results sort by creation time descending, then notification ID descending. `total` counts owned notifications matching the filter, including rows outside the page.

Example `200 OK`:

```json
{
  "items": [
    {
      "id": 71,
      "vendor_id": 17,
      "enforcer_id": 8,
      "ticket_id": 11,
      "control_number": "11",
      "trade_name": "Reyes Fresh Fish",
      "market_section_name": "Fish and Seafood",
      "type": "Ticket",
      "penalty_type": "CashFine",
      "total_fine_amount": 500,
      "due_date": "2026-10-19T02:00:00Z",
      "status": "Pending",
      "message": "A violation ticket (11) was issued for Reyes Fresh Fish.",
      "is_read": false,
      "created_at": "2026-10-14T02:00:00Z"
    }
  ],
  "has_more": false,
  "total": 1
}
```

An empty page returns `items: []`, `has_more: false`, and the matching `total`. For a warning, `type` is `Warning` and `penalty_type`, `total_fine_amount`, `due_date`, and `status` are `null`.

Errors: `400` invalid query; `401` missing/invalid authentication; `403` wrong role; `404` vendor profile missing.

## Mark a vendor notification as read

`PATCH /api/Notification/vendor/71/read`

Authorization: bearer JWT with role `MarketVendor`. No request body. Returns `204 No Content`. Only `VendorIsRead` changes; enforcer `IsRead` is unaffected. Repeating the operation is allowed.

Errors: `400` nonpositive ID; `401` missing/invalid authentication; `403` wrong role; `404` missing vendor profile, missing notification, or notification belonging to another vendor.

Existing enforcer routes remain `GET /api/Notification` and `PATCH /api/Notification/{notificationId}/read` with their existing response fields and ownership rules.

## Register the vendor device

`POST /api/User/device-token` now also permits `MarketVendor`. The user ID comes from JWT claims.

Request:

```json
{ "device_token": "<FCM token returned by the device>" }
```

Response `200 OK`:

```json
{ "message": "Device token registered." }
```

Existing device-token validation and authentication errors remain unchanged. A vendor without registered device tokens still gets saved in-app notifications; no push can be delivered to that device.

## Push payload

Example violation inspection push (FCM target token omitted):

```json
{
  "notification": {
    "title": "Violation ticket issued",
    "body": "A violation ticket (11) was issued for Reyes Fresh Fish."
  },
  "data": {
    "type": "inspection_submitted",
    "ticketId": "11",
    "vendorId": "17",
    "inspection_type": "Ticket",
    "status": "Pending"
  }
}
```

All data values are strings. The `status` data field is omitted for warnings. Vendor ID is a profile ID, not an account ID.

## Manual verification

After applying the reviewed schema and restarting the API:

1. Register a vendor device token, submit a warning and a violation inspection, and verify one owned in-app notification and push per inspection.
2. Verify `All`, `Unread`, pagination, and the empty state. Another vendor must not see or mark these notifications.
3. Read the notification as an enforcer, then as the vendor; neither recipient's flag should change the other's.
4. Update a ticket status and run overdue processing on a local database; verify saved vendor ownership and vendor push events.
5. With no vendor token or failed push delivery, verify inspection and notification persistence still succeeds.

Only the backend changed. Mobile clients must consume the vendor endpoint and register their FCM token for the vendor account. No real database or FCM delivery was used in automated verification.
