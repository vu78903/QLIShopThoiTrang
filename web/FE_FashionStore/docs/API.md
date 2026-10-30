# Product API

Public read-only endpoints. Base URL local DEV: `http://127.0.0.1:5000`.

| Method | Path | Parameters |
|---|---|---|
| GET | /api/products | q (≤100), category (≤20), audience, sort, page (1..100000), pageSize (1..100) |
| GET | /api/products/{id} | Product ID (1..20), distinct from SKU |
| GET | /api/products/{id}/variants | Product ID (1..20) |
| GET | /api/categories | Active categories |
| GET | /health/live | Server process only |
| GET | /health/ready | Database connection only |

`audience`: NAM, NU, TRE_EM, UNISEX. Omit it for all groups, including products with null DoiTuong. `sort`: new (default), price_asc, price_desc, name. Default page=1, pageSize=12.

Search matches product name or product ID using parameterized EF queries and the database's collation. Category compares MaLoai. New sorting uses NgayTao descending, then MaSP; price/name sorting also has MaSP as a stable tie-breaker. All filtering, sorting and pagination execute in SQL. Total and items are two reads, not a transaction snapshot during concurrent catalog edits.

A visible product must have active product/category and at least one active SKU. Stock=0 remains visible as sold out. Disabled SKUs are omitted. Summary price is the minimum price of active SKUs, including sold-out SKUs; stock sums active SKUs as a 64-bit value. No promotion or loyalty discount is invented in catalog prices.

Images are ordered: product-level first, then main-image flag, ThuTu, MaHinh. Images for disabled SKUs are omitted. Detail and summary use the same ordering. CreatedAt comes from SQL datetime2; no UTC suffix is invented for a timezone-free column.

## Examples

The JSON below illustrates the contract only; it is not data read from the supplied backup. The generated `openapi.v1.json` was retrieved from the running backend.

```http
GET /api/products?q=ao&audience=NAM&category=AO&sort=price_asc&page=1&pageSize=12
```

```json
{
  "items": [
    {
      "id": "EXAMPLE-SP", "name": "Sản phẩm minh họa contract",
      "category": { "id": "AO", "name": "Áo" },
      "audience": "NAM", "price": 150000, "stock": 3,
      "imageUrl": "/images/example.jpg", "createdAt": "2026-01-01T00:00:00"
    }
  ],
  "total": 1, "page": 1, "pageSize": 12
}
```

```http
GET /api/products/EXAMPLE-SP
```

```json
{
  "id": "EXAMPLE-SP", "name": "Sản phẩm minh họa contract",
  "category": { "id": "AO", "name": "Áo" },
  "audience": "NAM", "price": 150000, "stock": 3,
  "imageUrl": "/images/example.jpg", "createdAt": "2026-01-01T00:00:00",
  "description": "Ví dụ mô tả",
  "variants": [ { "sku": "EXAMPLE-SKU-M", "size": "M", "color": "Xanh", "price": 150000, "stock": 3 } ],
  "images": [ { "sku": null, "url": "/images/example.jpg" } ]
}
```

Variants returns the `variants` array directly. Categories returns `[{"id":"AO","name":"Áo"}]`. These GET requests do not have JSON request bodies. A valid empty list returns 200 with items=[], total=0; a page past the end returns items=[] with the actual total.

## Status codes

| Status | Meaning |
|---|---|
| 200 | Valid catalog response, including empty lists |
| 400 | Invalid query or route ID length |
| 404 | Product missing or not publicly visible; applies to detail and variants |
| 503 | SQL unavailable, SQL schema/query error or missing development connection configuration |
| 500 | Unexpected error |

Validation and exceptions use ProblemDetails. Unexpected/SQL errors return a short public title and traceId; stack traces remain in server logs. MVC maps missing product to 404, invalid filters to 400 and unavailable backend/invalid JSON to a 503 page with the existing error layout.

No `/api/app-state`, order creation or payment simulation is included. Account/customer/cart operations are documented in [ACCOUNT_CART.md](ACCOUNT_CART.md); catalog endpoints remain public.

## Account & Cart

See [ACCOUNT_CART.md](ACCOUNT_CART.md) for the 11 new operations, request/response examples, ownership, authentication and Windows checks. The generated openapi.v1.json includes all current endpoints.
