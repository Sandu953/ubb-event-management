import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/auth";
import { Badge, Empty, PageHeading } from "../../components/common/UI";
export default function ProfilePage() {
  const { current, logout } = useAuth();
  if (!current) return null;
  return (
    <>
      <PageHeading
        eyebrow="YOUR ACCOUNT"
        title="My profile"
        description="Your account and organization roles, as reported by the server."
      />
      <section className="panel">
        <div className="actions">
          <span className="avatar large">{current.user.fullName[0]}</span>
          <div>
            <h2>{current.user.fullName}</h2>
            <p className="muted">{current.user.email}</p>
          </div>
          <Badge value={current.user.globalRole} />
        </div>
        <dl className="details-list">
          <div>
            <dt>Your user ID</dt>
            <dd>
              <code>{current.user.id}</code>
            </dd>
          </div>
        </dl>
        <p className="muted">
          Share this ID with your organization administrator when requesting
          membership. Profile and password editing are not currently available
          through the API.
        </p>
        <button className="button secondary" onClick={logout}>
          Sign out
        </button>
      </section>
      <header className="section-heading">
        <h2>My organizations</h2>
      </header>
      {current.memberships.length ? (
        <div className="organization-grid">
          {current.memberships.map((m) => (
            <Link
              key={m.organizationId}
              className="panel organization-card"
              to={`/organizations/${m.organizationId}`}
            >
              <Badge value={m.orgRole} />
              <h2>{m.organizationName}</h2>
              <span>Open organization ↗</span>
            </Link>
          ))}
        </div>
      ) : (
        <Empty title="No organization memberships">
          You can still discover and register for events. Contact an
          organization administrator to join their team.
        </Empty>
      )}
    </>
  );
}
