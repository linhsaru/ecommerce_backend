-- =========================
-- ECOMMERCE BACKEND - PostgreSQL Schema
-- =========================
-- Extensions (gen_random_uuid)
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================
-- ENUMS
-- =========================
DO $$ BEGIN
  CREATE TYPE order_status AS ENUM (
    'pending',
    'confirmed',
    'processing',
    'shipping',
    'completed',
    'cancelled',
    'refunded'
  );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE payment_status AS ENUM ('unpaid','paid','failed','refunded','partially_refunded');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE payment_method AS ENUM ('cod','bank_transfer','vnpay','momo','stripe','paypal','other');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
  CREATE TYPE shipment_status AS ENUM ('pending','ready','shipped','delivered','returned','cancelled');
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- =========================
-- USERS & AUTH
-- =========================
CREATE TABLE IF NOT EXISTS users (
  id              BIGSERIAL PRIMARY KEY,
  uuid            uuid NOT NULL DEFAULT gen_random_uuid(),
  email           text NOT NULL,
  phone           text,
  password_hash   text,
  full_name       text,
  status          int NOT NULL DEFAULT 1,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now(),
  deleted_at      timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_users_email ON users (lower(email)) WHERE deleted_at IS NULL;
CREATE UNIQUE INDEX IF NOT EXISTS ux_users_uuid  ON users (uuid);

-- =========================
-- ADDRESSES
-- =========================
CREATE TABLE IF NOT EXISTS user_addresses (
  id            BIGSERIAL PRIMARY KEY,
  user_id       bigint NOT NULL REFERENCES users(id),
  recipient     text NOT NULL,
  phone         text NOT NULL,
  line1         text NOT NULL,
  line2         text,
  ward          text,
  district      text,
  province      text,
  country       text NOT NULL DEFAULT 'VN',
  postal_code   text,
  is_default    boolean NOT NULL DEFAULT false,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  deleted_at    timestamptz
);

CREATE INDEX IF NOT EXISTS ix_user_addresses_user_id ON user_addresses(user_id);

-- =========================
-- CATALOG: BRANDS, CATEGORIES, PRODUCTS, VARIANTS
-- =========================
CREATE TABLE IF NOT EXISTS brands (
  id          BIGSERIAL PRIMARY KEY,
  name        text NOT NULL,
  slug        text NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  deleted_at  timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_brands_slug ON brands (slug) WHERE deleted_at IS NULL;

CREATE TABLE IF NOT EXISTS categories (
  id          BIGSERIAL PRIMARY KEY,
  parent_id   bigint REFERENCES categories(id),
  name        text NOT NULL,
  slug        text NOT NULL,
  sort_order  int NOT NULL DEFAULT 0,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  deleted_at  timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_categories_slug ON categories (slug) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_categories_parent_id ON categories (parent_id);

CREATE TABLE IF NOT EXISTS products (
  id              BIGSERIAL PRIMARY KEY,
  brand_id        bigint REFERENCES brands(id),
  name            text NOT NULL,
  slug            text NOT NULL,
  description     text,
  status          int NOT NULL DEFAULT 1,
  thumbnail_url   text,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now(),
  deleted_at      timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_products_slug ON products (slug) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_products_brand_id ON products (brand_id);

CREATE TABLE IF NOT EXISTS product_categories (
  product_id    bigint NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  category_id   bigint NOT NULL REFERENCES categories(id),
  PRIMARY KEY (product_id, category_id)
);

CREATE INDEX IF NOT EXISTS ix_product_categories_category_id ON product_categories(category_id);

CREATE TABLE IF NOT EXISTS product_images (
  id          BIGSERIAL PRIMARY KEY,
  product_id  bigint NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  url         text NOT NULL,
  alt         text,
  sort_order  int NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS ix_product_images_product_id ON product_images(product_id);

CREATE TABLE IF NOT EXISTS product_variants (
  id            BIGSERIAL PRIMARY KEY,
  product_id    bigint NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  sku           text NOT NULL,
  variant_name  text,
  attributes    jsonb,
  price         numeric(18,2) NOT NULL DEFAULT 0,
  compare_at    numeric(18,2),
  cost          numeric(18,2),
  weight_gram   int,
  status        int NOT NULL DEFAULT 1,
  created_at    timestamptz NOT NULL DEFAULT now(),
  updated_at    timestamptz NOT NULL DEFAULT now(),
  deleted_at    timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_product_variants_sku ON product_variants (sku) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS ix_product_variants_product_id ON product_variants(product_id);

-- =========================
-- INVENTORY
-- =========================
CREATE TABLE IF NOT EXISTS warehouses (
  id          BIGSERIAL PRIMARY KEY,
  name        text NOT NULL,
  code        text NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_warehouses_code ON warehouses(code);

CREATE TABLE IF NOT EXISTS inventory (
  warehouse_id   bigint NOT NULL REFERENCES warehouses(id),
  variant_id     bigint NOT NULL REFERENCES product_variants(id) ON DELETE CASCADE,
  quantity       int NOT NULL DEFAULT 0,
  reserved       int NOT NULL DEFAULT 0,
  updated_at     timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (warehouse_id, variant_id),
  CONSTRAINT ck_inventory_nonneg CHECK (quantity >= 0 AND reserved >= 0)
);

CREATE INDEX IF NOT EXISTS ix_inventory_variant_id ON inventory(variant_id);

-- =========================
-- CART
-- =========================
CREATE TABLE IF NOT EXISTS carts (
  id          BIGSERIAL PRIMARY KEY,
  user_id     bigint REFERENCES users(id),
  session_id  text,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_cart_owner CHECK (user_id IS NOT NULL OR session_id IS NOT NULL)
);

CREATE INDEX IF NOT EXISTS ix_carts_user_id ON carts(user_id);
CREATE INDEX IF NOT EXISTS ix_carts_session_id ON carts(session_id);

CREATE TABLE IF NOT EXISTS cart_items (
  cart_id     bigint NOT NULL REFERENCES carts(id) ON DELETE CASCADE,
  variant_id  bigint NOT NULL REFERENCES product_variants(id),
  quantity    int NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  updated_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (cart_id, variant_id),
  CONSTRAINT ck_cart_items_qty CHECK (quantity > 0)
);

CREATE INDEX IF NOT EXISTS ix_cart_items_variant_id ON cart_items(variant_id);

-- =========================
-- PROMOTION / COUPON
-- =========================
CREATE TABLE IF NOT EXISTS coupons (
  id              BIGSERIAL PRIMARY KEY,
  code            text NOT NULL,
  name            text,
  discount_type   text NOT NULL,
  discount_value  numeric(18,2) NOT NULL,
  min_order_value numeric(18,2) NOT NULL DEFAULT 0,
  max_discount    numeric(18,2),
  usage_limit     int,
  usage_count     int NOT NULL DEFAULT 0,
  start_at        timestamptz,
  end_at          timestamptz,
  status          int NOT NULL DEFAULT 1,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_coupons_code ON coupons (upper(code));

-- =========================
-- ORDERS (must be created before coupon_redemptions for FK order_id)
-- =========================
CREATE TABLE IF NOT EXISTS orders (
  id                 BIGSERIAL PRIMARY KEY,
  order_no           text NOT NULL,
  user_id            bigint REFERENCES users(id),
  status             order_status NOT NULL DEFAULT 'pending',
  payment_status     payment_status NOT NULL DEFAULT 'unpaid',
  subtotal_amount    numeric(18,2) NOT NULL DEFAULT 0,
  discount_amount    numeric(18,2) NOT NULL DEFAULT 0,
  shipping_amount    numeric(18,2) NOT NULL DEFAULT 0,
  total_amount       numeric(18,2) NOT NULL DEFAULT 0,
  currency           text NOT NULL DEFAULT 'VND',
  coupon_id          bigint REFERENCES coupons(id),
  note               text,
  ship_recipient     text NOT NULL,
  ship_phone         text NOT NULL,
  ship_line1         text NOT NULL,
  ship_line2         text,
  ship_ward          text,
  ship_district      text,
  ship_province      text,
  ship_country       text NOT NULL DEFAULT 'VN',
  ship_postal_code   text,
  created_at         timestamptz NOT NULL DEFAULT now(),
  updated_at         timestamptz NOT NULL DEFAULT now(),
  cancelled_at       timestamptz,
  completed_at       timestamptz
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_orders_order_no ON orders(order_no);
CREATE INDEX IF NOT EXISTS ix_orders_user_id ON orders(user_id);
CREATE INDEX IF NOT EXISTS ix_orders_status ON orders(status);
CREATE INDEX IF NOT EXISTS ix_orders_created_at ON orders(created_at);

-- =========================
-- COUPON REDEMPTIONS (references orders)
-- =========================
CREATE TABLE IF NOT EXISTS coupon_redemptions (
  id          BIGSERIAL PRIMARY KEY,
  coupon_id   bigint NOT NULL REFERENCES coupons(id),
  user_id     bigint REFERENCES users(id),
  order_id    bigint REFERENCES orders(id),
  created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_coupon_redemptions_coupon_id ON coupon_redemptions(coupon_id);
CREATE INDEX IF NOT EXISTS ix_coupon_redemptions_user_id ON coupon_redemptions(user_id);

CREATE TABLE IF NOT EXISTS order_items (
  id            BIGSERIAL PRIMARY KEY,
  order_id      bigint NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
  product_id    bigint NOT NULL REFERENCES products(id),
  variant_id    bigint NOT NULL REFERENCES product_variants(id),
  sku           text NOT NULL,
  name          text NOT NULL,
  variant_name  text,
  unit_price    numeric(18,2) NOT NULL,
  quantity      int NOT NULL,
  line_total    numeric(18,2) NOT NULL,
  created_at    timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_order_items_qty CHECK (quantity > 0)
);

CREATE INDEX IF NOT EXISTS ix_order_items_order_id ON order_items(order_id);
CREATE INDEX IF NOT EXISTS ix_order_items_variant_id ON order_items(variant_id);

-- =========================
-- PAYMENTS
-- =========================
CREATE TABLE IF NOT EXISTS payments (
  id              BIGSERIAL PRIMARY KEY,
  order_id        bigint NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
  method          payment_method NOT NULL,
  status          payment_status NOT NULL DEFAULT 'unpaid',
  amount          numeric(18,2) NOT NULL,
  provider        text,
  provider_txn_id text,
  paid_at         timestamptz,
  raw_payload     jsonb,
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_payments_order_id ON payments(order_id);
CREATE INDEX IF NOT EXISTS ix_payments_provider_txn_id ON payments(provider_txn_id);

-- =========================
-- SHIPMENTS
-- =========================
CREATE TABLE IF NOT EXISTS shipments (
  id               BIGSERIAL PRIMARY KEY,
  order_id         bigint NOT NULL REFERENCES orders(id) ON DELETE CASCADE,
  status           shipment_status NOT NULL DEFAULT 'pending',
  carrier          text,
  tracking_no      text,
  shipped_at       timestamptz,
  delivered_at     timestamptz,
  shipping_fee     numeric(18,2) NOT NULL DEFAULT 0,
  raw_payload      jsonb,
  created_at       timestamptz NOT NULL DEFAULT now(),
  updated_at       timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_shipments_order_id ON shipments(order_id);
CREATE INDEX IF NOT EXISTS ix_shipments_tracking_no ON shipments(tracking_no);

-- =========================
-- REVIEWS
-- =========================
CREATE TABLE IF NOT EXISTS product_reviews (
  id          BIGSERIAL PRIMARY KEY,
  product_id  bigint NOT NULL REFERENCES products(id) ON DELETE CASCADE,
  user_id     bigint REFERENCES users(id),
  rating      int NOT NULL,
  title       text,
  content     text,
  status      int NOT NULL DEFAULT 1,
  created_at  timestamptz NOT NULL DEFAULT now(),
  CONSTRAINT ck_reviews_rating CHECK (rating >= 1 AND rating <= 5)
);

CREATE INDEX IF NOT EXISTS ix_reviews_product_id ON product_reviews(product_id);
CREATE INDEX IF NOT EXISTS ix_reviews_user_id ON product_reviews(user_id);
