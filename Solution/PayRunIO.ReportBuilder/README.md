# PayRunIO.ReportBuilder

A standalone Blazor (interactive server) web application that lets end users build custom tabular
payroll reports with AI assistance, execute them against a PayRun.io API instance using their own
credentials, and download the results.

## How it works

* **Sign in** — users are funnelled through the KeyCloak OAuth 2 authorization code flow (with
  PKCE). The KeyCloak access/refresh tokens are held server side (`IUserTokenStore`) keyed by the
  user's subject claim, and the access token is used as the bearer credential on API calls. Tokens
  are refreshed automatically against the KeyCloak token endpoint when they near expiry.
* **AI chat** — the chat panel drives the shared `PayRunIO.RqlAssistant.Service` pipeline
  (`ResponseType.TabularQuery`), so the model grounds itself with the schema/route/grammar tools
  and validates queries before replying. The last ```xml block in each reply becomes the current
  report query. Each turn also passes the current query and last execution error as context, so
  "fix it" style prompts work.
* **Standard / advanced mode** — the designer runs in two modes, toggled from the report pane
  header. Standard mode (the default) keeps the RQL behind the scenes: the assistant still builds
  and amends the query, but the user only sees the conversation, parameters and results (fenced
  xml blocks in assistant replies are replaced with a short note). Advanced mode shows the raw RQL
  in an editable query pane. The last selection is persisted per browser (localStorage,
  `payrun.reportbuilder.mode.v1`) and restored on the next visit.
* **Save (private) vs publish (public)** — saving a report writes it to the user's own browser
  storage (`LocalReportStore`, localStorage key `payrun.reportbuilder.reports.v1`), giving each user
  a private collection that no one else can see. Publishing is the separate, explicit step that
  stores the report in the PayRun.io account as a `ReportDefinition` via `ReportDefinitionService`
  (`ReportBuilder-` title prefix) — those definitions are **visible to everyone**, so the action is
  only offered in advanced mode and carries a note saying so.
  Publishing also saves locally and records the published id on the local copy, so re-publishing
  updates the same definition rather than creating a duplicate. The Reports page lists the two
  collections separately; local reports can be deleted from there.
* **Common reports** — `CommonReportCatalog` seeds the report list; add templates there to grow it.
* **Execute & download** — queries are POSTed to `/Query` on the configured API endpoint with the
  user's token. Responses following the tabular output pattern (`Table`/`Headers`/`Rows`) render
  as a grid with CSV download; any other shape falls back to a raw XML view. Root-level query
  `<Variables>` are surfaced as editable "report parameters".

## Configuration (`appsettings.json`, environment variables or user secrets)

| Key | Purpose |
| --- | --- |
| `KeyCloak:Authority` | Realm URL, e.g. `https://auth.dev.payrun.io/realms/payescape` |
| `KeyCloak:ClientId` / `KeyCloak:ClientSecret` | Confidential client used for the code flow and token refresh |
| `KeyCloak:Scopes` | Defaults to `openid offline_access profile email` |
| `PayRunApi:EndPoint` | The single API instance all reports execute against |
| `OpenAI:Provider` | `OpenAI` (Chat Completions), `OpenAI (Responses)` (Responses API) or `Anthropic` |
| `OpenAI:ApiKey` / `OpenAI:Endpoint` / `OpenAI:Model` / `OpenAI:Temperature` | AI provider settings (same keys as the desktop QueryBuilder) |
| `OpenAI:ReasoningEffort` | Optional (`none`/`minimal`/`low`/`medium`/`high`) for OpenAI reasoning models (GPT-5 family). These models need the `OpenAI (Responses)` provider for tool use — or `ReasoningEffort=none` on the standard provider. When set, `Temperature` is not sent |

Secrets (client secret, API key) should be supplied via user secrets or environment variables
(`KeyCloak__ClientSecret`, `OpenAI__ApiKey`), not committed to `appsettings.json`.

## KeyCloak client requirements

The client must allow this app's redirect URIs (defaults shown for the dev launch profile):

* Redirect URI: `https://localhost:7171/signin-oidc`
* Post-logout redirect URI: `https://localhost:7171/signout-callback-oidc`

Either add these to the existing `payescape-gateway` client per environment, or register a
dedicated confidential client for the report builder.

## Notes / current limitations

* The token store is in-memory: an app restart requires users to sign in again (they are prompted
  automatically when a report run finds no token). Swap `InMemoryUserTokenStore` for a distributed
  implementation if you scale out or need restart resilience.
* Excel export is not yet implemented — CSV and raw XML downloads are available; CSV opens
  directly in Excel.
* Saved reports live in browser localStorage, so a user's private collection does not follow them
  to another browser, device or profile, and clearing site data deletes it. Publishing is the only
  way to move a report off the machine today; a per-user server-side store would be the next step
  if reports need to roam.
