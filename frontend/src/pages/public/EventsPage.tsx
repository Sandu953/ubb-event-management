import { useSearchParams } from "react-router-dom";
import type { FormEvent } from "react";
import { useEvents } from "../../hooks/queries";
import { useAuth } from "../../contexts/auth";
import EventCard from "../../components/events/EventCard";
import {
  Empty,
  ErrorNotice,
  Field,
  Loading,
  PageHeading,
  Pagination,
  Select,
} from "../../components/common/UI";
import { eventStatuses, label, uuidPattern } from "../../utils/domain";
import { timeZone, toLocalInput, toUtc } from "../../utils/dates";
function localDate(value: string | null) {
  return value && !Number.isNaN(Date.parse(value)) ? toLocalInput(value) : "";
}
export default function EventsPage() {
  const [params, setParams] = useSearchParams();
  const { current } = useAuth();
  const page = Math.max(1, Number(params.get("page")) || 1);
  const events = useEvents({
    page,
    pageSize: 12,
    search: params.get("search") || "",
    type: params.get("type") || "",
    organizationId: params.get("organizationId") || "",
    status: params.get("status") || "",
    from: params.get("from") || "",
    to: params.get("to") || "",
  });
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const values = new URLSearchParams();
    for (const [key, value] of data.entries())
      if (typeof value === "string" && value.trim())
        values.set(
          key,
          key === "from" || key === "to" ? toUtc(value) : value.trim(),
        );
    setParams(values);
  }
  return (
    <>
      <PageHeading
        eyebrow="FIND YOUR NEXT EXPERIENCE"
        title="Explore events"
        description={`Workshops, conversations, and moments that bring us together. Times in ${timeZone}.`}
      />
      <form key={params.toString()} className="panel filters" onSubmit={submit}>
        <div className="filter-primary">
          <Field
            label="Search events"
            name="search"
            placeholder="Title, description, or venue…"
            maxLength={200}
            defaultValue={params.get("search") || ""}
          />
          <Field
            label="Event type"
            name="type"
            placeholder="Exact type, e.g. Workshop"
            maxLength={100}
            defaultValue={params.get("type") || ""}
          />
          <Select
            label="Event status"
            name="status"
            defaultValue={params.get("status") || ""}
          >
            <option value="">All public events</option>
            {eventStatuses
              .filter((s) => s !== "DRAFT")
              .map((s) => (
                <option key={s} value={s}>
                  {label(s)}
                </option>
              ))}
          </Select>
          <button className="button" type="submit">
            Find events ↗
          </button>
        </div>
        <details>
          <summary>More filters</summary>
          <div className="form-grid">
            <Field
              label="Starting from"
              name="from"
              type="datetime-local"
              defaultValue={localDate(params.get("from"))}
            />
            <Field
              label="Starting before"
              name="to"
              type="datetime-local"
              defaultValue={localDate(params.get("to"))}
            />
            <Field
              label="Organization ID"
              name="organizationId"
              pattern={uuidPattern}
              placeholder="Organization UUID"
              list="known-organizations"
              defaultValue={params.get("organizationId") || ""}
              hint="The public API supports filtering by ID; a public organization directory is not available."
            />
            <datalist id="known-organizations">
              {current?.memberships.map((m) => (
                <option key={m.organizationId} value={m.organizationId}>
                  {m.organizationName}
                </option>
              ))}
            </datalist>
          </div>
        </details>
        {params.size > 0 && (
          <button
            className="text-button"
            type="button"
            onClick={() => setParams({})}
          >
            Clear filters
          </button>
        )}
      </form>
      {events.isPending ? (
        <Loading />
      ) : events.isError ? (
        <ErrorNotice
          error={events.error}
          retry={() => {
            void events.refetch();
          }}
        />
      ) : (
        <>
          <div className="results-heading">
            <span>{events.data.totalCount} events to explore</span>
            <span className="muted">Sorted by start time</span>
          </div>
          {events.data.items.length ? (
            <div className="event-grid">
              {events.data.items.map((event) => (
                <EventCard key={event.id} event={event} />
              ))}
            </div>
          ) : (
            <Empty title="No events found">
              Try another search or clear your filters.
            </Empty>
          )}
          <Pagination
            {...events.data}
            onPage={(next) => {
              const updated = new URLSearchParams(params);
              updated.set("page", String(next));
              setParams(updated);
            }}
          />
        </>
      )}
    </>
  );
}
