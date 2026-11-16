import { Link, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { organizationService } from "../../services/organizationService";
import { canManage } from "../../utils/domain";
import {
  Empty,
  ErrorNotice,
  Loading,
  PageHeading,
} from "../../components/common/UI";
import MembersPanel from "../../components/organizations/MembersPanel";
export default function OrganizationDetailsPage() {
  const { id = "" } = useParams();
  const { current } = useAuth();
  const allowed = canManage(current, id);
  const query = useQuery({
    queryKey: ["organizations", current?.user.id, id],
    queryFn: ({ signal }) => organizationService.get(id, signal),
    enabled: allowed,
  });
  if (!allowed)
    return (
      <Empty title="Access denied">
        You do not have a staff role in this organization.
      </Empty>
    );
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
  return (
    <>
      <Link className="back-link" to="/organizations">
        ← Organizations
      </Link>
      <PageHeading eyebrow="ORGANIZATION WORKSPACE" title={query.data.name}>
        <Link className="button" to={`/manage/events?organizationId=${id}`}>
          Organization events ↗
        </Link>
      </PageHeading>
      <section className="panel">
        <p className="preserve-lines">{query.data.description}</p>
        <p className="small muted">
          Organization ID: <code>{id}</code>
        </p>
        <p className="small muted">
          Organization details are read-only. The API supports creation and
          membership administration, but does not expose organization editing.
        </p>
      </section>
      <MembersPanel key={id} organizationId={id} />
    </>
  );
}
