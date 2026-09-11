import { useMemo, useState } from "react";
import type {
  Questionnaire,
  QuestionnaireItem,
  QuestionnaireResponse,
  QuestionnaireResponseItem,
} from "../fhir/r4";
import { LINK } from "./linkIds";

export type Answers = Record<string, string>;

/** Flatten a Questionnaire's item tree into a linkId -> item lookup, for enableWhen and answerOption. */
function flatten(items: QuestionnaireItem[] | undefined, into: Map<string, QuestionnaireItem> = new Map()) {
  for (const item of items ?? []) {
    into.set(item.linkId, item);
    flatten(item.item, into);
  }
  return into;
}

function isEnabled(item: QuestionnaireItem, answers: Answers, itemsByLinkId: Map<string, QuestionnaireItem>): boolean {
  if (!item.enableWhen?.length) return true;
  const satisfied = item.enableWhen.map((ew) => {
    const target = itemsByLinkId.get(ew.question);
    const current = answers[ew.question];
    const expected = target?.type === "choice" || target?.type === "open-choice"
      ? ew.answerCoding?.code
      : (ew.answerString ?? ew.answerDate ?? ew.answerDateTime ?? String(ew.answerBoolean ?? ""));
    const exists = current !== undefined && current !== "";
    switch (ew.operator) {
      case "exists": return exists === Boolean(ew.answerBoolean ?? true);
      case "=": return current === expected;
      case "!=": return current !== expected;
      default: return exists; // >, <, >=, <= aren't used by any Prohori item today
    }
  });
  return item.enableBehavior === "any" ? satisfied.some(Boolean) : satisfied.every(Boolean);
}

/** Answers -> a QuestionnaireResponse, walking the Questionnaire's own item tree so the shape
 * always matches what was rendered (and what $extract expects) — no separate "response schema". */
function buildResponse(
  items: QuestionnaireItem[],
  answers: Answers,
  itemsByLinkId: Map<string, QuestionnaireItem>,
): QuestionnaireResponseItem[] {
  const out: QuestionnaireResponseItem[] = [];
  for (const item of items) {
    if (!isEnabled(item, answers, itemsByLinkId)) continue;
    if (item.type === "group") {
      const children = buildResponse(item.item ?? [], answers, itemsByLinkId);
      if (children.length) out.push({ linkId: item.linkId, item: children });
      continue;
    }
    const value = answers[item.linkId]?.trim();
    if (!value) continue;
    if (item.linkId === LINK.givenNames) {
      const names = value.split(/\s+/).filter(Boolean);
      if (names.length) out.push({ linkId: item.linkId, answer: names.map((valueString) => ({ valueString })) });
    } else if (item.type === "choice") {
      const coding = item.answerOption?.find((o) => o.valueCoding?.code === value)?.valueCoding;
      if (coding) out.push({ linkId: item.linkId, answer: [{ valueCoding: coding }] });
    } else if (item.type === "date") {
      out.push({ linkId: item.linkId, answer: [{ valueDate: value }] });
    } else if (item.type === "dateTime") {
      out.push({ linkId: item.linkId, answer: [{ valueDateTime: new Date(value).toISOString() }] });
    } else {
      out.push({ linkId: item.linkId, answer: [{ valueString: value }] });
    }
  }
  return out;
}

export function answersFromResponse(items: QuestionnaireResponseItem[] | undefined): Answers {
  const answers: Answers = {};
  for (const item of items ?? []) {
    if (item.item) Object.assign(answers, answersFromResponse(item.item));
    if (item.linkId === LINK.givenNames && item.answer) {
      answers[item.linkId] = item.answer.map((a) => a.valueString).filter(Boolean).join(" ");
    } else if (item.answer?.[0]) {
      const v = item.answer[0];
      const value = v.valueCoding?.code ?? v.valueString ?? v.valueDate ?? v.valueDateTime;
      if (value) answers[item.linkId] = value;
    }
  }
  return answers;
}

export function QuestionnaireForm({
  questionnaire,
  initialAnswers,
  onSubmit,
  submitting,
  submitLabel = "Submit case",
}: {
  questionnaire: Questionnaire;
  initialAnswers?: Answers;
  onSubmit: (response: QuestionnaireResponse) => void;
  submitting?: boolean;
  submitLabel?: string;
}) {
  const itemsByLinkId = useMemo(() => flatten(questionnaire.item), [questionnaire]);
  const [answers, setAnswers] = useState<Answers>(initialAnswers ?? {});
  const set = (linkId: string, value: string) => setAnswers((prev) => ({ ...prev, [linkId]: value }));

  const items = questionnaire.item ?? [];

  return (
    <form
      className="qform"
      onSubmit={(e) => {
        e.preventDefault();
        onSubmit({
          resourceType: "QuestionnaireResponse",
          questionnaire: questionnaire.url,
          status: "completed",
          item: buildResponse(items, answers, itemsByLinkId),
        });
      }}
    >
      {items.map((item) => (
        <FormItem key={item.linkId} item={item} answers={answers} onChange={set} itemsByLinkId={itemsByLinkId} />
      ))}
      <button type="submit" className="qform__submit" disabled={submitting}>
        {submitting ? "Submitting…" : submitLabel}
      </button>
    </form>
  );
}

function FormItem({
  item,
  answers,
  onChange,
  itemsByLinkId,
}: {
  item: QuestionnaireItem;
  answers: Answers;
  onChange: (linkId: string, value: string) => void;
  itemsByLinkId: Map<string, QuestionnaireItem>;
}) {
  if (!isEnabled(item, answers, itemsByLinkId)) return null;

  if (item.type === "group") {
    return (
      <fieldset className="qform__group">
        <legend>{item.text}</legend>
        {(item.item ?? []).map((child) => (
          <FormItem key={child.linkId} item={child} answers={answers} onChange={onChange} itemsByLinkId={itemsByLinkId} />
        ))}
      </fieldset>
    );
  }

  const value = answers[item.linkId] ?? "";
  const inputId = `q-${item.linkId}`;

  return (
    <div className="field">
      <label htmlFor={inputId}>
        {item.text}
        {item.required && " *"}
      </label>
      {item.type === "choice" ? (
        <select id={inputId} required={item.required} value={value} onChange={(e) => onChange(item.linkId, e.target.value)}>
          <option value="">Select…</option>
          {(item.answerOption ?? []).map((option) => (
            <option key={option.valueCoding?.code} value={option.valueCoding?.code}>
              {option.valueCoding?.display}
            </option>
          ))}
        </select>
      ) : item.type === "date" ? (
        <input id={inputId} type="date" required={item.required} value={value} onChange={(e) => onChange(item.linkId, e.target.value)} />
      ) : item.type === "dateTime" ? (
        <input id={inputId} type="datetime-local" required={item.required} value={value} onChange={(e) => onChange(item.linkId, e.target.value)} />
      ) : (
        <input
          id={inputId}
          type="text"
          required={item.required}
          value={value}
          placeholder={item.linkId === LINK.givenNames ? "space-separated" : undefined}
          onChange={(e) => onChange(item.linkId, e.target.value)}
        />
      )}
    </div>
  );
}
