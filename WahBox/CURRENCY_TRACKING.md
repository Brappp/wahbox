# WahBox Currency Tracking Summary

## Implemented Currency Modules

### Tomestones
- ✅ **Poetics** (ID: 28) - Max: 2,000
- ✅ **Non-Limited Tomestone** (Dynamic ID) - Max: 2,000  
- ✅ **Limited Tomestone** (Dynamic ID) - Max: 2,000

### Grand Company
- ✅ **Storm Seals** (ID: 20) - Max: 90,000
- ✅ **Serpent Seals** (ID: 21) - Max: 90,000
- ✅ **Flame Seals** (ID: 22) - Max: 90,000

### PvP Currencies
- ✅ **Wolf Marks** (ID: 25) - Max: 20,000
- ✅ **Trophy Crystals** (ID: 36656) - Max: 20,000

### Hunt Currencies
- ✅ **Allied Seals** (ID: 27) - Max: 4,000
- ✅ **Centurio Seals** (ID: 10307) - Max: 4,000
- ✅ **Sack of Nuts** (ID: 26533) - Max: 4,000

### Crafting/Gathering Scrips
- ✅ **White Scrips** (ID: 25199) - Max: 4,000
- ✅ **Purple Scrips** (ID: 25200) - Max: 4,000
- ✅ **Skybuilders' Scrips** (ID: 28063) - Max: 10,000

### Other
- ✅ **Bicolor Gemstones** (ID: 26807) - Max: 1,000

## Special Currency Handling

The `CurrencyHelper` class properly handles:
- Regular inventory items (most currencies)
- Grand Company seals (special retrieval)
- PvP currencies (Wolf Marks, Trophy Crystals)
- Hunt currencies (Allied/Centurio Seals, Sack of Nuts)
- Scrips (White/Purple for both Crafters and Gatherers)
- Bicolor Gemstones
- Dynamic tomestone IDs that change each expansion

## Notes
- Tomestone IDs change with each expansion but are properly detected
- Grand Company seals only track the player's current GC
- All special currencies now properly retrieve their counts from game memory
- Maximum values are automatically set for all known currencies
