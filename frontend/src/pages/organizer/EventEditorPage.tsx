import {
  Link,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useEvent } from "../../hooks/queries";
import { useAction } from "../../hooks/useAction";
import { eventService } from "../../services/eventService";
import { registrationService } from "../../services/registrationService";
import type { Event, EventDetails } from "../../types/api";
import { canManage } from "../../utils/domain";
import EventForm from "../../components/events/EventForm";
import {
  Empty,
  ErrorNotice,
  Loading,
  PageHeading,
} from "../../components/common/UI";
export default function EventEditorPage() {
  const { id = "" } = useParams();
  const [params] = useSearchParams();
  const { current } = useAuth();
  const query = useEvent(id);
  const allowed = !!query.data && canManage(current, query.data.organizationId);
  const attendance = useQuery({
    queryKey: ["attendance", current?.user.id, id],
    queryFn: ({ signal }) => registrationService.attendance(id, signal),
    enabled: !!id && allowed,
  });
  if (id && query.isPending) return <Loading />;
  if (id && query.isError && !query.data)
    return (
      <ErrorNotice
        error={query.error}
        retry={() => {
          void query.refetch();
        }}
      />
    );
  if (
    id &&
    (!allowed ||
      query.data?.status === "COMPLETED" ||
      query.data?.status === "CANCELLED")
  )
    return (
      <Empty title="This event cannot be edited">
        You need organization staff access and an event that is still active.
      </Empty>
    );
  return (
    <>
      <Link
        className="back-link"
        to={id ? `/manage/events/${id}` : "/manage/events"}
      >
        ← Back to events
      </Link>
      <PageHeading
        eyebrow="ORGANIZER WORKSPACE"
        title={id ? "Edit event" : "Create an event"}
        description={
          id
            ? "Keep your community informed with accurate event details."
            : "Start with a draft. Publish when everything is ready."
        }
      />
      <section className="panel editor-panel">
        <ErrorNotice error={attendance.error} />
        <Editor
          key={id || "new"}
          event={id ? query.data : undefined}
          initialOrganization={
            params.get("organizationId") ||
            current?.memberships[0]?.organizationId ||
            ""
          }
          confirmed={attendance.data?.confirmed}
          reload={async () =>
            (await query.refetch({ throwOnError: true })).data
          }
        />
      </section>
    </>
  );
}

function Editor({
  event,
  initialOrganization,
  confirmed,
  reload,
}: {
  event?: Event;
  initialOrganization: string;
  confirmed?: number;
  reload: () => Promise<Event | undefined>;
}) {
  // Keep the loaded version with the editable snapshot. Background refetches must
  // neither discard unsaved text nor silently submit a newer server version.
  const [snapshot, setSnapshot] = useState(event);
  const [formRevision, setFormRevision] = useState(0);
  const [reloadError, setReloadError] = useState<Error | null>(null);
  const [reloading, setReloading] = useState(false);
  const navigate = useNavigate();
  const save = useAction(
    ({
      details,
      organizationId,
    }: {
      details: EventDetails;
      organizationId: string;
    }) =>
      snapshot
        ? eventService.update(snapshot.id, {
            ...details,
            version: snapshot.version,
          })
        : eventService.create({ ...details, organizationId }),
    (result) => navigate(`/manage/events/${result.id}`, { replace: true }),
  );
  async function reloadSnapshot() {
    setReloading(true);
    try {
      const latest = await reload();
      if (!latest)
        throw new Error(
          "Unable to reload event details. Your form has been kept.",
        );
      setSnapshot(latest);
      setFormRevision((revision) => revision + 1);
      save.reset();
      setReloadError(null);
    } catch (error) {
      setReloadError(
        error instanceof Error
          ? error
          : new Error("Unable to reload event details."),
      );
    } finally {
      setReloading(false);
    }
  }
  return (
    <>
      <ErrorNotice error={reloadError || save.error} />
      {snapshot && (
        <p className="notice">
          Editing version {snapshot.version}. Your form is preserved if a save
          fails.{" "}
          <button
            className="text-button"
            disabled={save.isPending || reloading}
            onClick={() => {
              void reloadSnapshot();
            }}
          >
            Reload latest event details
          </button>{" "}
          to replace the form with the current server values before retrying a
          stale update.
        </p>
      )}
      <EventForm
        key={formRevision}
        event={snapshot}
        initialOrganization={initialOrganization}
        confirmed={confirmed}
        busy={save.isPending || reloading}
        onSubmit={(details, organizationId) =>
          save.mutate({ details, organizationId })
        }
      />
    </>
  );
}
