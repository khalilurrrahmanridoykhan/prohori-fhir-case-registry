import { useState } from "react";
import { Link } from "react-router-dom";
import { extract, populate, useCaseQuestionnaire } from "../questionnaire/api";
import { answersFromResponse, QuestionnaireForm, type Answers } from "../questionnaire/QuestionnaireForm";
import { accessToken } from "../smart";
import type { QuestionnaireResponse } from "../fhir/r4";

export function NewCase() {
  const { data: questionnaire, isLoading, isError, error } = useCaseQuestionnaire();
  const [nationalId, setNationalId] = useState("");
  const [prefill, setPrefill] = useState<{ answers: Answers; key: number } | undefined>();
  const [populating, setPopulating] = useState(false);
  const [populateNote, setPopulateNote] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const [result, setResult] = useState<{ created: string[] } | undefined>();
  const [submitError, setSubmitError] = useState<string>();

  if (!accessToken()) {
    return (
      <>
        <h1 className="page__title">New case</h1>
        <div className="state">
          Submitting a case needs a write-scoped SMART launch.{" "}
          <a href="/launch.html">Launch via SMART</a> (or run the local Keycloak launch — see{" "}
          <code>docs/smart-launch.md</code>).
        </div>
      </>
    );
  }

  async function handlePopulate() {
    if (!nationalId.trim()) return;
    setPopulating(true);
    setPopulateNote(undefined);
    try {
      const response = await populate(nationalId.trim());
      const answers = answersFromResponse(response.item);
      setPrefill({ answers, key: Date.now() });
      setPopulateNote(
        Object.keys(answers).length
          ? "Prefilled from an existing patient — review before submitting."
          : `No patient found for National ID ${nationalId.trim()} — starting a blank case.`,
      );
    } catch (e) {
      setPopulateNote((e as Error).message);
    } finally {
      setPopulating(false);
    }
  }

  async function handleSubmit(response: QuestionnaireResponse) {
    setSubmitting(true);
    setSubmitError(undefined);
    try {
      setResult(await extract(response));
    } catch (e) {
      setSubmitError((e as Error).message);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <h1 className="page__title">New case</h1>
      <p className="page__lede">
        Field intake as a FHIR <code>Questionnaire</code> — submitting it calls{" "}
        <code>$extract</code>, which builds the same Patient/Encounter/Observation Bundle the
        typed <code>/cases</code> endpoint does.
      </p>

      {result ? (
        <div className="state">
          Case submitted.
          <ul className="qform__created">
            {result.created.map((location) => (
              <li key={location}>{location}</li>
            ))}
          </ul>
          <button type="button" className="qform__submit" onClick={() => { setResult(undefined); setPrefill(undefined); setNationalId(""); }}>
            Record another visit
          </button>
          <p>
            <Link to="/">Back to case surveillance</Link>
          </p>
        </div>
      ) : (
        <>
          <div className="qform__populate">
            <div className="field">
              <label htmlFor="populate-nid">Returning patient? National ID</label>
              <input
                id="populate-nid"
                type="text"
                value={nationalId}
                onChange={(e) => setNationalId(e.target.value)}
                placeholder="19942691012345678"
              />
            </div>
            <button type="button" className="qform__populate-button" onClick={handlePopulate} disabled={populating || !nationalId.trim()}>
              {populating ? "Looking up…" : "Prefill"}
            </button>
            {populateNote && <span className="qform__populate-note">{populateNote}</span>}
          </div>

          {isLoading && <div className="state">Loading the field-intake form…</div>}
          {isError && <div className="state state--error">Could not load the Questionnaire — {(error as Error).message}</div>}
          {submitError && <div className="state state--error">{submitError}</div>}

          {questionnaire && (
            <QuestionnaireForm
              key={prefill?.key}
              questionnaire={questionnaire}
              initialAnswers={prefill?.answers}
              onSubmit={handleSubmit}
              submitting={submitting}
            />
          )}
        </>
      )}
    </>
  );
}
