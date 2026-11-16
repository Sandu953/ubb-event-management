import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../contexts/auth";
import { useOrganizations } from "../../hooks/queries";
import { useAction } from "../../hooks/useAction";
import { organizationService } from "../../services/organizationService";
import { isAdmin, uuidPattern } from "../../utils/domain";
import {
  Empty,
  ErrorNotice,
  Field,
  Loading,
  PageHeading,
  Pagination,
  Success,
} from "../../components/common/UI";
export default function OrganizationsPage() {
  const { current, refresh } = useAuth();
  const [page, setPage] = useState(1),
    [creating, setCreating] = useState(false);
  const [validation, setValidation] = useState("");
  const query = useOrganizations(page);
  const create = useAction(organizationService.create, () => {
    setCreating(false);
    refresh();
  });
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    if (
      !String(data.get("name")).trim() ||
      !String(data.get("description")).trim()
    ) {
      setValidation("Enter a name and description for the organization.");
      return;
    }
    setValidation("");
    create.mutate({
      name: String(data.get("name")).trim(),
      description: String(data.get("description")).trim(),
      initialAdminUserId: String(data.get("initialAdminUserId")).trim(),
    });
  }
  return (
    <>
      <PageHeading
        eyebrow={
          isAdmin(current) ? "SYSTEM ADMINISTRATION" : "YOUR ORGANIZATIONS"
        }
        title="Organizations"
        description={
          isAdmin(current)
            ? "Create organizations and help their teams get started."
            : "The teams you belong to and the roles you hold."
        }
      >
        {isAdmin(current) && (
          <button
            className="button"
            onClick={() => {
              create.reset();
              setCreating(!creating);
            }}
          >
            {creating ? "Close form" : "New organization ＋"}
          </button>
        )}
      </PageHeading>
      <ErrorNotice error={create.error} />
      {validation && (
        <p role="alert" className="notice error">
          {validation}
        </p>
      )}
      <Success>
        {create.isSuccess && (
          <>
            Organization created with its initial administrator.{" "}
            <Link to={`/organizations/${create.data.id}`}>
              Open organization ↗
            </Link>
          </>
        )}
      </Success>
      {creating && isAdmin(current) && (
        <section className="panel editor-panel">
          <h2>Create an organization</h2>
          <form className="stack" onSubmit={submit}>
            <Field
              label="Organization name"
              name="name"
              required
              maxLength={200}
            />
            <div className="field">
              <label htmlFor="org-description">Description *</label>
              <textarea
                id="org-description"
                name="description"
                rows={4}
                required
                maxLength={4000}
              />
            </div>
            <Field
              label="Initial administrator user ID"
              name="initialAdminUserId"
              pattern={uuidPattern}
              required
              hint="Use an existing user’s UUID from their profile. The API does not offer user search."
            />
            <button
              className="button"
              disabled={create.isPending}
              type="submit"
            >
              {create.isPending ? "Creating…" : "Create organization"}
            </button>
          </form>
        </section>
      )}
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
            <div className="organization-grid">
              {query.data.items.map((org) => (
                <Link
                  key={org.id}
                  className="panel organization-card"
                  to={`/organizations/${org.id}`}
                >
                  <span className="org-symbol" aria-hidden="true">
                    ◇
                  </span>
                  <h2>{org.name}</h2>
                  <p className="muted">{org.description}</p>
                  <span className="organization-link">Open organization ↗</span>
                </Link>
              ))}
            </div>
          ) : (
            <Empty title="No organizations yet">
              {isAdmin(current)
                ? "Create an organization and assign its initial administrator."
                : "Ask an organization administrator to add you using your profile’s user ID."}
            </Empty>
          )}
          <Pagination {...query.data} onPage={setPage} />
        </>
      )}
    </>
  );
}
