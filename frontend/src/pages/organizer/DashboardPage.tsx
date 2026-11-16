import { useState } from "react";
import { Link } from "react-router-dom";
import { useQueries } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useOrganizationScope } from "../../hooks/useOrganizationScope";
import { useEvents } from "../../hooks/queries";
import { eventService } from "../../services/eventService";
import OrganizationPicker from "../../components/organizations/OrganizationPicker";
import {
  Badge,
  Empty,
  ErrorNotice,
  Loading,
  PageHeading,
  Stats,
} from "../../components/common/UI";
import { isAdmin } from "../../utils/domain";
import { formatDate } from "../../utils/dates";
export default function DashboardPage() {
  const { current } = useAuth();
  const scope = useOrganizationScope();
  const [from] = useState(() => new Date().toISOString());
  const definitions = [
    { label: "All managed events", status: "" },
    { label: "Drafts", status: "DRAFT" },
    { label: "Published", status: "PUBLISHED" },
    { label: "Registration open", status: "REGISTRATION_OPEN" },
  ];
  const counts = useQueries({
    queries: definitions.map((item) => ({
      queryKey: ["events", current?.user.id, "count", scope.id, item.status],
      queryFn: ({ signal }: { signal: AbortSignal }) =>
        eventService.list(
          {
            organizationId: scope.id,
            includeDrafts: true,
            status: item.status,
            pageSize: 1,
          },
          signal,
        ),
      enabled: scope.allowed,
    })),
  });
  const upcoming = useEvents(
    { organizationId: scope.id, includeDrafts: true, from, pageSize: 5 },
    scope.allowed,
  );
  return (
    <>
      <PageHeading
        eyebrow={
          isAdmin(current)
            ? "ADMINISTRATION WORKSPACE"
            : "ORGANIZATION WORKSPACE"
        }
        title={`Welcome, ${current?.user.fullName.split(" ")[0] || "organizer"}.`}
        description="Bring people together. Keep every detail in view."
      >
        <Link
          className="button"
          to={`/manage/events/new${scope.id ? `?organizationId=${scope.id}` : ""}`}
        >
          Create event ＋
        </Link>
      </PageHeading>
      <section className="panel toolbar">
        <OrganizationPicker value={scope.id} onChange={scope.select} />
        <p className="muted">
          Your workspace is scoped to one organization. Membership permissions
          are verified by the API.
        </p>
      </section>
      {!scope.allowed ? (
        <Empty
          title={
            scope.id
              ? "Organization access unavailable"
              : "Choose an organization"
          }
        >
          {isAdmin(current) ? (
            <Link to="/organizations">
              Manage or create an organization to get started.
            </Link>
          ) : (
            "Choose one of your organizations to see its events."
          )}
        </Empty>
      ) : (
        <>
          <ErrorNotice error={counts.find((q) => q.error)?.error || null} />
          <Stats
            items={definitions.map((item, index) => ({
              label: item.label,
              value: counts[index].data?.totalCount ?? "—",
            }))}
          />
          <header className="section-heading">
            <div>
              <p className="eyebrow">ON YOUR RADAR</p>
              <h2>Upcoming organization events</h2>
            </div>
            <Link
              className="button secondary"
              to={`/manage/events?organizationId=${scope.id}`}
            >
              Manage all events →
            </Link>
          </header>
          {upcoming.isPending ? (
            <Loading />
          ) : upcoming.isError ? (
            <ErrorNotice
              error={upcoming.error}
              retry={() => {
                void upcoming.refetch();
              }}
            />
          ) : upcoming.data.items.length ? (
            <div className="panel table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Event</th>
                    <th>Starts</th>
                    <th>Status</th>
                    <th>
                      <span className="sr-only">Action</span>
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {upcoming.data.items.map((e) => (
                    <tr key={e.id}>
                      <td>
                        <strong>{e.title}</strong>
                        <small>{e.venue}</small>
                      </td>
                      <td>{formatDate(e.startTime)}</td>
                      <td>
                        <Badge value={e.status} />
                      </td>
                      <td>
                        <Link to={`/manage/events/${e.id}`}>Manage ↗</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty title="Your next event starts with an idea">
              Create a draft and get your community involved.
            </Empty>
          )}
          <div className="callout">
            <div>
              <h2>Your organization, connected</h2>
              <p>View members and organization information in one place.</p>
            </div>
            <Link
              className="button secondary"
              to={`/organizations/${scope.id}`}
            >
              Open organization ↗
            </Link>
          </div>
        </>
      )}
    </>
  );
}
