import { Link, useParams } from "react-router-dom";
import { useEvent } from "../../hooks/queries";
import { useAuth } from "../../contexts/auth";
import {
  Badge,
  ErrorNotice,
  Loading,
  PageHeading,
} from "../../components/common/UI";
import { canManage } from "../../utils/domain";
import { formatDate, timeZone } from "../../utils/dates";
import RegistrationPanel from "../../components/registrations/RegistrationPanel";
export default function EventDetailsPage() {
  const { id = "" } = useParams();
  const query = useEvent(id);
  const { current } = useAuth();
  if (query.isPending) return <Loading />;
  if (query.isError)
    return (
      <ErrorNotice
        error={query.error}
        retry={() => {
          void query.refetch();
        }}
      />
    );
  const event = query.data;
  const organization = current?.memberships.find(
    (m) => m.organizationId === event.organizationId,
  );
  return (
    <>
      <Link className="back-link" to="/events">
        ← Explore events
      </Link>
      <PageHeading eyebrow={event.type} title={event.title}>
        <Badge value={event.status} />
        {canManage(current, event.organizationId) && (
          <Link className="button secondary" to={`/manage/events/${id}`}>
            Manage event ↗
          </Link>
        )}
      </PageHeading>
      <div className="detail-layout">
        <div className="stack">
          <div className="detail-banner" aria-hidden="true">
            <span>Made for connection.</span>
            <strong>✦</strong>
          </div>
          <section className="panel">
            <h2>About this event</h2>
            <p className="preserve-lines">{event.description}</p>
          </section>
          <section className="panel">
            <h2>The details</h2>
            <dl className="details-list">
              <div>
                <dt>Starts</dt>
                <dd>
                  <time dateTime={event.startTime}>
                    {formatDate(event.startTime)}
                  </time>
                </dd>
              </div>
              <div>
                <dt>Ends</dt>
                <dd>
                  <time dateTime={event.endTime}>
                    {formatDate(event.endTime)}
                  </time>
                </dd>
              </div>
              <div>
                <dt>Timezone</dt>
                <dd>{timeZone}</dd>
              </div>
              <div>
                <dt>Venue</dt>
                <dd>{event.venue}</dd>
              </div>
              <div>
                <dt>Total capacity</dt>
                <dd>{event.maxCapacity} participants</dd>
              </div>
              <div>
                <dt>Organization</dt>
                <dd>
                  {organization?.organizationName || (
                    <>
                      <span className="muted">Organization ID</span>
                      <br />
                      <code>{event.organizationId}</code>
                    </>
                  )}
                </dd>
              </div>
            </dl>
            {!organization && (
              <small className="muted">
                Public event responses include an organization ID. Public
                organization names are not yet supplied by the API.
              </small>
            )}
          </section>
        </div>
        <RegistrationPanel event={event} />
      </div>
    </>
  );
}
