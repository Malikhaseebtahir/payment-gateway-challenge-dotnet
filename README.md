# Instructions for candidates

This is the .NET version of the Payment Gateway challenge. If you haven't already read this [README.md](https://github.com/cko-recruitment/) on the details of this exercise, please do so now. 

## Template structure
```
src/
    PaymentGateway.Api - a skeleton ASP.NET Core Web API
test/
    PaymentGateway.Api.Tests - an empty xUnit test project
imposters/ - contains the bank simulator configuration. Don't change this

.editorconfig - don't change this. It ensures a consistent set of rules for submissions when reformatting code
docker-compose.yml - configures the bank simulator
PaymentGateway.sln
```

Feel free to change the structure of the solution, use a different test library etc.

---

# Solution

## Running it

```bash
docker-compose up                                # bank simulator on :8080
dotnet run --project src/PaymentGateway.Api      # API + swagger ui at https://localhost:7092/swagger
dotnet test                                      # no docker needed; the bank is faked in tests
```

## API

### `POST /api/payments`

```json
{ "cardNumber": "2222405343248877", "expiryMonth": 4, "expiryYear": 2027, "currency": "GBP", "amount": 1050, "cvv": "123" }
```

| outcome | http status | body |
|---|---|---|
| authorized / declined by the bank | `201 created` with a `Location` header | `id`, `status`, `cardNumberLastFour`, `expiryMonth`, `expiryYear`, `currency`, `amount` |
| rejected (invalid request bank not called) | `400 bad Request` | `{ "status": "rejected", "errors": ["..."] }` |
| bank unavailable or errored | `502 bad gateway` | `problemDetails` |

### `GET /api/payments/{id}`

Returns `200 OK` with the same fields as above, or `404 not found`.

## Structure

```
Controllers/PaymentsController       
Validation/PaymentRequestValidator   
Clients/BankClient                   
Services/PaymentsRepository          
Models/Payment                       
```

## Design decisions

- **Card data.** card number and cvv are strings so leading zeros survive and so length and digit rules can be checked. Only the last four digits are stored or returned, the full cvv never reach the repository or a response. Last four is a string so that `0123` doesn't turn into `123`.
- **Rejected payments are not stored and get no id.** no payment could be created, so they return `400` with every validation error at once. Malformed json (for example `"amount": "lots"`) uses the same `Rejected` shape so merchants only have to handle one error format.
- **Declined is a successful API call.** The bank made a decision so a payment resource is created (`201`) and can be retrieved later.
- **Bank failure (`503`, timeout, unreachable, unexpected `400`)** returns `502 Bad Gateway` and nothing is stored and the bank gave no decision so recording a status would be wrong. The merchant can safely retry.
- **Expiry.** A card is valid until the end of its expiry month so the current month is accepted. Times are compared in utc.
- **Currencies.** only three are supported: `GBP`, `USD`, `EUR`, matching is case-sensitive, because ISO 4217 codes are upper case.
- **Amount** must be a positive integer in minor units. A zero-value payment is treated as invalid.
- **Statuses** are serialised as strings (`"Authorized"`), not enum ordinals.
- **Validation** is a plain class rather than fluentValidation or dataAnnotations. The rules are few and a plain class keeps them obvious and easy to unit test.

## Testing

- `PaymentRequestValidatorTests`: boundary and negative cases for every field with a fixed clock.
- `BankClientTests`: wire format to the bank and error mapping using a stub `HttpMessageHandler`.
- `PaymentsControllerTests`: in-process API tests (`webApplicationFactory`) with a `fakeBankClient` that mirrors the simulator's odd/even/zero rules.
