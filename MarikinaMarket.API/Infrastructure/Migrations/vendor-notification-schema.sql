-- Reviewable schema/backfill plan only. Not applied by the API or this task.
-- Apply through the approved database-change process before running the new code.
BEGIN;

ALTER TABLE public.notifications
    ADD COLUMN vendor_id integer,
    ADD COLUMN vendor_is_read boolean NOT NULL DEFAULT false;

UPDATE public.notifications AS notification
SET vendor_id = ticket.vendor_id
FROM public.tickets AS ticket
WHERE notification.ticket_id = ticket.id;

DO $check$
BEGIN
    IF EXISTS (SELECT 1 FROM public.notifications WHERE vendor_id IS NULL) THEN
        RAISE EXCEPTION 'Vendor backfill incomplete. Review unmatched notification tickets before continuing.';
    END IF;
END;
$check$;

ALTER TABLE public.notifications
    ALTER COLUMN vendor_id SET NOT NULL,
    ADD CONSTRAINT fk_notifications_vendor_profiles_vendor_id
        FOREIGN KEY (vendor_id) REFERENCES public.vendor_profiles(id) ON DELETE RESTRICT;

CREATE INDEX ix_notifications_vendor_id_created_at_id
    ON public.notifications (vendor_id, created_at DESC, id DESC);

COMMIT;
