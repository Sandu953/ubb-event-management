import { useEffect, useState } from "react";
import { getHealth } from "../services/api";

export default function HomePage() {
  const [status, setStatus] = useState("Connecting...");

  useEffect(() => {
    getHealth()
      .then((data) => setStatus(data.status))
      .catch(() => setStatus("Backend unavailable"));
  }, []);

  return (
    <main>
      <h1>UBB Event Management</h1>
      <p>Discover and register for student events.</p>
      <p>Backend status: {status}</p>
    </main>
  );
}
