import { Link } from "react-router-dom";
import type { Event } from "../../types/api";
import { useAuth } from "../../contexts/auth";
import { formatDate } from "../../utils/dates";
import { Badge } from "../common/UI";
export default function EventCard({ event }: { event: Event }) {
  const { current } = useAuth();
  const organization = current?.memberships.find(
    (item) => item.organizationId === event.organizationId,
  )?.organizationName;
  const day = new Date(event.startTime);
  return (
    <article className="event-card">
      <div className="event-cover" aria-hidden="true">
        <div className="event-orbit" />
        <span className="cover-type">{event.type}</span>
        <span className="date-tile">
          <small>{day.toLocaleDateString(undefined, { month: "short" })}</small>
          {day.getDate()}
        </span>
      </div>
      <div className="event-card-body">
        <div className="card-meta">
          <span>{event.type}</span>
          <Badge value={event.status} />
        </div>
        <h2>
          <Link to={`/events/${event.id}`}>{event.title}</Link>
        </h2>
        {organization && <p className="muted">{organization}</p>}
        <p className="event-description">{event.description}</p>
        <div className="event-facts">
          <p>
            <span aria-hidden="true">◷</span>
            <time dateTime={event.startTime}>
              {formatDate(event.startTime)}
            </time>
          </p>
          <p>
            <span aria-hidden="true">⌖</span>
            {event.venue}
          </p>
        </div>
        <div className="card-bottom">
          <span>{event.maxCapacity} capacity</span>
          <Link to={`/events/${event.id}`}>
            View event <span aria-hidden="true">↗</span>
          </Link>
        </div>
      </div>
    </article>
  );
}
