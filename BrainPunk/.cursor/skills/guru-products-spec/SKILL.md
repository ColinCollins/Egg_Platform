---
name: guru-products-spec
description: Update `Assets/Guru/guru_spec.yaml` products catalog entries (IAP/subscriptions) with the expected schema (android/ios ids, attr, capabilities, method, manifest, subscriptions fields). Use when the user asks to add/modify `products:` in `guru_spec.yaml`, or mentions Guru products/IAP IDs/base_plan/offers.
---

# Guru products spec

目标：把 IAP/订阅商品，规范地写进 `Assets/Guru/guru_spec.yaml` 顶层 `products:`。

## Quick workflow

1. 打开 `Assets/Guru/guru_spec.yaml`，定位到顶层 `products:`。
2. 打开 `Assets/Game/Configs/tbproducts.json`，找到要接入的商品条目（用 `androidProductId` / `iosProductId` 对照）。
3. 为每个商品在 `guru_spec.yaml` 的 `products:` 下新增/更新一个 product key（建议用业务名：`no_ads` / `hint1` / `coin_999` 等），并填好必填字段（见下方模板）。
4. 做一次字段校验（见 “Checklist”）。

## Attr（最容易踩坑）

本项目当前 `guru_spec.yaml` 使用：

- `attr: possessive`：非消耗品（永久拥有，例如去广告）
- `attr: consumable`：消耗品（可重复购买，例如金币/提示）
- `attr: subscriptions`：订阅

你截图里的 Guru 文档示例写的是 `attr: asset`（非消耗品）。在你这个项目里可以理解为：

- **文档的 `asset` ≈ 项目的 `possessive`**

不要混用；以 `Assets/Guru/guru_spec.yaml` 现有枚举为准。

## Field schema（按项目现状）

每个 product key 推荐字段：

- `android`: string（Google Play product id）
- `ios`: string（App Store product id）
- `attr`: `possessive` / `consumable` / `subscriptions`
- `method`: 通常填 `iap`
- `manifest`:
  - `category`: string（用于归类，例如 `no_ads` / `hint` / `coin` / `sub`）
  - `ignore_sales`: boolean（可选，少数商品需要；注意是 **true/false**，不要写成 `"true"` 字符串）
  - `details`: object（可选，业务扩展字段）

订阅商品额外字段：

- `base_plan`: string（例如 `p1w`）
- `group`: string（例如 `premium`）
- `offers`: string list（例如 `- 3daytrial`）

## Example snippet

```yaml
products:
  # 非消耗品（去广告）
  no_ads:
    android: "brpk.a.iap.noads1"
    ios: "U_Shop_GoodsDes_NoAds"
    attr: possessive
    capabilities: noAds
    method: iap
    manifest:
      category: "no_ads"
      ignore_sales: true

  # 消耗品（提示）
  hint1:
    android: "brpk.a.iap.hint1"
    ios: "U_Shop_GoodsDes_Bulbs_01"
    attr: consumable
    method: iap
    manifest:
      category: "hint"

  # 消耗品（提示）
  hint2:
    android: "brpk.a.iap.hint2"
    ios: "U_Shop_GoodsDes_Bulbs_02"
    attr: consumable
    method: iap
    manifest:
      category: "hint"

  # 订阅（示例）
  hint99:
    android: "brpk.a.iap.hint99"
    ios: "U_Shop_GoodsDes_Bulbs_03"
    attr: subscriptions
    base_plan: p1w
    group: premium
    method: iap
    offers:
      - 3daytrial
    manifest:
      category: "sub"
```

## Checklist（提交前自查）

- **Key 命名**：`products:` 下的 key 用业务名，稳定可读（不要直接用价格/面额做 key）。
- **平台 ID**：`android`/`ios` 必须和商店后台一致（通常可从 `tbproducts.json` 对照）。
- **attr**：只能用项目现有的 `possessive / consumable / subscriptions`；不要写 `asset`。
- **ignore_sales 类型**：写 `true/false` 布尔值；不要写 `"true"` 字符串。
- **订阅字段齐全**：`attr: subscriptions` 时必须同时有 `base_plan`、`group`，`offers` 按需配置。
- **capabilities**：仅在确实有“能力开关”的永久商品上加（例如 `noAds`）。

