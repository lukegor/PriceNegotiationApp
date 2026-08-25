# PriceNegotiationApp — Business Feature Analysis

Date: 2026-08-25
Role: Business analysis (product scope only — no technical/architectural changes)
Context: Backend-only price-negotiation marketplace; portfolio program, so every proposed
feature must be a *real* business capability that would matter to a live shop, and must be
demonstrable through the API surface alone.

---

## 1. Executive Summary

The product today implements **one half of a negotiation**: customers and staff can trade
offers until someone accepts or walks away — but an accepted price produces nothing a
customer can actually buy, silence has no time cost, and neither party is ever told that it
is their turn. The negotiation loop is mechanically complete but commercially inert.

The highest-value additions are therefore not more negotiation mechanics; they are the
features that give the existing loop **an outcome** (a purchase), **a clock** (expiry),
and **a voice** (notifications and history). Everything else is optimization.

Recommended sequencing: 6 "Must" features form Wave 1–2 and turn the app into a closed
deal pipeline; the remainder are Should/Could items that improve conversion quality,
staff throughput, and trust.

---

## 2. Current Capability Map (as-is)

| Capability | Status |
|---|---|
| Customer registration / login / roles | ✅ |
| Product catalog: search, filter, paging | ✅ |
| Open negotiation on a product with initial offer | ✅ (1 per product per customer) |
| Bounded haggling: max 3 proposals, ≤2× base price cap, auto-rejection | ✅ |
| Staff accept / reject-current-offer; customer counter / withdraw | ✅ |
| Terminal states: Accepted, Rejected (auto), Withdrawn | ✅ |
| Hard delete by admin | ✅ |

**What is conspicuously absent:** anything after `Accepted`, any deadline on `Open`,
any signal between parties, any memory of the offers exchanged.

---

## 3. Personas & Journeys

- **Customer (buyer)** — wants the best price with minimum round-trips; abandons when ignored.
- **Staff (seller agent)** — handles many negotiations; needs triage, context, and guardrails.
- **Admin (shop owner)** — owns margin policy, oversight, and cleanup.

Journey today: browse → negotiate → *(silence risk)* → accept → **dead end**.
Target journey: browse → negotiate → notified turns → accepted deal → **purchase record** → repeat.

---

## 4. Gap Analysis

| # | Gap | Business consequence |
|---|---|---|
| G-01 | Accepted negotiation has no fulfillment outcome | The core value exchange never completes; no revenue event exists |
| G-02 | No expiry on open negotiations | Stale pipeline; customers ghosted indefinitely; staff queue pollutes over time |
| G-03 | No proposal history visible | Disputes ("you offered X!") cannot be settled; staff lack context |
| G-04 | No notifications | Every turn requires manual polling; deals die in silence |
| G-05 | Decline carries no reason | Customers can't improve their offer intelligently; feels arbitrary |
| G-06 | Staff have no triage view | First-come-first-served on a flat list; SLAs impossible |
| G-07 | Policy constants are fixed in code | Margin rules can't respond to market without a release |
| G-08 | Staff decide blind | No acceptance-rate/discount visibility; inconsistent pricing across agents |
| G-09 | No abuse controls on negotiation creation | Lowball spam drowns staff; inventory probing at scale |
| G-10 | Catalog is name+price only | Weak discovery; can't negotiate on out-of-stock or category-scoped goods |
| G-11 | No post-deal trust signals | No social proof to drive new negotiations |
| G-12 | Identity lacks recovery flows | Locked-out customers are permanently churned |

---

## 5. Feature Backlog

Priorities: **M** = Must (core value broken without it), **S** = Should (material lift),
**C** = Could (differentiator). All features are API-level capabilities; UI is out of scope.

### Wave 1 — Close the loop (Must)

#### BF-01 · Purchase from accepted deal
- **Persona:** Customer / Admin · **Priority:** M
- **Problem:** When staff accept, the customer holds a status flag, not a purchasable artifact.
- **Behavior:** An accepted negotiation generates a **Deal** — a redeemable record binding
  customer, product, agreed price, and validity window (e.g., 72h). Customer lists their deals,
  marks one as purchased (or admin confirms payment); deal transitions Redeemed/Expired.
  Product's base price may optionally re-anchor to the last struck deal.
- **Success metric:** % of Accepted negotiations converted into Deals within validity window.
- **Dependencies:** none (this unblocks BF-12, BF-15).

#### BF-02 · Negotiation expiry & stale-pipeline hygiene
- **Persona:** Admin / Customer · **Priority:** M
- **Problem:** Open negotiations live forever if either side goes quiet.
- **Behavior:** Configurable inactivity windows: e.g., staff silence >7 days auto-closes as
  `Expired` (customer freed to start fresh); customer silence >14 days after staff action
  likewise expires. Expiry is terminal and preserves history.
- **Success metric:** median age of open negotiations; % of opens resolved within policy window.
- **Dependencies:** none.

#### BF-03 · Proposal history timeline
- **Persona:** Customer / Staff · **Priority:** M
- **Problem:** Only the current offer is visible; the negotiation's story is lost.
- **Behavior:** Negotiation detail returns an ordered ledger: each proposal (who, amount,
  timestamp, source: initial/counter/auto-reject), staff actions (reject-current-offer events
  with reason once BF-06 lands), and state transitions.
- **Success metric:** support/dispute tickets about "what was offered" drop to zero.
- **Dependencies:** none.

### Wave 2 — Give the loop a voice (Must/Should)

#### BF-04 · Turn-based notifications
- **Persona:** Customer / Staff · **Priority:** S (M for real deployments)
- **Problem:** Nobody knows it's their turn; polling is the only option.
- **Behavior:** Per-user notification feed: offer received, offer rejected, negotiation
  accepted/expired/expiring-soon, deal awaiting redemption. Read/unread + list endpoints;
  optional email dispatch later.
- **Success metric:** average hours-to-response per turn drops materially.
- **Dependencies:** benefits from BF-02 (expiring-soon events).

#### BF-05 · Staff work queue & claim
- **Persona:** Staff · **Priority:** S
- **Problem:** Flat chronological list doesn't scale past a handful of concurrent negotiations.
- **Behavior:** Queue views filtered by state (awaiting-staff, awaiting-customer, decided),
  sorted by wait time; a staff member can **claim** a negotiation so others see ownership;
  admins see load per staff member.
- **Success metric:** first-response time distribution; % negotiations claimed vs orphaned.
- **Dependencies:** BF-02 for meaningful sorting.

#### BF-06 · Decline with structured feedback
- **Persona:** Staff / Customer · **Priority:** S
- **Problem:** A bare rejection gives the customer nothing to act on.
- **Behavior:** Reject-current-offer accepts an optional short **reason** from a controlled
  set (too low / near budget / manager review) plus free-text note; surfaced to the customer
  in timeline and decline response.
- **Success metric:** counter-rate after rejection increases (customers iterate instead of quitting).
- **Dependencies:** BF-03 displays reasons historically.

### Wave 3 — Policy control & intelligence (Should)

#### BF-07 · Configurable negotiation policy
- **Persona:** Admin · **Priority:** S
- **Problem:** Max proposals and offer-multiplier are compile-time constants.
- **Behavior:** Admin sets global defaults and optionally per-category/product overrides
  (e.g., electronics cap 3 proposals ×1.15; clearance ×2). New negotiations snapshot the
  effective policy at creation (consistent with existing snapshot behavior).
- **Success metric:** policy changes ship in minutes, not releases; margin leakage per category controllable.
- **Dependencies:** none technically; pairs naturally with BF-09.

#### BF-08 · Auto-decision thresholds
- **Persona:** Admin / Staff · **Priority:** S
- **Problem:** Staff hand-decide offers that are obviously fine or obviously unacceptable.
- **Behavior:** Offers ≥ auto-accept threshold (e.g., ≥90% of base) are instantly Accepted;
  below auto-decline floor are instantly Rejected (with reason); middle band stays human.
  Thresholds come from BF-07 policy.
- **Success metric:** staff touch-rate drops while acceptance rate holds.
- **Dependencies:** BF-07 (threshold source), BF-06 (rejection reason).

#### BF-09 · Negotiation analytics
- **Persona:** Admin · **Priority:** S
- **Problem:** No steering data: which products discount deepest, which staff close fastest.
- **Behavior:** Aggregate read endpoints: acceptance/rejection rates, average final discount
  vs base, proposals-per-deal, staff response times, expiry counts. Windowed (7/30/90d).
- **Success metric:** admin can name the worst-margin product and slowest queue within one query.
- **Dependencies:** BF-01/BF-02 produce the lifecycle data worth measuring.

#### BF-10 · Anti-abuse throttles
- **Persona:** Admin · **Priority:** S
- **Problem:** Nothing stops a scripted customer opening hundreds of throwaway negotiations.
- **Behavior:** Per-customer caps: max open negotiations overall (beyond per-product rule),
  max new negotiations per rolling day, minimum offer floor relative to base (lowball filter)
  with clear error codes; admin-visible abuse flags.
- **Success metric:** staff queue spam ratio trends to ~0.
- **Dependencies:** none.

### Wave 4 — Trust & growth (Could)

#### BF-11 · Post-deal rating
- **Persona:** Customer · **Priority:** C
- **Behavior:** After redeeming a Deal (BF-01), customer rates smoothness 1–5 + comment;
  aggregates shown per product/staff.
- **Metric:** rating coverage; correlation with repeat negotiations.
- **Dependencies:** BF-01.

#### BF-12 · Watchlist / saved products
- **Persona:** Customer · **Priority:** C
- **Behavior:** Save products; watchlist feed shows base-price changes and own negotiation
  states, prompting re-entry into lapsed negotiations.
- **Metric:** re-negotiation rate after price drops.
- **Dependencies:** none.

#### BF-13 · Account self-service recovery
- **Persona:** Customer · **Priority:** S (hygiene)
- **Behavior:** Password reset via emailed single-use token; email verification at registration;
  both rate-limited like existing auth endpoints.
- **Metric:** support requests for manual unlocks → 0.
- **Dependencies:** none.

#### BF-14 · Social proof feed
- **Persona:** Anonymous visitor · **Priority:** C
- **Behavior:** Public anonymized endpoint: recently struck deals (product, % off base,
  day-granular timestamp). Marketing hook for the negotiation concept itself.
- **Metric:** new registrations citing deals feed (survey proxy).
- **Dependencies:** BF-01.

---

## 6. Priority Matrix (impact vs effort)

```
        High impact
             │
   BF-01 ●   │   ● BF-04      ● BF-07
   BF-02 ●   │   ● BF-05      ● BF-09
   BF-03 ●   │   ● BF-06      ● BF-08
             │   ● BF-13      ● BF-10
 Low effort ─┼─────────────────── High effort
             │   ● BF-12
             │   ● BF-14      ● BF-11
        Low impact
```

Sequencing logic: top-left first (cheap, existential), then right-side Musts that need
Wave-1 foundations, then differentiators.

---

## 7. Explicit Non-Goals (for now)

- Full free-form chat inside negotiations (BF-06 covers the 80% case with structured notes).
- Multi-currency, tax calculation, payments processing — Deal redemption is confirmation-only;
  real payment rails are a separate product decision.
- Mobile push channels, B2B quote flows, auction-style bidding, AI-suggested prices.
- Any UI work — this remains an API-first product.

## 8. KPI Summary (what "done" moves)

| KPI | Moved by |
|---|---|
| Accepted→Purchased conversion | BF-01, BF-14 |
| Median time-per-negotiation-turn | BF-04, BF-05 |
| Open-negotiation staleness | BF-02 |
| Counter-rate after rejection | BF-03, BF-06 |
| Staff touches per closed deal | BF-08, BF-09 |
| Margin discipline (avg discount) | BF-07, BF-08 |
| Abuse/spam ratio in queue | BF-10 |
