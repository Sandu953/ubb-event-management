import { useState, type FormEvent } from "react";
import type { Event, EventDetails } from "../../types/api";
import { useAuth } from "../../contexts/auth";
import { canManage } from "../../utils/domain";
import { timeZone, toLocalInput, toUtc } from "../../utils/dates";
import OrganizationPicker from "../organizations/OrganizationPicker";
import { Field } from "../common/UI";
export default function EventForm({
  event,
  initialOrganization = "",
  confirmed,
  busy,
  onSubmit,
}: {
  event?: Event;
  initialOrganization?: string;
  confirmed?: number;
  busy: boolean;
  onSubmit: (details: EventDetails, organizationId: string) => void;
}) {
  const { current } = useAuth();
  const [organizationId, setOrganizationId] = useState(
    event?.organizationId || initialOrganization,
  );
  const [validation, setValidation] = useState("");
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const data = new FormData(e.currentTarget);
    const title = String(data.get("title")).trim(),
      type = String(data.get("type")).trim(),
      description = String(data.get("description")).trim(),
      venue = String(data.get("venue")).trim();
    const start = String(data.get("startTime")),
      end = String(data.get("endTime")),
      maxCapacity = Number(data.get("maxCapacity"));
    if (!organizationId || !canManage(current, organizationId)) {
      setValidation("Choose an organization you are authorized to manage.");
      return;
    }
    if (!title || !type || !description || !venue) {
      setValidation("Complete all required event details.");
      return;
    }
    if (
      !start ||
      !end ||
      !Number.isFinite(Date.parse(start)) ||
      !Number.isFinite(Date.parse(end)) ||
      Date.parse(end) <= Date.parse(start)
    ) {
      setValidation("End time must be after start time.");
      return;
    }
    if (
      !Number.isInteger(maxCapacity) ||
      maxCapacity < 1 ||
      maxCapacity > 2147483647 ||
      maxCapacity < (confirmed ?? 0)
    ) {
      setValidation(
        "Capacity must be a positive whole number and cannot be less than confirmed registrations.",
      );
      return;
    }
    setValidation("");
    onSubmit(
      {
        title,
        type,
        description,
        venue,
        startTime: toUtc(start),
        endTime: toUtc(end),
        maxCapacity,
      },
      organizationId,
    );
  }
  return (
    <form className="stack" onSubmit={submit}>
      <fieldset disabled={busy} className="stack">
        {!event && (
          <OrganizationPicker
            value={organizationId}
            onChange={setOrganizationId}
            required
          />
        )}
        <Field
          label="Event title"
          name="title"
          maxLength={200}
          required
          defaultValue={event?.title}
          placeholder="Give your community something to look forward to"
        />
        <div className="form-grid">
          <Field
            label="Event type"
            name="type"
            maxLength={100}
            required
            defaultValue={event?.type}
            placeholder="Workshop, conference, social…"
          />
          <Field
            label="Venue"
            name="venue"
            maxLength={500}
            required
            defaultValue={event?.venue}
          />
        </div>
        <div className="field">
          <label htmlFor="event-description">Description *</label>
          <textarea
            id="event-description"
            name="description"
            rows={6}
            maxLength={10000}
            required
            defaultValue={event?.description}
          />
        </div>
        <p className="small muted">
          Enter local times in {timeZone}. Times are converted to UTC when
          saved.
        </p>
        <div className="form-grid">
          <Field
            label="Start date and time"
            name="startTime"
            type="datetime-local"
            required
            defaultValue={event ? toLocalInput(event.startTime) : ""}
          />
          <Field
            label="End date and time"
            name="endTime"
            type="datetime-local"
            required
            defaultValue={event ? toLocalInput(event.endTime) : ""}
          />
        </div>
        <Field
          label="Maximum capacity"
          name="maxCapacity"
          type="number"
          min={Math.max(1, confirmed || 0)}
          max={2147483647}
          step={1}
          required
          defaultValue={event?.maxCapacity || 30}
          hint={
            confirmed === undefined
              ? "A full event automatically places new participants on the waiting list."
              : `${confirmed} confirmed participants. The server rechecks capacity when saving.`
          }
        />
        {validation && (
          <div className="notice error" role="alert">
            {validation}
          </div>
        )}
        <button type="submit" className="button" disabled={busy}>
          {busy ? "Saving…" : event ? "Save changes" : "Create draft event ↗"}
        </button>
      </fieldset>
    </form>
  );
}
