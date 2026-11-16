import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { registrationService } from "../../services/registrationService";
import { useAction } from "../../hooks/useAction";
import {
  Badge,
  ConfirmDialog,
  Empty,
  ErrorNotice,
  Loading,
  PageHeading,
  Pagination,
  Select,
  Success,
} from "../../components/common/UI";
import type { MyRegistration } from "../../types/api";
import { canCancel } from "../../utils/domain";
import { formatDate, timeZone } from "../../utils/dates";
export default function RegistrationsPage() {
  const { current } = useAuth();
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState("");
  const [selected, setSelected] = useState<MyRegistration | null>(null);
  const query = useQuery({
    queryKey: ["registrations", current?.user.id, page, status],
    queryFn: ({ signal }) => registrationService.mine(page, status, signal),
  });
  const cancel = useAction(
    (id: string) => registrationService.cancel(id),
    () => setSelected(null),
  );
  return (
    <>
      <PageHeading
        eyebrow="YOUR CAMPUS CALENDAR"
        title="My registrations"
        description={`Your confirmed places, waiting lists, and event history. Times in ${timeZone}.`}
      >
        <button
          className="button secondary"
          disabled={query.isFetching}
          onClick={() => {
            void query.refetch();
          }}
        >
          Refresh status
        </button>
        <Link className="button" to="/events">
          Find an event ↗
        </Link>
      </PageHeading>
      <div className="panel toolbar">
        <Select
          label="Registration status"
          value={status}
          onChange={(event) => {
            setStatus(event.target.value);
            setPage(1);
          }}
        >
          <option value="">All registrations</option>
          <option value="CONFIRMED">Confirmed</option>
          <option value="WAITLISTED">Waitlisted</option>
          <option value="CANCELED">Canceled</option>
        </Select>
        <p className="muted">
          Waiting-list promotions appear when you refresh. Your history is
          preserved after cancellation.
        </p>
      </div>
      <ErrorNotice error={cancel.error} />
      <Success>
        {cancel.isSuccess &&
          "Registration canceled. Your history has been updated."}
      </Success>
      {query.isPending ? (
        <Loading />
      ) : query.isError ? (
        <ErrorNotice
          error={query.error}
          retry={() => {
            void query.refetch();
          }}
        />
      ) : (
        <>
          {query.data.items.length ? (
            <div className="stack">
              {query.data.items.map((item) => (
                <article key={item.id} className="panel registration-row">
                  <div>
                    <div className="actions">
                      <Badge value={item.status} />
                      {item.checkedIn && (
                        <span className="badge badge-confirmed">
                          Checked in
                        </span>
                      )}
                    </div>
                    <h2>
                      <Link to={`/events/${item.eventId}`}>
                        {item.eventTitle}
                      </Link>
                    </h2>
                    <p className="muted">
                      {formatDate(item.startTime)} · {item.venue}
                    </p>
                    {item.status === "WAITLISTED" && (
                      <p className="small">
                        You’ll be promoted automatically if a place becomes
                        available.
                      </p>
                    )}
                  </div>
                  <div className="actions">
                    <Link
                      className="button secondary small"
                      to={`/events/${item.eventId}`}
                    >
                      View event ↗
                    </Link>
                    {canCancel(item) && (
                      <button
                        className="text-button danger-text"
                        disabled={cancel.isPending}
                        onClick={() => {
                          cancel.reset();
                          setSelected(item);
                        }}
                      >
                        Cancel registration
                      </button>
                    )}
                  </div>
                </article>
              ))}
            </div>
          ) : (
            <Empty title="No registrations here yet">
              <p>Find an event that sparks your curiosity.</p>
              <Link className="button" to="/events">
                Explore events ↗
              </Link>
            </Empty>
          )}
          <Pagination {...query.data} onPage={setPage} />
        </>
      )}
      {selected && (
        <ConfirmDialog
          title="Cancel your registration?"
          busy={cancel.isPending}
          confirmLabel="Cancel registration"
          onClose={() => setSelected(null)}
          onConfirm={() => cancel.mutate(selected.eventId)}
        >
          <p>
            Your place at <strong>{selected.eventTitle}</strong> will be
            released.
          </p>
          <ErrorNotice error={cancel.error} />
        </ConfirmDialog>
      )}
    </>
  );
}
