# 0004 — Pricing parity proven by differential tests, quirks included

- **Status:** Accepted · 2026-10-03

## Context

Order pricing lives in `usp_PriceOrder`: tier discounts, a volume discount, a cap, free-shipping threshold, VAT rates.
Invoices issued over ten years depend on its exact behaviour, including the parts nobody would design that way today.
Reading the procedure and rewriting it is how migrations silently change prices.

## Decision

- The rules are ported to a pure domain (`Comptoir.Domain.OrderPricing`) and proven equal by a **differential test**:
  CsCheck generates random orders (tiers including lower case and blanks found in old data, quantities around the
  100-unit threshold, prices with half-cent rounding cases, unknown VAT codes), prices each with the stored procedure
  on a real SQL Server and with the port, and requires equality to the cent on every line and total. A mismatch is
  shrunk to the smallest order that still differs.
- Legacy behaviours are kept and named in the code: VAT rounded **per line** then summed; volume discount judged
  **per line**, not per product; rounding **half away from zero** (T-SQL `ROUND`), not .NET's default banker's
  rounding; unknown tier = no discount; unknown VAT code = 0 %.
- The test is mutation-checked: switching to banker's rounding is caught (`15 × 610.11` for a tier-A customer), so is
  an off-by-one on the threshold (`99 × 278.09`).

## Consequences

- ✅ "The new system prices like the old one" is a fact checked on every commit, not a belief.
- ✅ Each quirk is now documented, so the business can decide to change one — as a visible decision, not a bug.
- ⚠️ Parity freezes the quirks for now. Fixing one later means changing the legacy procedure too, or accepting that
  the two systems differ while both are live.
