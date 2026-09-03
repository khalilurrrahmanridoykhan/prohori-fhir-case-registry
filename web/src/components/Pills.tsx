import { diseaseLabel, type Disease, type Result } from "../fhir/terminology";

export function ResultPill({ result }: { result: Result }) {
  const label = result === "positive" ? "Positive" : result === "negative" ? "Negative" : "No result";
  return <span className={`pill pill--${result}`}>{label}</span>;
}

export function DiseaseTag({ disease }: { disease: Disease }) {
  return (
    <span className="disease">
      <span className={`dot dot--${disease}`} aria-hidden="true" />
      {diseaseLabel[disease]}
    </span>
  );
}
