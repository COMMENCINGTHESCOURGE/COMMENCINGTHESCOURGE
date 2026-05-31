# Necroworld UI Mockups & Node Trees
## Complete interface reference for all 6 panels

---

## 1. MAIN HUD (persistent overlay)

```
MainHUD (CanvasLayer)
├── VBoxContainer (top-left, 200px wide)
│   ├── DayLabel [Label]           "Day -3"
│   ├── TimeLabel [Label]          "14:30 (Day)"
│   ├── WeatherLabel [Label]       "Weather: Rain"
│   ├── TempLabel [Label]          "18°C (Comfortable)"
│   ├── HBoxContainer
│   │   ├── HungerLabel [Label]    "Hunger"
│   │   └── HungerBar [ProgressBar]  [▓▓▓▓▓▓▓░░░] 72%
│   ├── HBoxContainer
│   │   ├── ThirstLabel [Label]    "Thirst"
│   │   └── ThirstBar [ProgressBar]  [▓▓▓▓▒░░░░░] 45%
│   ├── HBoxContainer
│   │   ├── FatigueLabel [Label]   "Fatigue"
│   │   └── FatigueBar [ProgressBar] [▒▒▒▒░░░░░░] 22%
│   ├── HBoxContainer
│   │   ├── InfectionLabel [Label] "Infection"
│   │   └── InfectionBar [ProgressBar] [▓░░░░░░░░░] 8%
│   ├── HBoxContainer
│   │   ├── MoraleLabel [Label]    "Morale"
│   │   └── MoraleBar [ProgressBar]  [▓▓▓▓▓▓▒░░░] 65%
│   └── CurrencyLabel [Label]      "Cash: DEAD (Day 8)"
│
├── QuickSlots (HBoxContainer, bottom-center)
│   ├── Slot1 [TextureRect + Label]  -  Pistol [9/12]
│   ├── Slot2 [TextureRect + Label]  -  Crowbar
│   ├── Slot3 [TextureRect + Label]  -  Bandage x3
│   ├── Slot4 [TextureRect + Label]  -  Water
│   ├── Slot5 [TextureRect + Label]  -  Canned Beans
│   ├── Slot6 [TextureRect + Label]  -  Empty
│   └── Slot7 [TextureRect + Label]  -  Empty
│   (Selected slot highlighted with border)
│
├── Compass (HBoxContainer, top-center, 400px)
│   ├── DirectionLabel [Label]     "N / E / S / W markers"
│   └── POIMarkers (TextureRect[])  "Territory | Landmarks | Zombie hotspots"
│
├── InteractionPrompt (Center-bottom)
│   └── PromptLabel [Label]        "[E] Pick up Canned Beans"
│                                   "[F] Talk to Marcus"
│                                   "[Q] Board up window"
│
├── NotificationStack (VBoxContainer, top-right)
│   └── Notification [Panel + Label]  "+Antibiotics crafted"
│                                     "Zombie horde approaching!"
│                                     "Currency died (Day 8)"
│
└── Crosshair (Center, 24x24px)
    └── CrosshairRect [TextureRect]  "+"
```

**Node tree file:** `UI/MainHUD.tscn`

---

## 2. INVENTORY GRID (toggle with Tab / I)

```
InventoryPanel (Panel, centered, 800x600)
├── TitleBar (HBoxContainer)
│   ├── TitleLabel [Label]          "Inventory"
│   ├── WeightLabel [Label]         "Weight: 18.5 / 40.0 kg"
│   └── CloseButton [Button]        "[X]"
│
├── GridContainer (8 columns, 10 rows)
│   ├── Cell1 [TextureRect + Label]  [Pistol icon] "Pistol"
│   ├── Cell2 [TextureRect + Label]  [Ammo icon]  "9mm x42"
│   ├── Cell3 [TextureRect + Label]  [Food icon]  "Canned Beans x3"
│   ├── Cell4 (empty, dimmed)
│   ...
│   └── Cell80 (empty, dimmed)
│
├── SplitPanel
│   ├── ItemDetail (right panel, 250px)
│   │   ├── ItemName [Label]        "Crowbar"
│   │   ├── ItemIcon [TextureRect]  [Large icon]
│   │   ├── ItemDesc [Label]        "Heavy metal crowbar. Pries and pounds."
│   │   ├── StatBlock (VBox)
│   │   │   ├── Stat1 [Label]       "Damage:  20"
│   │   │   ├── Stat2 [Label]       "Weight:  2.0 kg"
│   │   │   ├── Stat3 [Label]       "Volume:  3 slots"
│   │   │   ├── Stat4 [Label]       "Durability: 80/100"
│   │   │   └── Stat5 [Label]       "Noise:    Low"
│   │   └── ActionButtons (HBox)
│   │       ├── UseButton [Button]  "[Use]"
│   │       ├── EquipButton [Button] "[Equip]"
│   │       ├── DropButton [Button]  "[Drop]"
│   │       └── SplitButton [Button] "[Split]"
│   │
│   └── EquipmentSlots (bottom-right, 3x3 grid)
│       ├── Head [TextureRect]      (empty)
│       ├── Chest [TextureRect]     (empty)
│       ├── Hands [TextureRect]     [Crowbar]
│       ├── Legs [TextureRect]      (empty)
│       ├── Backpack [TextureRect]  [Backpack]
│       ├── Ammo [TextureRect]      [9mm x42]
│       ├── Acc1 [TextureRect]      (empty)
│       ├── Acc2 [TextureRect]      (empty)
│       └── QuickSlot [TextureRect] [Bandage x3]
│
├── SortButton [Button]            "[Sort]"
├── SearchBar [LineEdit]           "Search..."
└── CategoryFilter (HBox)
    ├── All [Button]
    ├── Weapons [Button]
    ├── Consumables [Button]
    ├── Materials [Button]
    └── Medical [Button]
```

**Node tree file:** `UI/InventoryPanel.tscn`

---

## 3. CRAFTING MENU (toggle with C)

```
CraftingPanel (Panel, centered, 600x500)
├── TitleBar (HBox)
│   ├── TitleLabel [Label]          "Crafting"
│   └── CloseButton [Button]        "[X]"
│
├── StationTabs (HBox)
│   ├── HandsTab [Button]           "Hands"  ← active
│   ├── CampfireTab [Button]        "Campfire"
│   ├── WorkbenchTab [Button]       "Workbench"
│   └── MedicalTab [Button]         "Medical Station"
│
├── RecipeList (ScrollContainer, left 350px)
│   ├── RecipeRow1 [Panel + VBox]
│   │   ├── RecipeName [Label]      "Craft Bandage"
│   │   ├── RecipeTime [Label]      "10s | Need: Medicine skill"
│   │   ├── Ingredients [Label]     "Cloth x2"
│   │   ├── OutputPreview [TextureRect] [Bandage icon]
│   │   └── CraftButton [Button]    "[Craft]"  (green if can, gray if can't)
│   │
│   ├── RecipeRow2 [Panel + VBox]
│   │   ├── RecipeName [Label]      "Build Campfire"
│   │   ├── RecipeTime [Label]      "20s | Survival skill"
│   │   ├── Ingredients [Label]     "Wood x3"
│   │   └── CraftButton [Button]    "[Craft]"  (disabled - missing Wood x3)
│   │
│   └── RecipeRow3 [Panel + VBox]
│       ├── RecipeName [Label]      "Craft Machete"
│       ├── RecipeTime [Label]      "120s | Engineering skill"
│       ├── Ingredients [Label]     "Scrap Metal x3, Duct Tape x1"
│       └── CraftButton [Button]    "[Craft]"  (requires Workbench)
│
├── AvailabilityFilter
│   ├── ShowAll [CheckBox]          "Show all recipes"
│   └── ShowCraftable [CheckBox]    "Only craftable"
│
└── CraftProgress (bottom bar, visible during crafting)
    ├── ProgressBar [ProgressBar]   [▓▓▓▓░░░░░░] 40%
    ├── ProgressLabel [Label]       "Crafting Bandage... (4s remaining)"
    └── CancelButton [Button]       "[Cancel]"
```

**Node tree file:** `UI/CraftingPanel.tscn`

---

## 4. BUILDING PLACEMENT UI (toggle with B)

```
BuildingPanel (Panel, bottom-center, anchored)
├── PiecePalette (HBoxContainer)
│   ├── FoundationBtn [Button]     [Foundation]  ← active
│   ├── WallBtn [Button]           [Wall]
│   ├── FloorBtn [Button]          [Floor]
│   ├── DoorBtn [Button]           [Doorway]
│   ├── WindowBtn [Button]         [Window]
│   ├── StairsBtn [Button]         [Stairs]
│   ├── RoofBtn [Button]           [Roof]
│   └── PillarBtn [Button]         [Pillar]
│
├── MaterialIndicator [Label]      "Required: Wood x5, Nails x10"
├── IntegrityPreview [Label]       "Integrity: 80 (Supported ✓)"
├── RotationBtn [Button]           "[Rotate]"  (cycles through 4 rotations)
├── RemoveBtn [Button]             "[Remove Mode]"  (toggle, turns red when active)
├── SnapToggle [CheckBox]          "Snap to Grid"
│
└── GhostPreview (3D node, follows mouse on ground plane)
    └── GhostMesh [MeshInstance3D]  (semi-transparent, green=valid, red=invalid)
```

**No separate .tscn needed** — the building UI renders as part of the main HUD when building mode is active. The ghost preview is a 3D node, not a Control node.

---

## 5. MINIMAP / COMPASS (persistent overlay)

```
MinimapPanel (CanvasLayer, top-right corner, 200x200)
├── Border [NinePatchRect]         (rounded border, semi-transparent bg)
├── PlayerDot [TextureRect]        (white triangle, rotates with player facing)
├── NPCDots [TextureRect[]]        (green dots)
├── ZombieDots [TextureRect[]]     (red dots, only within detection range)
├── TerritoryLines [TextureRect]   (color-coded by faction)
├── POIMarkers [TextureRect[]]     (star icons for landmarks)
│
└── CompassOverlay (top-center, 400px wide)
    └── CompassBar [TextureProgress]
        ├── N [Label]
        ├── E [Label]
        ├── S [Label]
        ├── W [Label]
        └── FacingIndicator [TextureRect]  (triangle pointing down at current heading)
```

**Node tree file:** `UI/MinimapPanel.tscn`

---

## 6. INTERACTION PROMPTS (context-sensitive)

```
InteractionSystem (Node - manages prompts programmatically)
├── ProximityDetector (Area3D, attached to Player)
│   └── DetectionShape [CollisionShape3D]  (sphere, radius 3m)
│
├── CurrentPrompt [Label3D or Control]
│   └── PromptText [RichTextLabel]   "[E] Pick up Canned Beans"
│                                    "[F] Talk to Marcus"  
│                                    "[Q] Board up window"
│                                    "[R] Open container"
│                                    (Multiple prompts stack if multiple interactables nearby)
│
└── LootWindow (Panel, 400x300, toggled by looting a container)
    ├── TitleBar [Label]              "Rusty Locker"
    ├── ContainerGrid (GridContainer, 5 columns)
    │   ├── LootSlot1 [TextureRect + Label]  [Ammo x12]
    │   ├── LootSlot2 [TextureRect + Label]  [Bandage]
    │   ├── LootSlot3 (empty)
    │   ├── ...
    │   └── TakeAllBtn [Button]       "[Take All]"
    │
    └── TransferButtons (HBox)
        ├── TakeBtn [Button]          "[Take]"
        └── StoreBtn [Button]         "[Store]"
```

**Programmatic only** — the interaction system uses Area3D triggers and generates prompts on the fly. No dedicated .tscn file; prompts are Label3D children of interactable objects and loot windows are instantiated from a small prefab.

---

## Color Palette (Dark Survival Theme)

| Element | Color | Hex |
|---------|-------|-----|
| Background (panels) | Dark charcoal | `#1A1A1E` |
| Panel border | Steel gray | `#3A3A42` |
| Text primary | Off-white | `#E0E0E0` |
| Text dim | Muted gray | `#808080` |
| Danger / Low | Blood red | `#CC3333` |
| Warning / Medium | Amber | `#CC8800` |
| Safe / High | Muted green | `#448844` |
| Infection bar | Toxic purple | `#8844AA` |
| Morale bar | Sky blue | `#4488CC` |
| Selection highlight | Cyan | `#22CCDD` |
| Button hover | Lighter steel | `#4A4A52` |
| Ghost valid | Semi-green | `#44FF4488` |
| Ghost invalid | Semi-red | `#FF444488` |

---

## Quick Reference: All 6 UI Files

| Panel | File | Toggle | Priority |
|-------|------|--------|----------|
| Main HUD | `UI/MainHUD.tscn` | Always on | Must have |
| Inventory | `UI/InventoryPanel.tscn` | Tab / I | Must have |
| Crafting | `UI/CraftingPanel.tscn` | C | Must have |
| Building | (HUD-integrated, 3D ghost) | B | Nice to have |
| Minimap | `UI/MinimapPanel.tscn` | Always on | Nice to have |
| Interaction | (Programmatic 3D prompts) | Auto | Must have |
