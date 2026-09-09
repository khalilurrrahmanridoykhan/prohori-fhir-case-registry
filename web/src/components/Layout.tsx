import { smartClient } from "../smart";
import type { ReactNode } from "react";
import { Link } from "react-router-dom";
import { FHIR_BASE } from "../config";

export function Layout({ children }: { children: ReactNode }) {
  return (
    <>
      <header className="masthead">
        <div className="masthead__inner">
          <Link to="/" className="wordmark">
            Pro<span>hori</span>
          </Link>
          <span className="masthead__tag">{smartClient ? `Patient ${smartClient.patient.id}` : "field case registry"}</span>
          {!smartClient && <a href="/launch.html">SMART launch</a>}
          <span className="masthead__server">{FHIR_BASE.replace(/^https?:\/\//, "")}</span>
        </div>
      </header>
      <main className="page">{children}</main>
    </>
  );
}
