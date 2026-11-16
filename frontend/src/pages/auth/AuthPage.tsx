import { useState, type FormEvent } from "react";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { useAuth } from "../../contexts/auth";
import { ErrorNotice, Field } from "../../components/common/UI";
export default function AuthPage({ register = false }: { register?: boolean }) {
  const auth = useAuth();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const redirect = params.get("redirect") || "/events";
  const destination =
    redirect.startsWith("/") &&
    !redirect.startsWith("//") &&
    !redirect.startsWith("/login") &&
    !redirect.startsWith("/register")
      ? redirect
      : "/events";
  const [show, setShow] = useState(false);
  const [validation, setValidation] = useState("");
  const mutation = useMutation({
    mutationFn: ({
      email,
      password,
      fullName,
    }: {
      email: string;
      password: string;
      fullName?: string;
    }) => auth.login(email, password, fullName),
    onSuccess: () => navigate(destination, { replace: true }),
  });
  if (auth.signedIn) return <Navigate to={destination} replace />;
  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const email = String(data.get("email")).trim();
    const password = String(data.get("password"));
    const fullName = register ? String(data.get("fullName")).trim() : undefined;
    if (!email || !password || (register && !fullName)) {
      setValidation("Complete all required fields.");
      return;
    }
    if (register && password.length < 12) {
      setValidation("Use at least 12 characters for your password.");
      return;
    }
    if (register && password !== data.get("confirmPassword")) {
      setValidation("Passwords do not match.");
      return;
    }
    setValidation("");
    mutation.mutate({ email, password, fullName });
  }
  return (
    <div className="auth-layout">
      <div className="auth-story">
        <p className="eyebrow">WELCOME TO YOUR COMMUNITY</p>
        <h1>
          {register
            ? "A new connection starts here."
            : "Good to have you back."}
        </h1>
        <p>
          One place for the events, organizations, and experiences that make
          university yours.
        </p>
        <div className="auth-art" aria-hidden="true">
          ✦
          <span>
            Stay curious.
            <br />
            Get involved.
          </span>
        </div>
      </div>
      <section className="panel auth-panel">
        <h2>{register ? "Create your account" : "Sign in to UBB Events"}</h2>
        <p className="muted">
          {register
            ? "Join as a student. Discover what’s next."
            : "Your next experience is waiting."}
        </p>
        <form onSubmit={submit} className="stack">
          <ErrorNotice error={mutation.error} />
          {validation && (
            <div className="notice error" role="alert">
              {validation}
            </div>
          )}
          {register && (
            <Field
              label="Full name"
              name="fullName"
              autoComplete="name"
              maxLength={200}
              required
            />
          )}
          <Field
            label="Email address"
            name="email"
            type="email"
            autoComplete="email"
            maxLength={254}
            required
          />
          <div>
            <Field
              label="Password"
              name="password"
              type={show ? "text" : "password"}
              autoComplete={register ? "new-password" : "current-password"}
              maxLength={128}
              required
              hint={register ? "At least 12 characters, up to 128." : undefined}
            />
            <button
              className="text-button"
              type="button"
              aria-pressed={show}
              onClick={() => setShow(!show)}
            >
              {show ? "Hide password" : "Show password"}
            </button>
          </div>
          {register && (
            <Field
              label="Confirm password"
              name="confirmPassword"
              type={show ? "text" : "password"}
              autoComplete="new-password"
              maxLength={128}
              required
            />
          )}
          <button
            className="button full"
            disabled={mutation.isPending}
            type="submit"
          >
            {mutation.isPending
              ? "Please wait…"
              : register
                ? "Create student account ↗"
                : "Sign in ↗"}
          </button>
          <p className="small muted">
            Sessions last until sign-out, token expiry, or a page reload. Your
            access token stays in memory.
          </p>
        </form>
        <p className="auth-switch">
          {register ? "Already part of the community?" : "New to UBB Events?"}{" "}
          <Link
            to={`${register ? "/login" : "/register"}?redirect=${encodeURIComponent(destination)}`}
          >
            {register ? "Sign in" : "Create an account"}
          </Link>
        </p>
      </section>
    </div>
  );
}
