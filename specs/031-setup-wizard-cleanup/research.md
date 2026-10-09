# Research: Setup Wizard Layout Cleanup

## Where do language and sample data live?

- **Decision**: Leave them on `/language-select`. Do not add them to the wizard.
- **Rationale**: Spec 029 moved them there deliberately (its FR-011 to FR-014). The language must be applied before the wizard renders, and sample data skips the wizard, so it cannot live inside it. Issue #359 predates that move and its "top of the first page" request is already met by the first screen.
- **Alternatives considered**: Duplicating them on the wizard's first tab. Rejected: it contradicts spec 029 FR-014 ("reachable only from the first-run language screen") and clutters the tab the issue wants emptier.

## How to build the Accounting tab

- **Decision**: One new `AccountingTab` component that renders the currency, financial-year and inception fields, the two fee fields, then the existing `SalesTaxTab`.
- **Rationale**: The fields already exist as markup; only their host changes. Hosting `SalesTaxTab` keeps its toggle-off clearing logic and its tests intact, and puts each fee above its tax treatment as FR-005 asks.
- **Alternatives considered**: Folding everything into one large markup file (duplicates `SalesTaxTab`'s logic); leaving fees on the Organisation tab (splits fees from their tax treatment).

## Field placement

- **Decision**: Renewal month and audit retention stay on the Organisation tab with name and theme. Fees go to Accounting.
- **Rationale**: Renewal month and retention are not money. Fees are money and carry tax treatment.
- **Alternatives considered**: Moving renewal month to Accounting beside fees. Rejected: it leaves the Organisation tab with only two fields and adds a non-accounting field to Accounting.

## Review grouping

- **Decision**: Two headed groups, Organisation and Accounting, followed by the existing committee-related lines and queued lists. Add currency, financial-year start, inception date and tax entry mode, which the Review tab does not show today.
- **Rationale**: Spec 017 FR-005 already requires every entered value to be shown; these four were added by spec 028 and never surfaced.
- **Alternatives considered**: Leaving Review as is. Rejected: FR-009 of this spec requires grouping that matches the tabs.

## Tab label

- **Decision**: Keep `Setup_Tab_OrganisationSettings` unchanged; add `Setup_Tab_Accounting`.
- **Rationale**: Renaming a key's wording forces a re-translation across every culture for no user benefit.
- **Alternatives considered**: Rename to "Organisation". Rejected for the cost above.
