# Checkout, inventory, and locking safety

This document is the internal contract for cart-to-payment processing. It records
the invariants that must remain true when checkout, inventory, or Stripe code is
changed.

## Executive summary

- Adding an item to a cart does not reserve stock.
- Creating an order snapshots current price, customer, address, and line data, but
  still does not reserve stock.
- Opening Stripe Checkout creates a database inventory lease.
- Available stock is on-hand stock minus unexpired lease quantities.
- A lease defaults to 30 minutes and Stripe receives the same absolute expiry.
- Expired leases stop affecting stock without a webhook or cleanup job.
- Checkout creation, session recording, and webhook settlement serialize on one
  canonical order-payment application lock.
- Product contention serializes on the physical ProductInventories row.
- A paid webhook consumes stock once; terminal unpaid webhooks remove lease rows.

## Application-lock registry

There are currently exactly two SQL application-lock namespaces.

| Protected resource | Canonical resource name | Owner | Callers |
| --- | --- | --- | --- |
| One shopper cart | `cart:{SHA256(exact subject)}` | SQL transaction | Cart set, remove, clear, and order creation |
| One order payment lifecycle | `stripe-order:{publicOrderGuid:D}` | SQL transaction | Checkout preparation, Stripe session recording, and Stripe webhook processing |

The source audit on October 5, 2026 found only two direct calls to
`sys.sp_getapplock`: `CartLockManager` and `OrderPaymentLock`.

### Order-payment lock invariant

Every operation that can change an order's payment attempt, payment status, order
status, or inventory settlement must call:

```csharp
OrderPaymentLock.AcquireAsync(orderPublicId, cancellationToken)
```

The identifier is always `Order.PublicId`, formatted as a `D` GUID. It must
never be replaced by:

- the internal numeric `Order.Id`;
- a payment-attempt ID;
- a Stripe session ID;
- an endpoint path fragment assembled independently;
- a differently prefixed or differently formatted order GUID.

The checkout route supplies the public order GUID. The same GUID is written into
Stripe's `client_reference_id` and `order_id` metadata. The webhook parses that
metadata and passes the same public GUID to `OrderPaymentLock`. Consequently,
checkout preparation, session persistence, and webhook application currently
compete for exactly the same SQL application lock.

### What the application lock prevents

- Two requests creating active payment attempts for the same order.
- Checkout preparation racing with a payment webhook.
- Session-ID persistence racing with a terminal event.
- Two API instances relying on unrelated in-process locks.
- A stale failure updating the order concurrently with a success event.

The lock is exclusive, owned by the SQL transaction, has a 10-second acquisition
timeout, and is automatically released on commit, rollback, or transaction disposal.
The Stripe network call is deliberately made after committing the preparation
transaction so the database lock is not held across external I/O.

### How to make lock naming resistant to future developer mistakes

The current implementation is internally consistent, but consistency is enforced
by code review rather than an architectural test. It is therefore not accurate to
call it completely regression-proof.

The recommended hardening is:

1. Create one low-level SQL application-lock service.
2. Create one typed resource-name factory with methods such as
   `ForCart(subject)` and `ForOrderPayment(orderPublicId)`.
3. Keep resource prefixes private to that factory.
4. Prohibit direct `sp_getapplock` calls outside the low-level service.
5. Add an architectural test that scans production source and fails if another
   direct `sp_getapplock` call appears.
6. Add unit tests asserting that checkout preparation, session recording, and
   webhook handling derive the same resource name from one order public ID.
7. Continue using transaction-owned locks and the same database principal.
8. Treat changing a lock name as a coordinated migration, because old and new
   application versions may run concurrently during deployment.

Until that hardening is implemented, all reviewers must search for
`sp_getapplock`, `stripe-order:`, and `cart:` when payment or cart locking is
changed.

## Product inventory contention

Inventory reservation does not use an application-lock string. For each order,
products are processed in ascending product ID order. A conditional update touches
the corresponding ProductInventories row and therefore takes a physical SQL update
lock for that product.

The condition is conceptually:

```sql
OnHand
- SUM(Quantity)
  FROM InventoryReservations
  WHERE ProductId = @productId
    AND ExpiresAtUtc > @utcNow
>= @requestedQuantity
```

A competing checkout for the same product waits on the same physical row and then
re-evaluates the availability predicate after the first transaction completes.
Processing IDs in a stable order reduces deadlock risk for multi-product orders.
If any line cannot be reserved, the transaction rolls back all lines.

## Lease and Stripe expiration

`Inventory:ReservationMinutes` controls the lease lifetime and defaults to 30.
The valid range is 30 through 1440 minutes because Stripe Checkout Sessions accept
an `expires_at` value from 30 minutes through 24 hours after creation. See the
[Stripe Checkout Session API](https://docs.stripe.com/api/checkout/sessions/create#create_checkout_session-expires_at).

The API performs both changes from one value:

1. It stores the calculated absolute UTC deadline in every InventoryReservations
   row for the order.
2. It sends that exact deadline as Stripe Checkout's `expires_at`.

There is no separate Stripe Dashboard expiration setting to update. Deployment
configuration can override the default with:

```text
Inventory__ReservationMinutes=30
```

The same stored deadline is reused during idempotent retries. Generating a new
deadline on each retry would change the Stripe request associated with the same
idempotency key and is forbidden.

### Why both deadlines exist

The database deadline controls when stock becomes sellable again. It does not wait
for `checkout.session.expired`, so missed webhooks cannot hold stock indefinitely.

The Stripe deadline prevents a customer from successfully paying through an old
session after the database has made its stock available to somebody else.

Terminal webhooks still update payment-attempt state and physically remove
reservation rows, but removal is housekeeping rather than the availability
mechanism.

## Recommended production expiration

Thirty minutes is the recommended default for the current card-only embedded
checkout:

- it is Stripe's minimum Checkout Session lifetime;
- it is normally enough time to enter payment details and complete authentication;
- it minimizes abandoned-stock hold time;
- a longer promotion does not itself require a longer checkout lease.

Use 45 to 60 minutes only when the checkout experience demonstrably needs more
time, for example accessibility requirements or unusually long customer input.
Longer leases directly increase how long abandoned scarce stock is unavailable.

The important variable for a promotion is scarcity, not whether the promotion lasts
one hour or an entire weekend.

## Last-item behavior during a sale

Yes, a real customer can temporarily lose access to the last item because another
customer opened checkout and abandoned it.

Example:

1. The promotion ends Sunday at 23:59.
2. Customer A reserves the final unit at 23:58.
3. Customer B sees no available stock.
4. Customer A does nothing.
5. The lease ends around Monday at 00:28.

If the sale ends strictly at 23:59, Customer B never gets another opportunity at
the promotional price. This is the unavoidable tradeoff of reserving the final unit
before payment: the system favors the customer already in checkout over a later
customer who might pay faster.

The current order also snapshots its amount. Therefore, unless promotion-specific
rules are added, a customer who starts checkout before the sale deadline can pay the
snapshotted price until that checkout session expires.

### Choose and publish one sale policy

For ordinary weekend sales, the recommended policy is:

- keep the 30-minute lease;
- state that beginning checkout reserves stock and price for up to 30 minutes;
- display a countdown based on the server-provided expiry;
- stop presenting an expired client secret and request a fresh checkout;
- optionally notify waiting customers when stock becomes available again.

For a strict sale deadline, choose one of these policies:

1. **Checkout grace period:** customers who start checkout before closing receive
   the full 30 minutes to pay. This fits the current implementation.
2. **Strict payment deadline:** stop creating promotional checkout sessions at
   least 30 minutes before the advertised payment cutoff, or use a different
   payment design. Stripe Checkout cannot be configured below 30 minutes.

Trying to create a session two minutes before a strict deadline and locally
expiring it at the deadline is unsafe: Stripe could still accept payment for the
remainder of its minimum session lifetime.

### Anti-hoarding controls recommended for scarce promotions

The current system limits cart quantities but does not yet enforce one active
reservation per customer/product across multiple orders, and checkout endpoints do
not yet have a dedicated rate-limit policy. For high-demand stock, add:

- per-customer and per-product purchase limits;
- a limit on concurrent active checkout leases per customer;
- a dedicated checkout-creation rate limit;
- bot and account-abuse controls;
- an explicit cancel action that expires the Stripe session before releasing early;
- a waitlist or back-in-stock notification for the final units.

An early cancel must invalidate the Stripe session first. Releasing the database
lease while Stripe can still accept payment recreates the late-payment oversell race.

## End-to-end state transitions

| Event | Inventory effect | Payment effect |
| --- | --- | --- |
| Add to cart | None | None |
| Create order | Revalidate only | Pending order snapshot |
| Open checkout | Create unexpired lease | Create/reuse active attempt and Stripe session |
| Close browser | Lease remains until deadline | Session remains open until the same deadline |
| Lease deadline passes | Lease stops reducing availability automatically | Await terminal bookkeeping |
| Payment succeeds | Atomically decrement OnHand and delete lease | Attempt paid; order paid and confirmed |
| Payment fails or session expires | Delete lease row | Latest attempt failed or expired |
| Duplicate success webhook | No second decrement | Paid state remains paid |
| Stale terminal webhook | Must not release a newer lease | Must not override a newer or paid attempt |

## Required regression tests

Changes to this area should retain tests for:

- two concurrent checkouts competing for one final unit;
- expired reservation ignored without an expiration webhook;
- lease deadline equal to Stripe `expires_at`;
- repeated paid webhook consumes stock once;
- expired/failed webhook removes the reservation;
- stale terminal event cannot undo paid state;
- amount, currency, metadata, client reference, and session mismatch rejection;
- ambiguous Stripe response retried with the same idempotency key;
- unauthorized customer cannot open another customer's checkout;
- all order-payment paths producing the same application-lock resource.

## Known boundary

Stripe webhooks remain authoritative for marking an order paid. A payment completed
immediately before expiry can have a webhook delivered just after expiry. Settlement
therefore performs another atomic inventory check and never makes stock negative or
steals inventory held by another active lease. If no stock remains, processing fails
safely and the payment requires reconciliation.

Eliminating this boundary completely requires an authorization-then-capture payment
flow: authorize the card, claim inventory, and capture only after the claim succeeds.

## Relevant implementation files

- `Features/Cart/CartLockManager.cs`
- `Payments/OrderPaymentLock.cs`
- `Features/Inventory/InventoryReservationService.cs`
- `Features/Inventory/ProductAvailabilityService.cs`
- `Features/Orders/CreateCheckoutSession/CreateCheckoutSessionHandler.cs`
- `Payments/StripeWebhookProcessor.cs`
- `Controllers/StripeWebhookController.cs`
