using System.Collections.Generic;

/// <summary>
/// Author-time analysis for Cue Books. Produces FLAGS — never blocks, never edits — surfaced both
/// in the per-element inspector (<c>CueBookDataEditor</c>) and the project-wide sweep (<c>CueIdVerifierWindow</c>),
/// so the two share one source of truth. Each finding carries the CONSEQUENCE and the appropriate FIX.
///
/// Only conditions DETERMINABLE FROM THE ASSET ALONE are flagged (no false positives): the deadlock case
/// "AfterPreviousCompletion behind a held element with no end" can't be told from a valid code-stopped loop
/// (whether code calls Stop lives in the ability, not the book), so it is guidance in the mode TOOLTIP, not a flag.
/// </summary>
public static class CueBookLinter
{
    public enum Severity { Info, Warning }

    public struct Finding
    {
        public string effectId;
        public int elementIndex;     // -1 = effect-level
        public Severity severity;
        public string message;       // the consequence
        public string fix;           // how to resolve it
    }

    /// <summary>Analyze one book; returns every finding (empty list = clean).</summary>
    public static List<Finding> Analyze(CueBookData book)
    {
        var findings = new List<Finding>();
        if (book == null || book.effects == null) return findings;

        foreach (var effect in book.effects)
        {
            if (effect == null || effect.elements == null) continue;
            var elements = effect.elements;
            int n = elements.Count;

            // F6 — a variant "group" of ONE element always plays; the flag is either noise or a missed sibling.
            for (int i = 0; i < n; i++)
            {
                if (elements[i] == null || !elements[i].isVariant) continue;
                int end = i;
                while (end + 1 < n && elements[end + 1] != null && elements[end + 1].isVariant) end++;
                if (end == i)
                {
                    findings.Add(new Finding
                    {
                        effectId = effect.id, elementIndex = i, severity = Severity.Info,
                        message = "Is Variant is set on a single element (no consecutive sibling) — a group of one always plays; the flag does nothing.",
                        fix = "Mark the alternative element(s) directly next to it as Is Variant too, or untick it."
                    });
                }
                i = end;
            }

            for (int i = 0; i < n; i++)
            {
                var el = elements[i];
                if (el == null) continue;

                // F8 — Draw On Top only affects a spawned VISUAL; on Sound/Manpu it does nothing.
                if (el.drawOnTop && (el.kind == CueElementKind.Sound || el.kind == CueElementKind.Manpu))
                {
                    findings.Add(new Finding
                    {
                        effectId = effect.id, elementIndex = i, severity = Severity.Info,
                        message = "Draw On Top is set on a " + el.kind + " element — only Particle/Vfx visuals are moved to the GroundVFX layer; the flag does nothing here.",
                        fix = "Untick Draw On Top, or move it to the visual element it was meant for."
                    });
                }

                // F3 — a start mode on the FIRST element is ignored (it always starts at the effect's t=0).
                if (i == 0 && el.startMode != CueStartMode.Immediate)
                {
                    findings.Add(new Finding
                    {
                        effectId = effect.id, elementIndex = 0, severity = Severity.Info,
                        message = "First element's Start Mode is ignored — it always starts at the effect's t=0 (+ delay).",
                        fix = "Set the first element to Immediate (behaves the same; removes this note)."
                    });
                }

                // Cut checks.
                if (el.canCut && el.cuts != null)
                {
                    foreach (var cut in el.cuts)
                    {
                        // F5 — cut target out of range, or not an EARLIER element (cuts stop earlier elements only).
                        if (cut.targetIndex < 0 || cut.targetIndex >= n)
                        {
                            findings.Add(new Finding
                            {
                                effectId = effect.id, elementIndex = i, severity = Severity.Warning,
                                message = $"Cut targets index {cut.targetIndex}, which is out of range (0..{n - 1}) — it will never fire.",
                                fix = "Point the cut at a valid EARLIER element index."
                            });
                            continue;
                        }
                        if (cut.targetIndex >= i)
                        {
                            findings.Add(new Finding
                            {
                                effectId = effect.id, elementIndex = i, severity = Severity.Warning,
                                message = $"Cut targets index {cut.targetIndex} (this element or a later one) — cuts only stop EARLIER elements, so it will never fire.",
                                fix = "Point the cut at an element index BEFORE this one."
                            });
                            continue;
                        }

                        // F7 — the cut's target is a variant-group member: on plays where that variant is not
                        // chosen the cut is silently dropped (the runner drops cuts at skipped targets).
                        if (elements[cut.targetIndex] != null && elements[cut.targetIndex].isVariant)
                        {
                            findings.Add(new Finding
                            {
                                effectId = effect.id, elementIndex = i, severity = Severity.Warning,
                                message = $"Cut targets element {cut.targetIndex}, which is a VARIANT — on plays where another variant is chosen, this cut is dropped.",
                                fix = "Cut a non-variant element, or accept that the cut only applies when that variant is picked."
                            });
                        }

                        // F4 — circular: this element waits for its predecessor to COMPLETE, but it also cuts that
                        // predecessor. The predecessor can't be cut until this element starts, and this element
                        // won't start until the predecessor ends → deadlock.
                        if (el.startMode == CueStartMode.AfterPreviousCompletion && cut.targetIndex == i - 1)
                        {
                            findings.Add(new Finding
                            {
                                effectId = effect.id, elementIndex = i, severity = Severity.Warning,
                                message = $"Deadlock: this element waits for element {i - 1} to complete, but it also CUTS element {i - 1}. " +
                                          "It can't cut it until it starts, and it won't start until that element ends.",
                                fix = "Move the cut onto a PARALLEL element (With Previous) that runs alongside the target, or stop the target from code (CueHandle.Stop)."
                            });
                        }
                    }
                }
            }
        }

        return findings;
    }
}
