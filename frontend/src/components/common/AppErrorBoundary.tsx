import { Component, type ReactNode } from "react";
export default class AppErrorBoundary extends Component<
  { children: ReactNode },
  { failed: boolean }
> {
  state = { failed: false };
  static getDerivedStateFromError() {
    return { failed: true };
  }
  render() {
    return this.state.failed ? (
      <main className="fatal-error">
        <h1>Let’s get you back on track.</h1>
        <p>The page could not be displayed. Reload to try again.</p>
        <button className="button" onClick={() => window.location.reload()}>
          Reload application
        </button>
      </main>
    ) : (
      this.props.children
    );
  }
}
