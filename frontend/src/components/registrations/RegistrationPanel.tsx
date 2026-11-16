import { useState } from "react";
import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { useAction } from "../../hooks/useAction";
import { registrationService } from "../../services/registrationService";
import type { Event } from "../../types/api";
import { canCancel } from "../../utils/domain";
import {
  Badge,
  ConfirmDialog,
  ErrorNotice,
  Loading,
  Success,
} from "../common/UI";
export default function RegistrationPanel({ event }: { event: Event }) {
  const { current, signedIn, loading, error: authError, refresh } = useAuth();
  const [confirm, setConfirm] = useState(false);
  const mine = useQuery({
    queryKey: ["registrations", current?.user.id, "event", event.id],
    queryFn: ({ signal }) => registrationService.forEvent(event.id, signal),
    enabled: !!current,
  });
  const registration = useAction(() => registrationService.register(event.id));
  const cancel = useAction(
    () => registrationService.cancel(event.id),
    () => setConfirm(false),
  );
  const active = mine.data && mine.data.status !== "CANCELED";
  return (
    <section className="panel registration-panel">
      <p className="eyebrow">BE PART OF IT</p>
      <h2>Your place at this event</h2>
      {!signedIn ? (
        <>
          <p>Sign in to register and keep track of your place.</p>
          <Link
            className="button full"
            to={`/login?redirect=${encodeURIComponent(`/events/${event.id}`)}`}
          >
            Sign in to register ↗
          </Link>
        </>
      ) : authError ? (
        <ErrorNotice error={authError} retry={refresh} />
      ) : loading || mine.isPending ? (
        <Loading />
      ) : mine.isError ? (
        <ErrorNotice
          error={mine.error}
          retry={() => {
            void mine.refetch();
          }}
        />
      ) : (
        <>
          <ErrorNotice error={registration.error || cancel.error} />
          <Success>
            {registration.isSuccess &&
              (registration.data.status === "WAITLISTED"
                ? "You’re on the waiting list. The event is full; you’ll be promoted automatically if a place opens. Refresh to check your status."
                : "Your registration is confirmed. We look forward to seeing you!")}
          </Success>
          <Success>
            {cancel.isSuccess && "Your registration has been canceled."}
          </Success>
          {mine.data && (
            <div className="stack compact">
              <Badge value={mine.data.status} />
              {mine.data.checkedIn && <p>✓ Checked in</p>}
              {active && mine.data.status === "WAITLISTED" && (
                <p>
                  The event is full. You’ll be promoted automatically when a
                  place opens.
                </p>
              )}
            </div>
          )}
          {active && mine.data ? (
            canCancel(mine.data) && (
              <button
                className="button secondary full"
                disabled={cancel.isPending || registration.isPending}
                onClick={() => setConfirm(true)}
              >
                Cancel registration
              </button>
            )
          ) : event.status === "REGISTRATION_OPEN" ? (
            <>
              <p>
                Register for a place. If capacity is reached, you’ll join the
                waiting list.
              </p>
              <button
                className="button full"
                disabled={
                  registration.isPending || cancel.isPending || mine.isFetching
                }
                onClick={() => {
                  cancel.reset();
                  registration.mutate();
                }}
              >
                {registration.isPending
                  ? "Registering…"
                  : "Register for this event ↗"}
              </button>
            </>
          ) : (
            <p className="notice">
              {event.status === "CANCELLED"
                ? "This event has been cancelled."
                : event.status === "COMPLETED"
                  ? "This event has ended."
                  : "Registration has not opened yet."}
            </p>
          )}
          <button
            className="text-button"
            disabled={mine.isFetching}
            onClick={() => {
              void mine.refetch();
            }}
          >
            Refresh my status
          </button>
        </>
      )}
      {confirm && (
        <ConfirmDialog
          title="Cancel your registration?"
          busy={cancel.isPending}
          confirmLabel="Cancel registration"
          onClose={() => setConfirm(false)}
          onConfirm={() => {
            registration.reset();
            cancel.mutate();
          }}
        >
          <p>
            Your place will be released. If you register again, your position
            will be evaluated again.
          </p>
          <ErrorNotice error={cancel.error} />
        </ConfirmDialog>
      )}
    </section>
  );
}
