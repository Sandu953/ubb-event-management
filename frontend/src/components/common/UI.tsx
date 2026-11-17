import {
  useEffect,
  useId,
  useRef,
  type ReactNode,
  type InputHTMLAttributes,
  type SelectHTMLAttributes,
} from "react";
import { ApiError } from "../../services/api";
import { label } from "../../utils/domain";

export function Badge({ value }: { value: string }) {
  return (
    <span className={`badge badge-${value.toLowerCase()}`}>{label(value)}</span>
  );
}
export function PageHeading({
  eyebrow,
  title,
  description,
  children,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  children?: ReactNode;
}) {
  return (
    <header className="page-heading">
      <div>
        {eyebrow && <p className="eyebrow">{eyebrow}</p>}
        <h1>{title}</h1>
        {description && <p className="muted">{description}</p>}
      </div>
      <div className="actions">{children}</div>
    </header>
  );
}
export function ErrorNotice({
  error,
  retry,
}: {
  error: Error | null;
  retry?: () => void;
}) {
  if (!error) return null;
  const title =
    error instanceof ApiError
      ? {
          0: "Connection unavailable",
          401: "Sign in required",
          403: "Access denied",
          404: "Not found",
          409: "Unable to save this change",
        }[error.status] || "Something needs your attention"
      : "Something needs your attention";
  return (
    <div className="notice error" role="alert">
      <strong>{title}</strong>
      <p>{error.message}</p>
      {retry && (
        <button className="button small secondary" onClick={retry}>
          Refresh and try again
        </button>
      )}
    </div>
  );
}
export function Success({ children }: { children?: ReactNode }) {
  return children ? (
    <div className="notice success" role="status">
      {children}
    </div>
  ) : null;
}
export function Loading() {
  return (
    <div role="status" aria-live="polite" className="loading">
      <span className="spinner" />
      Loading…
      <div className="skeleton" />
      <div className="skeleton" />
    </div>
  );
}
export function Empty({
  title,
  children,
}: {
  title: string;
  children?: ReactNode;
}) {
  return (
    <div className="empty">
      <span className="empty-icon" aria-hidden="true">
        ◇
      </span>
      <h2>{title}</h2>
      <div className="muted">{children}</div>
    </div>
  );
}
export function Field({
  label: text,
  hint,
  ...props
}: InputHTMLAttributes<HTMLInputElement> & { label: string; hint?: string }) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>
        {text}
        {props.required && <span aria-hidden="true"> *</span>}
      </label>
      <input
        id={id}
        aria-describedby={hint ? `${id}-hint` : undefined}
        {...props}
      />
      {hint && <small id={`${id}-hint`}>{hint}</small>}
    </div>
  );
}
export function Select({
  label: text,
  children,
  ...props
}: SelectHTMLAttributes<HTMLSelectElement> & { label: string }) {
  const id = useId();
  return (
    <div className="field">
      <label htmlFor={id}>{text}</label>
      <select id={id} {...props}>
        {children}
      </select>
    </div>
  );
}
export function Pagination({
  page,
  pageSize,
  totalCount,
  onPage,
}: {
  page: number;
  pageSize: number;
  totalCount: number;
  onPage: (page: number) => void;
}) {
  const pages = Math.max(1, Math.ceil(totalCount / pageSize));
  return (
    <nav aria-label="Pagination" className="pagination">
      <span>
        {totalCount} {totalCount === 1 ? "result" : "results"} · Page {page} of{" "}
        {pages}
      </span>
      <div className="actions">
        <button
          className="button secondary small"
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Previous
        </button>
        <button
          className="button secondary small"
          disabled={page >= pages}
          onClick={() => onPage(page + 1)}
        >
          Next
        </button>
      </div>
    </nav>
  );
}
export function ConfirmDialog({
  title,
  children,
  confirmLabel = "Confirm",
  busy,
  onConfirm,
  onClose,
}: {
  title: string;
  children: ReactNode;
  confirmLabel?: string;
  busy: boolean;
  onConfirm: () => void;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const id = useId();
  useEffect(() => {
    const dialog = ref.current;
    const previous = document.activeElement;
    dialog?.showModal();
    return () => {
      dialog?.close();
      if (previous instanceof HTMLElement && previous.isConnected)
        previous.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      aria-labelledby={id}
      onCancel={(event) => {
        event.preventDefault();
        if (!busy) onClose();
      }}
    >
      <h2 id={id}>{title}</h2>
      <div className="dialog-body">{children}</div>
      <div className="actions">
        <button
          autoFocus
          className="button secondary"
          disabled={busy}
          onClick={onClose}
        >
          Keep current state
        </button>
        <button className="button" disabled={busy} onClick={onConfirm}>
          {busy ? "Saving…" : confirmLabel}
        </button>
      </div>
    </dialog>
  );
}
export function Stats({
  items,
}: {
  items: { label: string; value: number | string }[];
}) {
  return (
    <div className="stats">
      {items.map((item) => (
        <div className="stat" key={item.label}>
          <span className="muted">{item.label}</span>
          <strong>{item.value}</strong>
        </div>
      ))}
    </div>
  );
}
