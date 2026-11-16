import { useMutation, useQueryClient } from "@tanstack/react-query";
export function useAction<TData, TVariables>(
  mutationFn: (variables: TVariables) => Promise<TData>,
  onSuccess?: (data: TData) => void,
) {
  const client = useQueryClient();
  return useMutation({
    mutationFn,
    onSuccess: async (data) => {
      onSuccess?.(data);
      await client.invalidateQueries({
        predicate: (query) => query.queryKey[0] !== "auth",
      });
    },
  });
}
