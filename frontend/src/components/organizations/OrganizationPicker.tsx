import { useOrganizationOptions } from "../../hooks/useOrganizationOptions";
import { ErrorNotice, Select } from "../common/UI";
export default function OrganizationPicker({
  value,
  onChange,
  required = false,
}: {
  value: string;
  onChange: (id: string) => void;
  required?: boolean;
}) {
  const { options, query, admin } = useOrganizationOptions();
  return (
    <div>
      <Select
        label="Organization workspace"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        required={required}
      >
        <option value="">Choose an organization</option>
        {value && !options.some((o) => o.id === value) && (
          <option value={value}>{value}</option>
        )}
        {options.map((o) => (
          <option key={o.id} value={o.id}>
            {o.name}
          </option>
        ))}
      </Select>
      {admin && query.isPending && <p role="status">Loading organizations…</p>}
      {admin && (
        <ErrorNotice
          error={query.error}
          retry={() => {
            void query.refetch();
          }}
        />
      )}
      {query.hasNextPage && (
        <button
          type="button"
          className="text-button"
          disabled={query.isFetchingNextPage}
          onClick={() => {
            void query.fetchNextPage();
          }}
        >
          Load more organizations
        </button>
      )}
    </div>
  );
}
