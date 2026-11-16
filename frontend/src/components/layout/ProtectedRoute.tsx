import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useAuth } from "../../contexts/auth";
import { isStaff } from "../../utils/domain";
import { Empty, ErrorNotice, Loading } from "../common/UI";
export default function ProtectedRoute({ staff = false }: { staff?: boolean }) {
  const auth = useAuth();
  const location = useLocation();
  if (!auth.signedIn)
    return (
      <Navigate
        to={`/login?redirect=${encodeURIComponent(location.pathname + location.search)}`}
        replace
      />
    );
  if (auth.loading) return <Loading />;
  if (auth.error)
    return <ErrorNotice error={auth.error} retry={auth.refresh} />;
  if (staff && !isStaff(auth.current))
    return (
      <Empty title="Organization access required">
        Your account has no organization staff role. Contact your organization
        administrator.
      </Empty>
    );
  return <Outlet />;
}
