import { lazy, Suspense } from "react";
import { Link, Route, Routes } from "react-router-dom";
import Layout from "./components/layout/Layout";
import ProtectedRoute from "./components/layout/ProtectedRoute";
import { Empty, Loading } from "./components/common/UI";
const HomePage = lazy(() => import("./pages/public/HomePage"));
const EventsPage = lazy(() => import("./pages/public/EventsPage"));
const EventDetailsPage = lazy(() => import("./pages/public/EventDetailsPage"));
const AuthPage = lazy(() => import("./pages/auth/AuthPage"));
const RegistrationsPage = lazy(
  () => import("./pages/student/RegistrationsPage"),
);
const ProfilePage = lazy(() => import("./pages/student/ProfilePage"));
const DashboardPage = lazy(() => import("./pages/organizer/DashboardPage"));
const ManageEventsPage = lazy(
  () => import("./pages/organizer/ManageEventsPage"),
);
const EventEditorPage = lazy(() => import("./pages/organizer/EventEditorPage"));
const ManageEventPage = lazy(() => import("./pages/organizer/ManageEventPage"));
const ParticipantsPage = lazy(
  () => import("./pages/organizer/ParticipantsPage"),
);
const OrganizationsPage = lazy(() => import("./pages/admin/OrganizationsPage"));
const OrganizationDetailsPage = lazy(
  () => import("./pages/admin/OrganizationDetailsPage"),
);
export default function App() {
  return (
    <Suspense fallback={<Loading />}>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<HomePage />} />
          <Route path="events" element={<EventsPage />} />
          <Route path="events/:id" element={<EventDetailsPage />} />
          <Route path="login" element={<AuthPage />} />
          <Route path="register" element={<AuthPage register />} />
          <Route element={<ProtectedRoute />}>
            <Route path="registrations" element={<RegistrationsPage />} />
            <Route path="profile" element={<ProfilePage />} />
          </Route>
          <Route element={<ProtectedRoute staff />}>
            <Route path="dashboard" element={<DashboardPage />} />
            <Route path="manage/events" element={<ManageEventsPage />} />
            <Route path="manage/events/new" element={<EventEditorPage />} />
            <Route
              path="manage/events/:id/edit"
              element={<EventEditorPage />}
            />
            <Route path="manage/events/:id" element={<ManageEventPage />} />
            <Route
              path="manage/events/:id/registrations"
              element={<ParticipantsPage />}
            />
            <Route
              path="manage/events/:id/attendance"
              element={<ParticipantsPage attendanceView />}
            />
            <Route path="organizations" element={<OrganizationsPage />} />
            <Route
              path="organizations/:id"
              element={<OrganizationDetailsPage />}
            />
          </Route>
          <Route
            path="*"
            element={
              <Empty title="This page wandered off">
                <p>The page you’re looking for doesn’t exist.</p>
                <Link className="button" to="/events">
                  Explore events ↗
                </Link>
              </Empty>
            }
          />
        </Route>
      </Routes>
    </Suspense>
  );
}
