import { useEffect, useRef, useState } from "react";
import { Link, NavLink, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/auth";
import { isAdmin, isStaff } from "../../utils/domain";

export default function Layout() {
  const { current, signedIn, logout } = useAuth();
  const [open, setOpen] = useState(false);
  const location = useLocation();
  const mainRef = useRef<HTMLElement>(null);
  const menuRef = useRef<HTMLButtonElement>(null);
  const brandRef = useRef<HTMLAnchorElement>(null);
  useEffect(() => {
    mainRef.current?.focus();
  }, [location.pathname]);
  useEffect(() => {
    if (open) brandRef.current?.focus();
  }, [open]);
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <aside
        id="main-navigation"
        className={`sidebar ${open ? "is-open" : ""}`}
        onKeyDown={(event) => {
          if (event.key === "Escape") {
            setOpen(false);
            menuRef.current?.focus();
          }
        }}
      >
        <Link
          ref={brandRef}
          className="brand"
          to="/"
          onClick={() => setOpen(false)}
        >
          <span className="brand-mark" aria-hidden="true">
            u.
          </span>
          <span>
            UBB Events<small>STUDENT COMMUNITY</small>
          </span>
        </Link>
        <p className="nav-label">DISCOVER</p>
        <nav aria-label="Main navigation" onClick={() => setOpen(false)}>
          <NavLink to="/" end>
            <span aria-hidden="true">⌂</span>Home
          </NavLink>
          <NavLink to="/events">
            <span aria-hidden="true">▦</span>Explore events
          </NavLink>
          {signedIn && (
            <>
              <NavLink to="/registrations">
                <span aria-hidden="true">▤</span>My registrations
              </NavLink>
              <NavLink to="/profile">
                <span aria-hidden="true">◎</span>My profile
              </NavLink>
            </>
          )}
          {isStaff(current) && (
            <>
              <p className="nav-label">WORKSPACE</p>
              <NavLink to="/dashboard">
                <span aria-hidden="true">◫</span>Dashboard
              </NavLink>
              <NavLink to="/manage/events">
                <span aria-hidden="true">▦</span>Manage events
              </NavLink>
              <NavLink to="/manage/events/new">
                <span aria-hidden="true">＋</span>Create event
              </NavLink>
              <NavLink to="/organizations">
                <span aria-hidden="true">◇</span>
                {isAdmin(current) ? "Administration" : "My organizations"}
              </NavLink>
            </>
          )}
        </nav>
        <div className="sidebar-bottom">
          <div className="community-note">
            <span className="eyebrow">MADE FOR CONNECTION</span>
            <p>Your next great experience starts on campus.</p>
          </div>
          {signedIn ? (
            <>
              <Link to="/profile" className="user-summary">
                <span className="avatar">
                  {current?.user.fullName.slice(0, 1) || "U"}
                </span>
                <span>
                  {current?.user.fullName || "Loading account…"}
                  <small>
                    {current?.user.globalRole === "ADMIN"
                      ? "System administrator"
                      : "Student community"}
                  </small>
                </span>
              </Link>
              <button className="text-button" onClick={logout}>
                Sign out
              </button>
            </>
          ) : (
            <Link className="button full" to="/register">
              Join the community ↗
            </Link>
          )}
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <div className="actions">
            <button
              className="menu-button button secondary small"
              ref={menuRef}
              aria-expanded={open}
              aria-controls="main-navigation"
              onClick={() => setOpen(!open)}
            >
              ☰ <span>Menu</span>
            </button>
            <span className="topbar-title">Campus life, connected.</span>
          </div>
          <div className="actions">
            <span className="campus-label">Cluj-Napoca · UBB</span>
            {!signedIn && (
              <Link className="button secondary small" to="/login">
                Sign in <span aria-hidden="true">↗</span>
              </Link>
            )}
          </div>
        </header>
        <main ref={mainRef} id="main-content" tabIndex={-1}>
          <Outlet />
        </main>
        <footer className="footer">
          <span>UBB Events · Student organization platform</span>
          <Link to="/events">Find your next event ↗</Link>
        </footer>
      </div>
    </div>
  );
}
