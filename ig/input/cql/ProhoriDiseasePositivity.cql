library ProhoriDiseasePositivity version '1.0.0'

/*
 * The populations MeasureEvaluator.cs computes by hand in C# — see docs/measures.md for why
 * this is declared but not interpreted by a live CQL engine (checked first: local HAPI's own
 * clinical-reasoning module loads, but $evaluate-measure isn't wired up even with it enabled).
 *
 * One field visit (Encounter) in the measurement period is the unit of analysis; a visit is
 * numerator-positive if its linked Observation's RDT result is SNOMED CT 10828004 (Positive).
 * No exclusions in this measure, so Initial Population == Denominator.
 */

using FHIR version '4.0.1'

include FHIRHelpers version '4.0.1' called FHIRHelpers

codesystem "SNOMED": 'http://snomed.info/sct'

parameter "Measurement Period" Interval<DateTime>

context Patient

define "Field Visits In Period":
  [Encounter] E
    where E.period.start during "Measurement Period"

define "Positive RDT Observations In Period":
  [Observation] O
    where O.effective during "Measurement Period"
      and O.value.coding.code contains '10828004' // SNOMED CT: Positive

define "Initial Population":
  exists("Field Visits In Period")

define "Denominator":
  "Initial Population"

define "Numerator":
  exists("Field Visits In Period" E
    where exists("Positive RDT Observations In Period" O where O.encounter.reference = 'Encounter/' + E.id))

define "City":
  Patient.address.city
