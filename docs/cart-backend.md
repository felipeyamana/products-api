# Cart backend

Products API owns cart storage and rules. ECommerce authenticates shoppers and forwards requests; it must not store or calculate the authoritative cart.

## Authentication contract

ECommerce signs short-lived shopper tokens using RSA/RS256 and its private key. Products API validates them using only the corresponding public key. The token must use the configured issuer, audience, and `kid`, carry the authenticated shopper ID in `sub`, and include the `CartUser` role. Reads require the `cart:read` scope; mutations require `cart:write`.

Send this JWT as `Authorization: Bearer ...` for cart requests. Take the subject from the authenticated ECommerce session, never request parameters or browser headers. Generic ProductManager/Admin service tokens use a separate HMAC authentication scheme and cannot access carts. Shopper IDs are case-sensitive, 1–200 characters, without surrounding whitespace, and must be stable and globally unique across callers. No local Identity foreign key is created. Keep ECommerce's private key and JWTs on the ECommerce server. Preserve ECommerce's cookie authentication and antiforgery checks on browser mutations; Products API uses bearer authentication.

Configure Products API through environment or App Service settings:

```text
ECommerceJwt__Issuer=ecommerce-api
ECommerceJwt__Audience=products-api
ECommerceJwt__PublicKey=<base64 SubjectPublicKeyInfo public key>
ECommerceJwt__KeyId=ecommerce-development-1
```

The existing `Jwt` section and `/api/auth/token` remain responsible for Products API's own ProductManager/Admin service tokens. They do not issue shopper tokens.

## Endpoints

All successful operations return the current cart with `version`, `items`, `totalQuantity`, `currency`, and `subtotal`.

| Method | Route | Input |
| --- | --- | --- |
| GET | `/api/cart` | None |
| PUT | `/api/cart/items/{productId}` | `{"quantity":2,"version":"<cart version>"}` |
| DELETE | `/api/cart/items/{productId}?version=<cart version>` | None |
| DELETE | `/api/cart?version=<cart version>` | None |

Read first. An untouched cart has version `00000000-0000-0000-0000-000000000000`. Every actual cart change replaces its version. Missing versions/invalid quantities return 400; stale versions return 409. On 409, refresh before offering a retry. PUT sets an absolute quantity, never increments. A replay with an old version cannot apply twice. Removing an absent item with the current version succeeds without changing the version. Versions protect the entire cart, including clear and concurrent first additions. `CartLockManager` owns the lock namespace and creates one transaction-scoped application lock per shopper, so operations on the same cart are serialized across API instances while different shoppers can proceed concurrently. The transaction uses read-committed isolation; the application lock supplies cart-level serialization without holding serializable range locks on catalog data. SQL Server's execution strategy handles transient transaction failures.

## Rules

- At most 99 units per line and 50 distinct products. Quantity zero is invalid; use DELETE.
- Unique owner/product pairs. A Carts row stores the version and remains after clearing to reject stale requests.
- First addition saves `UnitPriceAtAddition`, `CurrencyAtAddition`, and timestamps. Quantity edits preserve the snapshot. Removing then re-adding takes a new snapshot.
- Use the latest catalog price ordered by CapturedAt then ID. `ActualPrice` is the current selling price, matching the existing product contract; `DiscountPrice` is the existing list-price field, not a value to subtract or substitute.
- Server-calculated decimal line totals use current prices. Compare current price/currency with the original snapshot through `priceChanged`; the snapshot does not lock a price or discount.
- Reject additions/quantity updates for missing, inactive, unpriced, invalid-currency, or insufficient-stock products. Currency codes must be three ASCII letters. Zero prices are allowed, negative prices are not.
- Enforce one currency, including original snapshots and available current prices. If a currency changes, remove the affected lines before re-adding in the desired currency.
- Retain saved items when products disappear, become inactive, lose valid pricing, or no longer have enough available stock. Such lines are unavailable. No product FK intentionally, so deleting a product does not delete cart items.
- Return a null subtotal if any item is unavailable or current currencies are mixed; never present a partial/mixed total as payable. Empty carts have subtotal zero.
- Database failures fail the request, never return an empty cart or delete saved items.
- Guest carts and frontend wiring are deferred. Cart totals exclude shipping, tax, and discounts outside catalog pricing. Order creation revalidates current stock, and Stripe checkout atomically reserves it. Successful payment consumes the reservation; the latest failed or expired payment attempt releases it for a later retry.

## Apply the migration

No application database migration was run during implementation. Configure `ConnectionStrings:DefaultConnection` in your local appsettings (or `ConnectionStrings__DefaultConnection`). Development settings override base settings.

From the repository root, run:

```powershell
Set-Location src/ProductsApi
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update
```

Run inside the API directory because the existing design-time factory loads appsettings from the working directory. `AddCarts` creates only Carts and CartItems; EF applies any outstanding earlier catalog migrations first. Migration generation does not require a live connection. EF tools 10.x are recommended to match the project's runtime.

## Verification

```powershell
dotnet test ProductsApi.sln
# With Docker available:
$env:RUN_TESTCONTAINERS = 'true'
dotnet test ProductsApi.sln
```

Cart and catalog SQL tests share the existing disposable SQL Server Testcontainer and reset its database between tests. They never read the application connection string. Without the opt-in flag, database-backed test bodies return without connecting to SQL Server.
