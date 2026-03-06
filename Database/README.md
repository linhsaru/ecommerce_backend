# Database (PostgreSQL)

## Schema

Chay file `schema.sql` tren PostgreSQL de tao database va cac bang.

Thu tu tao bang da duoc dieu chinh: bang `orders` duoc tao truoc bang `coupon_redemptions` de dam bao foreign key `order_id` hop le.

## Ket noi

Trong `appsettings.Development.json` (hoac appsettings.json), dat `ConnectionStrings:DefaultConnection` voi chuoi ket noi PostgreSQL, vi du:

```
Host=localhost;Port=5432;Database=ecommerce;Username=postgres;Password=your_password
```

Sau khi chay schema.sql, khoi dong API va goi cac endpoint REST (Products, Categories) de kiem tra.
