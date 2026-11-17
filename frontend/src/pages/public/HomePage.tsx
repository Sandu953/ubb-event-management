import { Link } from "react-router-dom";
import { useState } from "react";
import { useEvents } from "../../hooks/queries";
import EventCard from "../../components/events/EventCard";
import { Empty, ErrorNotice, Loading } from "../../components/common/UI";
import { timeZone } from "../../utils/dates";
export default function HomePage() {
  const [from] = useState(() => new Date().toISOString());
  const events = useEvents({ from, pageSize: 3 });
  return (
    <>
      <div className="home-intro">
        <p className="eyebrow">YOUR CAMPUS. YOUR COMMUNITY.</p>
        <span className="muted">A little curiosity goes a long way.</span>
      </div>
      <section className="hero">
        <div className="hero-copy">
          <span className="hero-kicker">
            <span /> Great things happen together
          </span>
          <h1>
            More than lectures.
            <br />
            <em>Make it an experience.</em>
          </h1>
          <p>
            Meet your people, explore new interests, and make your time at
            university count. Discover what’s happening in the UBB community.
          </p>
          <div className="actions">
            <Link className="button" to="/events">
              Explore events <span aria-hidden="true">↗</span>
            </Link>
            <Link className="hero-link" to="/register">
              Join the community →
            </Link>
          </div>
        </div>
        <div className="hero-art" aria-hidden="true">
          <div className="art-ring ring-one" />
          <div className="art-ring ring-two" />
          <div className="art-center">
            u<span>nited by curiosity.</span>
          </div>
          <span className="art-tag tag-one">Ideas worth sharing ✦</span>
          <span className="art-tag tag-two">People worth meeting ↗</span>
        </div>
      </section>
      <div className="feature-strip">
        <p>
          <span aria-hidden="true">✦</span>Discover something new
        </p>
        <p>
          <span aria-hidden="true">◎</span>Connect with your community
        </p>
        <p>
          <span aria-hidden="true">↗</span>Show up. Get involved.
        </p>
      </div>
      <section>
        <header className="section-heading">
          <div>
            <p className="eyebrow">MAKE ROOM IN YOUR CALENDAR</p>
            <h2>Coming up on campus</h2>
            <p className="muted">
              Your next experience is just around the corner. Times in{" "}
              {timeZone}.
            </p>
          </div>
          <Link className="button secondary" to="/events">
            See all events →
          </Link>
        </header>
        {events.isPending ? (
          <Loading />
        ) : events.isError ? (
          <ErrorNotice
            error={events.error}
            retry={() => {
              void events.refetch();
            }}
          />
        ) : events.data.items.length ? (
          <div className="event-grid">
            {events.data.items.map((event) => (
              <EventCard event={event} key={event.id} />
            ))}
          </div>
        ) : (
          <Empty title="Good things are on the way">
            There are no upcoming events yet. Check back soon for new
            experiences.
          </Empty>
        )}
      </section>
      <section className="callout">
        <div>
          <p className="eyebrow">BRING YOUR IDEAS TO LIFE</p>
          <h2>Part of a student organization?</h2>
          <p>
            Create events, bring people together, and manage every detail in one
            place.
          </p>
        </div>
        <Link to="/dashboard" className="button secondary">
          Open your workspace ↗
        </Link>
      </section>
    </>
  );
}
