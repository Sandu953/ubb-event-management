import { useInfiniteQuery } from "@tanstack/react-query";
import { useAuth } from "../contexts/auth";
import { organizationService } from "../services/organizationService";
import { isAdmin } from "../utils/domain";
export function useOrganizationOptions() {
  const { current } = useAuth();
  const admin = isAdmin(current);
  const query = useInfiniteQuery({
    queryKey: ["organizations", current?.user.id, "options"],
    initialPageParam: 1,
    queryFn: ({ pageParam, signal }) =>
      organizationService.list(pageParam, signal),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.totalCount ? last.page + 1 : undefined,
    enabled: admin,
  });
  const options = admin
    ? query.data?.pages.flatMap((p) =>
        p.items.map((o) => ({ id: o.id, name: o.name })),
      ) || []
    : current?.memberships.map((m) => ({
        id: m.organizationId,
        name: m.organizationName,
      })) || [];
  return { options, query, admin };
}
