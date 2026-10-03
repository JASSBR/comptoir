# Architecture Decision Records

Each record captures one decision, the context that forced it, and what it costs. Format: [MADR-lite](https://adr.github.io/madr/).

| # | Decision | Status |
|---|----------|--------|
| [0001](0001-strangler-fig-not-rewrite.md) | Strangler fig behind a facade, not a rewrite | Accepted |
| [0002](0002-legacy-left-untouched.md) | The legacy keeps running as it is (one endpoint added, three defects fixed) | Accepted |
| [0003](0003-shared-database-during-transition.md) | One database for both applications during the transition | Accepted |
| [0004](0004-pricing-parity-by-differential-testing.md) | Pricing parity proven by differential tests, quirks included | Accepted |
| [0005](0005-auth-bridge-at-the-facade.md) | The legacy session becomes a token at the facade | Accepted |
| [0006](0006-shadow-traffic-before-switching.md) | Shadow traffic before switching a route | Accepted |
| [0007](0007-same-contract-first.md) | Same contract first, new endpoints second | Accepted |
| [0008](0008-hosting-and-local-development.md) | Hosting: IIS for the legacy, containers for the new side | Accepted |
