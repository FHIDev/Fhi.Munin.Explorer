category: Notes for hosts

- **`munin-explorer-group` is now spacing only, and the heading style is yours again.** The
  catalogue group headings in the detail panel, the kilde view, the datasamling view and the
  variable view already wear `headline headline-xxs margin--none`. The rule for
  `munin-explorer-group` used to write Runa's eyebrow over the top of that — `0.6875rem`, weight
  700, uppercase, `letter-spacing: 0.08em` and a navy of its own — which drew a group heading at
  11px above the 16px values it introduces, so the panel did not scan as sections at all. All that
  is left in the rule is `margin: 20px 0 8px`, the space between one group and the next. Both
  sample hosts do this now, and the rule in `Fhi.Helsedata.Stiler` under `components/munin-explorer/`
  wants the same five declarations removed; a host that wrote its own copy of the eyebrow should
  drop it too. (Fhi.Metadata-gvtt9)
- **This moves both explorers, and that is the intent.** `munin-explorer-group` is shared with the
  variable detail panel, so the variable panel's Identifikasjon and Plassering headings change with
  the kilde panel's seven. The class is deliberately not split: the argument for letting the host's
  own heading scale win is the same on both sides, and a second name would be a second thing for
  every host to style. Runa is untouched — this is only about what the component does inside a
  host's pages. (Fhi.Metadata-gvtt9)
- **The kilde, datasamling and variable ingress paragraphs no longer carry a colour of ours.**
  `munin-explorer-kilde__description`, `munin-explorer-datasamling__description` and
  `munin-explorer-whole__description` share one rule, and it set a grey the host's own `ingress`
  class never asks for: `ingress` is styled only inside particular page types and none of those
  rules set a colour. Each paragraph inherits the body colour now. Spacing and the `65ch` measure
  are unchanged. (Fhi.Metadata-gvtt9)
- **Nothing in this repository can see Stiler, so green CI here is not evidence of the result on
  helsedata.no.** The two checks read the sample stylesheet and the capture of helsedata's live
  page, and neither is Stiler. Until the same five declarations come out there, a host on Stiler
  still gets the eyebrow — which is why this change alone does not finish the bead, and the
  matching `Fhi.Helsedata.Stiler` edit has to land before it can be closed. Note too that the
  samples' `headline-xxs` is a stand-in at 14px/600 while the real class measures 16px/400, so the
  samples under-state how much the heading grows. (Fhi.Metadata-gvtt9)
