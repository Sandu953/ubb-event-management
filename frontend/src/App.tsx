import { BrowserRouter, Routes, Route, Link } from "react-router-dom";

import HomePage from "./pages/HomePage";

function Placeholder({ title }: { title: string }) {
  return <h1>{title}</h1>;
}

export default function App() {
  return (
    <BrowserRouter>
      <nav>
        <Link to="/">Home</Link> <Link to="/events">Events</Link>{" "}
        <Link to="/login">Login</Link>
      </nav>

      <Routes>
        <Route path="/" element={<HomePage />} />

        <Route path="/events" element={<Placeholder title="Events" />} />

        <Route path="/login" element={<Placeholder title="Login" />} />

        <Route path="/dashboard" element={<Placeholder title="Dashboard" />} />
      </Routes>
    </BrowserRouter>
  );
}
