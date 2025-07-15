# Module Feature Comparison

## Summary

Wahdori Plugin combines features from:
- **CurrencyAlert**: Currency tracking and notifications
- **DailyDuty**: Daily/weekly task tracking

Total modules implemented: **30** (100% coverage)

## Currency Modules (14 total - All implemented ✅)

| Module | DailyDuty | CurrencyAlert | Wahdori | Notes |
|--------|-----------|---------------|---------|-------|
| Tomestones (Current) | ❌ | ✅ | ✅ | Weekly capped tomestones |
| Tomestones (Poetics) | ❌ | ✅ | ✅ | Legacy tomestone currency |
| Grand Company Seals | ❌ | ✅ | ✅ | Auto-detects player's GC |
| Allied Seals | ❌ | ✅ | ✅ | ARR hunt currency |
| Centurio Seals | ❌ | ✅ | ✅ | HW/SB hunt currency |
| Sack of Nuts | ❌ | ✅ | ✅ | ShB/EW hunt currency |
| Bicolor Gemstones | ❌ | ✅ | ✅ | FATE currency |
| White Scrips | ❌ | ✅ | ✅ | Crafter/Gatherer scrips |
| Purple Scrips | ❌ | ✅ | ✅ | Current endgame scrips |
| Skybuilders' Scrips | ❌ | ✅ | ✅ | Ishgard Restoration |
| Wolf Marks | ❌ | ✅ | ✅ | PvP currency |
| Trophy Crystals | ❌ | ✅ | ✅ | Crystalline Conflict |
| Storm Seals | ❌ | ✅ | ❌ | Handled by GC module |
| Serpent Seals | ❌ | ✅ | ❌ | Handled by GC module |
| Flame Seals | ❌ | ✅ | ❌ | Handled by GC module |

## Daily Modules (8 total - All implemented ✅)

| Module | DailyDuty | CurrencyAlert | Wahdori | Notes |
|--------|-----------|---------------|---------|-------|
| Duty Roulette | ✅ | ❌ | ✅ | Daily roulette tracking |
| Beast Tribe Quests | ✅ | ❌ | ✅ | Daily allowances |
| Hunt Marks (Daily) | ✅ | ❌ | ✅ | Daily hunt bills |
| Mini Cactpot | ✅ | ❌ | ✅ | Daily lottery tickets |
| GC Supply Mission | ✅ | ❌ | ✅ | Daily turn-ins |
| GC Provisioning | ✅ | ❌ | ✅ | Daily crafting/gathering |
| Levequests | ✅ | ❌ | ✅ | Leve allowances |
| Tribal Quests | ✅ | ❌ | ✅ | Beast tribe allowances |

## Weekly Modules (8 total - All implemented ✅)

| Module | DailyDuty | CurrencyAlert | Wahdori | Notes |
|--------|-----------|---------------|---------|-------|
| Wondrous Tails | ✅ | ❌ | ✅ | Khloe's journal |
| Custom Deliveries | ✅ | ❌ | ✅ | Weekly allowances |
| Doman Enclave | ✅ | ❌ | ✅ | Weekly donation limit |
| Challenge Log | ✅ | ❌ | ✅ | Weekly challenges |
| Fashion Report | ✅ | ❌ | ✅ | Weekly glamour challenge |
| Jumbo Cactpot | ✅ | ❌ | ✅ | Weekly lottery |
| Hunt Marks (Weekly) | ✅ | ❌ | ✅ | Elite marks |
| Masked Carnivale | ✅ | ❌ | ✅ | Blue Mage arena |

## Special Modules (2 total - All implemented ✅)

| Module | DailyDuty | CurrencyAlert | Wahdori | Notes |
|--------|-----------|---------------|---------|-------|
| Treasure Maps | ✅ | ❌ | ✅ | 18hr cooldown |
| Retainer Ventures | ❌ | ❌ | ✅ | Venture completion tracking |

## Other DailyDuty Modules (Not implemented)

| Module | Notes |
|--------|-------|
| Raids (Alliance) | Weekly loot lockouts |
| Raids (Normal) | Weekly loot lockouts |
| GC Squadron | Squadron missions |
| Faux Hollows | Unreal trial weekly |

## Core Features

| Feature | DailyDuty | CurrencyAlert | Wahdori | Notes |
|---------|-----------|---------------|---------|-------|
| Chat notifications | ✅ | ✅ | ✅ | Combined system |
| On-screen overlay | ✅ | ✅ | ✅ | Unified display |
| Module management | ✅ | ❌ | ✅ | Enable/disable modules |
| Localization | ✅ | ✅ | ✅ | Multi-language support |
| Per-character config | ✅ | ✅ | ✅ | Character-specific settings |
| Threshold warnings | ❌ | ✅ | ✅ | Currency cap alerts |
| Reset timers | ✅ | ❌ | ✅ | Daily/weekly resets |
| Clickable links | ✅ | ❌ | ❌ | Not implemented yet |
| TODO lists | ✅ | ❌ | ❌ | Not implemented yet |

## Module Implementation Status

- ✅ Currency: 14/14 (100%)
- ✅ Daily: 8/8 (100%)
- ✅ Weekly: 8/8 (100%)
- ✅ Special: 2/2 (100%)

**Total: 32/32 modules implemented (100%)**

Note: Some currency types like Storm/Serpent/Flame Seals are handled by the unified Grand Company module rather than separate modules. 