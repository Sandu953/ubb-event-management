import { useState, type FormEvent } from "react";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useAction } from "../../hooks/useAction";
import { organizationService } from "../../services/organizationService";
import type { Member, OrgRole } from "../../types/api";
import { canManageMembers, isAdmin, uuidPattern } from "../../utils/domain";
import {
  Badge,
  ConfirmDialog,
  Empty,
  ErrorNotice,
  Field,
  Loading,
  Pagination,
  Select,
  Success,
} from "../common/UI";
type MemberAction = { member: Member; role?: OrgRole };
export default function MembersPanel({
  organizationId,
}: {
  organizationId: string;
}) {
  const { current, refresh } = useAuth();
  const [page, setPage] = useState(1),
    [adding, setAdding] = useState(false);
  const [selected, setSelected] = useState<MemberAction | null>(null);
  const [formKey, setFormKey] = useState(0);
  const query = useQuery({
    queryKey: ["members", current?.user.id, organizationId, page],
    queryFn: ({ signal }) =>
      organizationService.members(organizationId, page, signal),
  });
  const manager = canManageMembers(current, organizationId),
    admin = isAdmin(current);
  const add = useAction(
    ({ userId, orgRole }: { userId: string; orgRole: OrgRole }) =>
      organizationService.addMember(organizationId, userId, orgRole),
    () => {
      setFormKey((k) => k + 1);
      setAdding(false);
      refresh();
    },
  );
  const change = useAction(
    ({ member, role }: MemberAction) =>
      role
        ? organizationService.updateMember(organizationId, member.userId, role)
        : organizationService.removeMember(organizationId, member.userId),
    () => {
      setSelected(null);
      refresh();
    },
  );
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    add.mutate({
      userId: String(data.get("userId")).trim(),
      orgRole:
        admin && data.get("orgRole") === "ORG_ADMIN"
          ? "ORG_ADMIN"
          : "ORGANIZER",
    });
  }
  return (
    <section>
      <header className="section-heading">
        <div>
          <p className="eyebrow">THE PEOPLE BEHIND IT</p>
          <h2>Organization members</h2>
        </div>
        {manager && (
          <button
            className="button secondary"
            disabled={add.isPending || change.isPending}
            onClick={() => {
              add.reset();
              setAdding(!adding);
            }}
          >
            {adding ? "Close form" : "Add member ＋"}
          </button>
        )}
      </header>
      <ErrorNotice error={add.error || change.error} />
      <Success>{add.isSuccess && "Member added successfully."}</Success>
      <Success>{change.isSuccess && "Membership updated."}</Success>
      {adding && manager && (
        <form key={formKey} className="panel toolbar" onSubmit={submit}>
          <Field
            label="Existing user ID"
            name="userId"
            required
            pattern={uuidPattern}
            hint="Ask the user to share the UUID shown on their profile."
          />
          <Select
            label="Organization role"
            name="orgRole"
            defaultValue="ORGANIZER"
          >
            <option value="ORGANIZER">Organizer</option>
            {admin && <option value="ORG_ADMIN">Organization admin</option>}
          </Select>
          <button
            className="button"
            disabled={add.isPending || change.isPending}
            type="submit"
          >
            {add.isPending ? "Adding…" : "Add member"}
          </button>
        </form>
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
            <div className="panel table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Member</th>
                    <th>Organization role</th>
                    {manager && <th>Actions</th>}
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((member) => (
                    <tr key={member.userId}>
                      <td>
                        <strong>{member.fullName}</strong>
                        <small>{member.email}</small>
                        <code className="small">{member.userId}</code>
                      </td>
                      <td>
                        <Badge value={member.orgRole} />
                      </td>
                      {manager && (
                        <td>
                          <div className="actions">
                            {admin && (
                              <button
                                className="text-button"
                                disabled={change.isPending || add.isPending}
                                onClick={() => {
                                  change.reset();
                                  setSelected({
                                    member,
                                    role:
                                      member.orgRole === "ORG_ADMIN"
                                        ? "ORGANIZER"
                                        : "ORG_ADMIN",
                                  });
                                }}
                              >
                                {member.orgRole === "ORG_ADMIN"
                                  ? "Make organizer"
                                  : "Make org admin"}
                              </button>
                            )}
                            {(admin || member.orgRole === "ORGANIZER") && (
                              <button
                                className="text-button danger-text"
                                disabled={change.isPending || add.isPending}
                                onClick={() => {
                                  change.reset();
                                  setSelected({ member });
                                }}
                              >
                                Remove
                              </button>
                            )}
                          </div>
                        </td>
                      )}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <Empty title="No members to display">
              Refresh to retrieve current membership information.
            </Empty>
          )}
          <Pagination {...query.data} onPage={setPage} />
        </>
      )}
      {selected && (
        <ConfirmDialog
          title={selected.role ? "Change organization role?" : "Remove member?"}
          confirmLabel={selected.role ? "Update role" : "Remove member"}
          busy={change.isPending}
          onClose={() => setSelected(null)}
          onConfirm={() => change.mutate(selected)}
        >
          <p>
            {selected.role
              ? `Change ${selected.member.fullName} to ${selected.role === "ORG_ADMIN" ? "organization administrator" : "organizer"} in this organization?`
              : `${selected.member.fullName} will lose their staff access to this organization.`}{" "}
            The organization must retain at least one administrator.
          </p>
          <ErrorNotice error={change.error} />
        </ConfirmDialog>
      )}
    </section>
  );
}
