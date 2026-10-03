# Local Stripe embedded checkout

Configure the Products API server credentials privately from the repository root:

```powershell
dotnet user-secrets set "Stripe:ApiKey" "YOUR_SANDBOX_SERVER_KEY" --project src/ProductsApi
dotnet user-secrets set "Stripe:WebhookSigningSecret" "YOUR_LOCAL_WHSEC_SECRET" --project src/ProductsApi
```

The ecommerce client also needs the sandbox publishable key (`pk_test_...`) in its
public client configuration. Never put the server key (`sk_...`) or webhook signing
secret (`whsec_...`) in the browser application.

Stop the API before rebuilding. Apply the generated `AddStripePayments` migration
using your normal migration workflow, or from `src/ProductsApi`:

```powershell
dotnet ef database update --configuration Release
```

The design-time database factory uses appsettings and environment configuration.
Point it at your intended local database using `ConnectionStrings__DefaultConnection`.
The migration has been generated and verified, but it is not applied automatically.

Then start the API and listener in separate terminals:

```powershell
dotnet run --project src/ProductsApi --launch-profile ProductsApi
stripe.cmd listen --events checkout.session.completed,checkout.session.async_payment_succeeded,checkout.session.async_payment_failed,checkout.session.expired --forward-to http://localhost:57476/api/payments/stripe/webhook
```

## Purchase flow

Keep the order authorization policies enabled. For manual local calls, generate a
short-lived token signed by the ECommerce development key. Use the authenticated
customer GUID returned by ECommerce `GET /api/auth/me`:

```powershell
$token = dotnet run --project scripts/ECommerceDevelopmentToken -- <customer-guid>
$headers = @{ Authorization = "Bearer $token" }
```

The helper reads `DownstreamTokens` from the sibling ECommerce API's development
configuration and user-secrets store. It prints only the five-minute token. Pass an
explicit ECommerce API project directory as the second argument when the repositories
are not siblings. The token includes the local cart, address, and order roles/scopes,
so it can be pasted into Swagger's **Authorize** dialog as well.

1. Use authenticated `POST /api/orders` with `addressId` and `cartVersion`.
   This validates current catalog prices, snapshots the order, and clears the cart.
2. With the same customer token and `orders:write`, call
   `POST /api/orders/{orderId}/checkout` with no body.
3. The API creates or reuses the order's active payment attempt and returns
   `orderId`, `sessionId`, and `clientSecret`.
4. The ecommerce initializes Stripe.js with its publishable key, passes the client
   secret to Embedded Checkout, and mounts the payment form inside its checkout page.
5. Pay using sandbox card `4242 4242 4242 4242`, a future expiry, and any
   three-digit CVC.
6. When the embedded form completes, the ecommerce can show a processing state and
   poll `GET /api/orders/{orderId}`. The signed webhook remains authoritative and
   should change the order to `paymentStatus: Paid`, set `paidAtUtc`, and change
   `status` to `Confirmed`.

The API creates sessions with `ui_mode=embedded_page`,
`redirect_on_completion=never`, and card as the only payment method. This keeps the
browser on the ecommerce site. The checkout endpoint does not return a Stripe-hosted
URL, and `Stripe:SuccessUrl` and `Stripe:CancelUrl` are no longer used.

Treat the returned client secret as sensitive. Return it only to the authenticated
customer who owns the order, do not log it, and do not store it in browser storage.
It is expected to reach the browser so Stripe.js can mount the session.

Checkout supports BRL, USD, EUR, and GBP. It charges the saved order grand total as
one order line. Stripe catalog products do not need to be created first.

## Payment attempts and retries

Each checkout cycle has a durable `PaymentAttempts` row. Only one unfinished
attempt can exist for an order. The row snapshots the amount and currency and
stores the Stripe session ID, status, failure code, and timestamps.

The API commits a new attempt before calling Stripe. Its public ID creates the
stable provider key `stripe-checkout-{paymentAttemptId}`. Stripe calls happen
outside SQL transactions. If Stripe creates a session but its response is lost,
or saving that response fails, retrying checkout uses the same attempt and key.

An attempt without a recorded provider session is automatically retryable for
23 hours. After that window the API requires manual reconciliation because
Stripe only guarantees idempotency-key retention for a limited period.

Open sessions are reused. Webhook failure or expiration completes the attempt,
allowing the next checkout request to create a new numbered attempt for the same
order. A paid attempt confirms the order. Repeated and stale events cannot undo
a paid state or reset shipment progress.

Verified events unrelated to this API, including default CLI fixtures without
both `order_id` and `payment_attempt_id` metadata, return 200 without changing
orders. Thus `stripe.cmd trigger checkout.session.completed` tests delivery,
not a real order payment.

Missing signing configuration, mismatched linked sessions, and temporary
processing failures are retryable errors. Invalid signatures return 400.

## Secrets and deployment

Do not commit credentials or paste them into chat. User Secrets are unencrypted
local development storage outside the repository. Existing database and JWT
configuration is still required. Restart after changing secrets.

In Azure, the Products API needs these application settings:

- `Stripe__ApiKey`: the environment's server secret key (`sk_test_...` or `sk_live_...`).
- `Stripe__WebhookSigningSecret`: the signing secret for the matching deployed
  webhook destination (`whsec_...`).

The ecommerce deployment needs the matching publishable key (`pk_test_...` or
`pk_live_...`) in its public environment configuration. The publishable key is
safe to expose to the browser. Use sandbox and live credentials consistently;
keys and webhook signing secrets cannot be mixed between environments.

The older `Stripe__SuccessUrl` and `Stripe__CancelUrl` settings can be removed after
deploying this version because the Products API no longer reads them.

## Validation

Unit tests cover signatures, monetary conversion, attempt transitions, stale
events, and paid-state preservation. The SQL Server integration scenario covers
ambiguous Stripe responses, idempotency-key reuse, ownership, amount mismatch,
session persistence, embedded session options, and durable payment confirmation
through a fake Stripe gateway. It requires Docker and `RUN_TESTCONTAINERS=true`;
no Stripe credentials are used.
