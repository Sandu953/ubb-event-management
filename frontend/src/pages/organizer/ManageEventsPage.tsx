import { Link } from "react-router-dom";
import type { FormEvent } from "react";
import { useOrganizationScope } from "../../hooks/useOrganizationScope";
import { useEvents } from "../../hooks/queries";
import OrganizationPicker from "../../components/organizations/OrganizationPicker";
import {
  Badge,
  Empty,
  ErrorNotice,
  Field,
  Loading,
  PageHeading,
  Pagination,
  Select,
} from "../../components/common/UI";
import { eventStatuses, label } from "../../utils/domain";
import { formatDate } from "../../utils/dates";
export default function ManageEventsPage() {
  const scope = useOrganizationScope();
  const page = Math.max(1, Number(scope.params.get("page")) || 1);
  const query = useEvents(
    {
      organizationId: scope.id,
      includeDrafts: true,
      page,
      pageSize: 12,
      search: scope.params.get("search") || "",
      status: scope.params.get("status") || "",
    },
    scope.allowed,
  );
  function filter(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const next = new URLSearchParams(scope.params);
    next.set("search", String(data.get("search")).trim());
    next.set("status", String(data.get("status")));
    next.delete("page");
    scope.setParams(next);
  }
  return (
    <>
      <PageHeading
        eyebrow="PLAN. PUBLISH. BRING PEOPLE TOGETHER."
        title="Manage events"
        description="Manage drafts, registrations, and attendance in your organization."
      >
        <Link
          className="button"
          to={`/manage/events/new?organizationId=${scope.id}`}
        >
          Create event ＋
        </Link>
      </PageHeading>
      <div className="panel">
        <OrganizationPicker value={scope.id} onChange={scope.select} />
        <form
          className="toolbar"
          key={scope.params.toString()}
          onSubmit={filter}
        >
          <Field
            label="Search organization events"
            name="search"
            maxLength={200}
            defaultValue={scope.params.get("search") || ""}
          />
          <Select
            label="Lifecycle status"
            name="status"
            defaultValue={scope.params.get("status") || ""}
          >
            <option value="">All stages</option>
            {eventStatuses.map((s) => (
              <option key={s} value={s}>
                {label(s)}
              </option>
            ))}
          </Select>
          <button className="button secondary" type="submit">
            Apply filters
          </button>
        </form>
      </div>
      {!scope.allowed ? (
        <Empty title="Choose an authorized organization">
          Select your workspace to see its events.
        </Empty>
      ) : query.isPending ? (
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
            <div className="panel table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Event</th>
                    <th>Start time</th>
                    <th>Capacity</th>
                    <th>Status</th>
                    <th>Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((e) => (
                    <tr key={e.id}>
                      <td>
                        <Link to={`/manage/events/${e.id}`}>
                          <strong>{e.title}</strong>
                        </Link>
                        <small>
                          {e.type} · {e.venue}
                        </small>
                      </td>
                      <td>{formatDate(e.startTime)}</td>
                      <td>{e.maxCapacity}</td>
                      <td>
                        <Badge value={e.status} />
                      </td>
                      <td>
                        <div className="actions">
                          <Link to={`/manage/events/${e.id}`}>Manage</Link>
                          <Link to={`/manage/events/${e.id}/attendance`}>
                            Attendance ↗
                          </Link>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty title="No events match this view">
              Create an event or adjust your filters.
            </Empty>
          )}
          <Pagination
            {...query.data}
            onPage={(next) => {
              const params = new URLSearchParams(scope.params);
              params.set("page", String(next));
              scope.setParams(params);
            }}
          />
        </>
      )}
    </>
  );
}
