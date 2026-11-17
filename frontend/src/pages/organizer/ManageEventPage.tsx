import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useEvent } from "../../hooks/queries";
import { useAction } from "../../hooks/useAction";
import { eventService } from "../../services/eventService";
import { registrationService } from "../../services/registrationService";
import type { EventStatus } from "../../types/api";
import { canManage, label, transitions } from "../../utils/domain";
import { formatDate, timeZone } from "../../utils/dates";
import {
  Badge,
  ConfirmDialog,
  Empty,
  ErrorNotice,
  Loading,
  PageHeading,
  Stats,
  Success,
} from "../../components/common/UI";
export default function ManageEventPage() {
  const { id = "" } = useParams();
  const { current } = useAuth();
  const event = useEvent(id);
  const allowed = !!event.data && canManage(current, event.data.organizationId);
  const attendance = useQuery({
    queryKey: ["attendance", current?.user.id, id],
    queryFn: ({ signal }) => registrationService.attendance(id, signal),
    enabled: allowed,
  });
  const [target, setTarget] = useState<EventStatus | null>(null);
  const mutation = useAction(
    (status: EventStatus) =>
      eventService.transition(id, status, event.data!.version),
    () => setTarget(null),
  );
  if (event.isPending) return <Loading />;
  if (event.isError)
    return (
      <ErrorNotice
        error={event.error}
        retry={() => {
          void event.refetch();
        }}
      />
    );
  if (!allowed)
    return (
      <Empty title="Access denied">
        You do not have a staff role in this event’s organization.
      </Empty>
    );
  const data = event.data;
  return (
    <>
      <Link
        className="back-link"
        to={`/manage/events?organizationId=${data.organizationId}`}
      >
        ← Organization events
      </Link>
      <PageHeading
        eyebrow="EVENT WORKSPACE"
        title={data.title}
        description={`${formatDate(data.startTime)} · ${data.venue} · ${timeZone}`}
      >
        <Badge value={data.status} />
        <Link className="button secondary" to={`/events/${id}`}>
          View event ↗
        </Link>
      </PageHeading>
      <ErrorNotice error={mutation.error} />
      <Success>
        {mutation.isSuccess && "Event status updated successfully."}
      </Success>
      <ErrorNotice
        error={attendance.error}
        retry={() => {
          void attendance.refetch();
        }}
      />
      <Stats
        items={[
          { label: "Capacity", value: data.maxCapacity },
          { label: "Confirmed", value: attendance.data?.confirmed ?? "—" },
          { label: "Waitlisted", value: attendance.data?.waitlisted ?? "—" },
          { label: "Checked in", value: attendance.data?.checkedIn ?? "—" },
        ]}
      />
      <div className="detail-layout">
        <section className="panel stack">
          <h2>Manage this event</h2>
          <p className="preserve-lines">{data.description}</p>
          <div className="actions">
            {data.status !== "COMPLETED" && data.status !== "CANCELLED" && (
              <Link
                className="button secondary"
                to={`/manage/events/${id}/edit`}
              >
                Edit event details
              </Link>
            )}
            <Link className="button" to={`/manage/events/${id}/registrations`}>
              Manage registrations ↗
            </Link>
            <Link
              className="button secondary"
              to={`/manage/events/${id}/attendance`}
            >
              Take attendance ↗
            </Link>
          </div>
          <dl className="details-list">
            <div>
              <dt>Event type</dt>
              <dd>{data.type}</dd>
            </div>
            <div>
              <dt>Ends</dt>
              <dd>{formatDate(data.endTime)}</dd>
            </div>
            <div>
              <dt>Current version</dt>
              <dd>{data.version}</dd>
            </div>
          </dl>
        </section>
        <section className="panel stack">
          <p className="eyebrow">EVENT LIFECYCLE</p>
          <h2>What’s next?</h2>
          <p className="muted">
            Publish your draft, open registration, then complete the event after
            attendance is recorded.
          </p>
          {transitions[data.status].map((status) => (
            <button
              key={status}
              className={`button ${status === "CANCELLED" ? "danger" : ""}`}
              disabled={mutation.isPending || event.isFetching}
              onClick={() => {
                mutation.reset();
                setTarget(status);
              }}
            >
              {status === "PUBLISHED"
                ? "Publish event"
                : status === "REGISTRATION_OPEN"
                  ? "Open registration"
                  : status === "COMPLETED"
                    ? "Complete event"
                    : "Cancel event"}
            </button>
          ))}
          {!transitions[data.status].length && (
            <p className="notice">
              This event is {label(data.status).toLowerCase()}. No further
              transitions are available.
            </p>
          )}
          <button
            className="text-button"
            onClick={() => {
              void event.refetch();
              void attendance.refetch();
            }}
          >
            Reload current status
          </button>
        </section>
      </div>
      {target && (
        <ConfirmDialog
          title={`Change status to ${label(target).toLowerCase()}?`}
          busy={mutation.isPending}
          confirmLabel="Update event status"
          onClose={() => setTarget(null)}
          onConfirm={() => mutation.mutate(target)}
        >
          <p>
            {target === "CANCELLED"
              ? "The event will be cancelled and all active registrations will be canceled. This cannot be undone."
              : target === "COMPLETED"
                ? "The event will be completed. Further edits, registrations, and check-ins will be closed."
                : target === "PUBLISHED"
                  ? "Your event will become publicly visible. Registration will remain closed until you open it."
                  : "Participants will be able to register. Full events will accept a waiting list."}
          </p>
          <ErrorNotice error={mutation.error} />
        </ConfirmDialog>
      )}
    </>
  );
}
