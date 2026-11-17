import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useEvent } from "../../hooks/queries";
import { useAction } from "../../hooks/useAction";
import { registrationService } from "../../services/registrationService";
import type { Participant } from "../../types/api";
import { canManage } from "../../utils/domain";
import {
  Badge,
  ConfirmDialog,
  Empty,
  ErrorNotice,
  Field,
  Loading,
  PageHeading,
  Pagination,
  Select,
  Stats,
  Success,
} from "../../components/common/UI";
import { formatDate } from "../../utils/dates";
export default function ParticipantsPage({
  attendanceView = false,
}: {
  attendanceView?: boolean;
}) {
  const { id = "" } = useParams();
  const { current } = useAuth();
  const event = useEvent(id);
  const allowed = !!event.data && canManage(current, event.data.organizationId);
  const [page, setPage] = useState(1),
    [status, setStatus] = useState(""),
    [search, setSearch] = useState("");
  const [selected, setSelected] = useState<Participant | null>(null);
  const participants = useQuery({
    queryKey: ["participants", current?.user.id, id, page, status],
    queryFn: ({ signal }) =>
      registrationService.participants(id, page, status, signal),
    enabled: allowed,
  });
  const totals = useQuery({
    queryKey: ["attendance", current?.user.id, id],
    queryFn: ({ signal }) => registrationService.attendance(id, signal),
    enabled: allowed,
  });
  const checkIn = useAction(
    (registrationId: string) => registrationService.checkIn(id, registrationId),
    () => setSelected(null),
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
        Only this organization’s staff can view participant information.
      </Empty>
    );
  const filtered =
    participants.data?.items.filter((p) =>
      `${p.fullName} ${p.email}`.toLowerCase().includes(search.toLowerCase()),
    ) || [];
  return (
    <>
      <Link className="back-link" to={`/manage/events/${id}`}>
        ← Event workspace
      </Link>
      <PageHeading
        eyebrow={event.data.title}
        title={attendanceView ? "Attendance" : "Event registrations"}
        description="Manage confirmed participants and the waiting list."
      >
        <button
          className="button secondary"
          disabled={participants.isFetching || totals.isFetching}
          onClick={() => {
            void participants.refetch();
            void totals.refetch();
            void event.refetch();
          }}
        >
          Refresh participants
        </button>
      </PageHeading>
      <ErrorNotice
        error={totals.error}
        retry={() => {
          void totals.refetch();
        }}
      />
      <Stats
        items={[
          { label: "Confirmed", value: totals.data?.confirmed ?? "—" },
          { label: "Waitlisted", value: totals.data?.waitlisted ?? "—" },
          { label: "Canceled", value: totals.data?.canceled ?? "—" },
          { label: "Checked in", value: totals.data?.checkedIn ?? "—" },
        ]}
      />
      <div className="panel toolbar">
        <Select
          label="Registration status"
          value={status}
          onChange={(e) => {
            setStatus(e.target.value);
            setPage(1);
          }}
        >
          <option value="">All statuses</option>
          <option value="CONFIRMED">Confirmed</option>
          <option value="WAITLISTED">Waitlisted</option>
          <option value="CANCELED">Canceled</option>
        </Select>
        <Field
          label="Search this page"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder="Name or email"
          hint="Searches the participants on the current page only."
        />
      </div>
      {event.data.status !== "REGISTRATION_OPEN" && (
        <p className="notice">
          Check-in is only available while this event’s registration is open.
        </p>
      )}
      <ErrorNotice error={checkIn.error} />
      <Success>
        {checkIn.isSuccess && "Participant checked in successfully."}
      </Success>
      {participants.isPending ? (
        <Loading />
      ) : participants.isError ? (
        <ErrorNotice
          error={participants.error}
          retry={() => {
            void participants.refetch();
          }}
        />
      ) : (
        <>
          {filtered.length ? (
            <div className="panel table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Participant</th>
                    <th>Registered</th>
                    <th>Status</th>
                    <th>Attendance</th>
                    <th>Action</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((p) => (
                    <tr key={p.registrationId}>
                      <td>
                        <strong>{p.fullName}</strong>
                        <small>{p.email}</small>
                      </td>
                      <td>{formatDate(p.registeredAt)}</td>
                      <td>
                        <Badge value={p.status} />
                      </td>
                      <td>{p.checkedIn ? "✓ Checked in" : "Not checked in"}</td>
                      <td>
                        {p.status === "CONFIRMED" &&
                          !p.checkedIn &&
                          event.data.status === "REGISTRATION_OPEN" && (
                            <button
                              className="button small"
                              disabled={checkIn.isPending}
                              onClick={() => {
                                checkIn.reset();
                                setSelected(p);
                              }}
                            >
                              Check in
                            </button>
                          )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty
              title={search ? "No matches on this page" : "No participants yet"}
            >
              Participants appear here after they register.
            </Empty>
          )}
          <Pagination {...participants.data} onPage={setPage} />
        </>
      )}
      {selected && (
        <ConfirmDialog
          title="Check in participant?"
          confirmLabel="Check in"
          busy={checkIn.isPending}
          onClose={() => setSelected(null)}
          onConfirm={() => checkIn.mutate(selected.registrationId)}
        >
          <p>
            Mark <strong>{selected.fullName}</strong> as present. Check-in
            cannot be undone through the current API.
          </p>
          <ErrorNotice error={checkIn.error} />
        </ConfirmDialog>
      )}
    </>
  );
}
