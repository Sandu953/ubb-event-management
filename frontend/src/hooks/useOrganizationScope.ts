import { useSearchParams } from "react-router-dom";
import { useAuth } from "../contexts/auth";
import { canManage } from "../utils/domain";
export function useOrganizationScope() {
  const { current } = useAuth();
  const [params, setParams] = useSearchParams();
  const id =
    params.get("organizationId") ??
    current?.memberships[0]?.organizationId ??
    "";
  const allowed = !!id && canManage(current, id);
  const select = (organizationId: string) => {
    const next = new URLSearchParams(params);
    next.set("organizationId", organizationId);
    next.delete("page");
    setParams(next);
  };
  return { id, allowed, select, params, setParams };
}
