# Floor Plan Architecture — Building Types from PUBG + Real Urban Patterns

## Grounding

PUBG's Erangel has ~15 building types that repeat across the map. Players learn them because the layout is **consistent per type** — once you know the "two-story white apartment" layout, you know every instance of it. That's the model: a small set of archetypes with modular variation, not infinite procedural noise.

These archetypes come from real Korean/Asian urban architecture: postwar apartment blocks, commercial/residential mixed-use, row houses, single-family homes converted to multi-unit. Erangel is a Soviet-influenced Korean island — the buildings encode that history.

## Building Archetype Catalog

### Type 1: Single-Story Bungalow (PUBG: "Shack" / "Small House")
- **Real equivalent:** Postwar single-family, rural/outer-district
- **Footprint:** 6m × 8m (48m²)
- **Floors:** 1
- **Rooms:** 2-3 (living + bedroom + kitchen)
- **Windows:** 2-4 (small, often barred)
- **Construction:** Brick or cinder block, corrugated roof
- **Defensive profile:** Fortress. Low glass/wall ratio. ~24 boards.
- **Interior:**
  ```
  [Bedroom 1]──[Living/Dining]
       │              │
  [Kitchen]────[Entry/Door]
  ```
- **Loot:** Low-tier household goods. Tools in kitchen. Possible hidden floorboard cache.
- **Vinc rule:** `room_count / floor_area ≈ 0.04-0.06`, `window_spacing / wall_length ≈ 0.15`

### Type 2: Two-Story Row House (PUBG: "Two-Story White" / "Town House")
- **Real equivalent:** Korean *dareukjip* (multi-family row housing, 1970s-80s)
- **Footprint:** 5m × 12m (60m² per floor)
- **Floors:** 2
- **Rooms:** 3-4 per floor (1F: living + kitchen + small bedroom, 2F: 2-3 bedrooms)
- **Windows:** 3-4 per floor (moderate, often barred on ground floor)
- **Construction:** Concrete frame, brick infill, tile roof
- **Defensive profile:** Moderate. ~48 boards ground floor. 2F safe room possible.
- **Interior:**
  ```
  1F:
  [Kitchen]──[Living]──[Bedroom 1]
       │                   │
  [Entry]──[Stairs]──[Bathroom]
  
  2F:
  [Bedroom 2]──[Hall]──[Bedroom 3]
                    │
            [Stairs]──[Bathroom]
  ```
- **Loot:** Moderate. Kitchen for food, bedrooms for clothes/meds, possible rooftop access.
- **Vinc rule:** `room_count / floor_area ≈ 0.07-0.09`, vertical circulation at ~25% of floor

### Type 3: Apartment Block (PUBG: "Apartments" — the school-adjacent buildings)
- **Real equivalent:** Korean *dandok jutack* / urban apartment complex (1980s-90s)
- **Footprint:** 15m × 30m per building
- **Floors:** 5
- **Units per floor:** 2-4 apartments
- **Per-unit:** 1 bedroom + living + kitchen + bath + balcony
- **Windows:** 2-3 per unit (floor-to-ceiling in living room, small in bedrooms)
- **Construction:** Reinforced concrete frame, slab floors, curtain walls
- **Defensive profile:** Vulnerable. Many units, many windows. ~120 boards per floor.
- **Interior (single unit ~50m²):**
  ```
  [Balcony]
      │
  [Living/Dining]──[Kitchen]
       │
  [Hall]──[Bathroom]
      │
  [Bedroom]──[Entry]
  ```
- **Common areas:** Stairwell at ends of building, central corridor per floor, rooftop access
- **Loot:** High density. Each unit has independent loot tables. Rooftop often has military loot.
- **Vinc rule:** `unit_area / building_footprint ≈ 0.60-0.70`, hallway circulation ≈ 10%

### Type 4: Commercial/Mixed-Use (PUBG: "Store" / "Restaurant")
- **Real equivalent:** Korean *sangga* (street-level commercial with residential above)
- **Footprint:** 6m × 15m (90m² per floor)
- **Floors:** 2-3
- **1F:** Retail/restaurant (open plan, counter, kitchen, storage, bathroom)
- **2F/3F:** Residential (1-2 bedroom apartment per floor)
- **Windows:** 1F: large display window (glass), 2F+: 2-3 windows
- **Construction:** Concrete frame, glass storefront at ground level
- **Defensive profile:** 1F is a sieve (large glass), 2F+ is fortress. Ground floor is death trap.
- **Interior:**
  ```
  1F (Restaurant):
  [Display Window/Entry]──[Dining Area]
                                  │
                         [Counter]──[Kitchen]
                                     │
                               [Storage]──[Bathroom/Stairs]
  
  2F (Residential):
  [Kitchen]──[Living]──[Bedroom]
       │ 
  [Stairs]──[Bathroom]
  ```
- **Loot:** 1F: food, alcohol, cooking supplies, cash register (dead currency). 2F+: household goods.
- **Vinc rule:** `commercial_area / total_footprint ≈ 0.40`, `glass_facade_ratio ≥ 0.60 (1F only)`

### Type 5: School (PUBG: "School")
- **Real equivalent:** Korean public school (1960s-70s concrete brutalist)
- **Footprint:** 20m × 40m (800m² per floor)
- **Floors:** 3-4
- **Rooms:** 10-16 classrooms per floor, central hall, admin offices, gymnasium, restrooms
- **Windows:** Many (every classroom has exterior wall of windows)
- **Construction:** Reinforced concrete, large window grids
- **Defensive profile:** Extremely vulnerable. ~300+ boards to fortify. Open corridors.
- **Interior:**
  ```
  [Classroom]──[Classroom]──[Classroom]──[Stairs]
       │            │            │
  [Hallway ──────────────────────────]
       │            │            │
  [Classroom]──[Classroom]──[Classroom]──[Stairs]
  
  (Repeated each floor. Gymnasium is a separate large-volume space.)
  ```
- **Loot:** Medium density but wide variety. Office supplies, lab equipment, locker rooms have clothes/bags, cafeteria has food. Rooftop for sniping.
- **Vinc rule:** `classroom_area / circulation ≈ 1.5`, `corridor_width ≥ 2m`

### Type 6: Hospital (PUBG: "Hospital")
- **Real equivalent:** Regional Korean hospital (1970s concrete)
- **Footprint:** 25m × 40m (1000m² per floor)
- **Floors:** 3-5
- **Rooms:** Emergency, operating rooms, patient wards, pharmacy, offices, basement morgue
- **Windows:** Moderate (patient rooms have windows, operating rooms don't)
- **Construction:** Reinforced concrete, specialized HVAC
- **Defensive profile:** Moderate. Some windowless interior rooms are very defensible.
- **Interior:**
  ```
  1F:
  [ER]──[Triage]──[Nurse Station]──[Elevator/Stairs]
    │                  │
  [Waiting]─────[Pharmacy]──[Admin]
  
  Basement:
  [Morgue]──[Storage]──[Generator Room]
  ```
- **Loot:** Medical! Bandages, first aid kits, antibiotics, antivirals, painkillers. Pharmacy is prime.
- **Vinc rule:** `medical_areas / total_area ≈ 0.30`, `isolation_rooms have windows = false`

### Type 7: Warehouse / Industrial (PUBG: "Warehouse" / "Factory")
- **Real equivalent:** Korean light industrial (1980s pre-fab)
- **Footprint:** 15m × 25m (375m²)
- **Floors:** 1-2
- **Rooms:** Open floor plan, office mezzanine, storage, loading dock, restroom
- **Windows:** Few, high up (clerestory windows)
- **Construction:** Pre-fab metal or concrete tilt-up
- **Defensive profile:** Fortress (few entry points, but large open interior means limited cover)
- **Interior:**
  ```
  [Loading Dock]──[Open Floor / Warehouse Space]
                         │
                  [Mezzanine: Office]
                         │
                  [Bathroom]──[Storage Closet]
  ```
- **Loot:** Materials. Scrap metal, wood, tools, machinery. Possible vehicle spawn.

### Type 8: Multi-Story Pagoda (Asian urban specific)
- **Real equivalent:** Korean mixed-use *sangga* in older neighborhoods
- **Footprint:** 8m × 20m
- **Floors:** 3-5
- **1F:** Commercial (restaurant, convenience store, PC bang)
- **2F+:** Residential or offices
- **Roof:** Often flat, used for storage or satellite dishes
- **Interior:** Narrow floor plate means single-loaded corridor. Rooms on one side, corridor on the other, stairs at one or both ends.

## Material-to-Model Mapping (from Infrastructure Exposure Constraints)

| StreetView Material | Game Material | Integrity | Board Cost |
|-------------------|---------------|-----------|------------|
| Concrete/Concrete block | Foundation type | 150 | 0 |
| Brick | Wall type | 80 | 0 |
| Wood siding | Light wall | 40 | 0 |
| Glass curtain wall | Window type | 20 | 12/panel |
| Corrugated metal | Roof type | 30 | 0 |
| Tile/Stone | Roof type | 50 | 0 |
| Drywall/Plaster (interior) | Interior wall | 20 | 0 (no defense value) |

## Floor Plan Generation Algorithm

```
Input: BuildingProfile (floors, width, depth, type, material, window_count, roof_type)
Output: FloorPlan (room layout, wall positions, door positions, furniture)

1. Determine archetype from building type AND window count:
   - windows/floor < 2 AND width < 7m  → Type 1 (bungalow)
   - windows/floor 2-4 AND width 5-7m → Type 2 (row house)
   - windows/floor 2-4 AND width > 10m → Type 4 (commercial)
   - windows/floor 4-8 AND width > 12m → Type 3 (apartment)
   - windows/floor > 8 AND width > 15m → Type 5 (school/institutional)
   - windows/floor < 3 AND depth > 20m → Type 7 (warehouse)

2. Generate floor plate:
   - Load-bearing walls at structural spans (max 6m wood, max 8m concrete)
   - Exterior walls from footprint
   - Core: stairs + bathroom + utility shaft

3. Subdivide per archetype:
   - Residential types: split by function (living/bedroom/kitchen/bath)
   - Commercial types: open front + service rear
   - Institutional types: corridor + room repetition

4. Place doors from window/door data:
   - StreetView shows front door position
   - Interior doors at room boundaries (standard 0.8m wide)
   - At least 2 exit paths per floor

5. Place furniture per room:
   - Bedroom: bed + cabinet + shelf/closet
   - Living: table + chairs + shelf
   - Kitchen: cabinet + table
   - Bathroom: (no furniture, tile floor)
   - Office: desk + chair + cabinet
   - Commercial: counter + shelves + tables

6. Assign loot tables per room:
   - Kitchen → food category (60%) + material (10%) + empty (30%)
   - Bedroom → medical (15%) + clothing (45%) + material (10%) + empty (30%)
   - Living → material (25%) + component (10%) + empty (65%)
   - Garage → material (50%) + component (20%) + ammo (5%) + empty (25%)
   - Office → component (20%) + ammo (10%) + empty (70%)
   - Pharmacy → medical (80%) + empty (20%)
```

## Footprint Extraction from StreetView

A single facade image can't give you the full footprint. But you can infer it:

```
Known: facade_width (from image, measure in pixels → calibrate known reference)
Known: floor_count (from window row spacing)
Known: building_type (from window layout, material, roof)

Assumptions per type:
  Type 1 (bungalow): depth = width × 1.2-1.5
  Type 2 (row house):  depth = width × 2.0-2.5 (narrow front, deep lot)
  Type 3 (apartment):  depth = width × 0.6-0.8 (wide front, moderate depth)
  Type 4 (commercial): depth = width × 1.5-2.5
  Type 5 (school):     depth = width × 1.0-2.0
  Type 7 (warehouse):  depth = width × 0.8-1.5

Fallback: If only one StreetView angle is available,
use the type's median depth ratio.
If multiple angles are available (corner building),
triangulate the actual depth from the two facade views.
```

## First Buildable Target

Generate a 2-story row house from:
- StreetView pano at a given address
- Extract: width, floor count, window count, window positions, door position, material
- Assign: Type 2 (row house) archetype
- Generate: 1F floor plan with kitchen, living, bedroom, bath, stairs
- Generate: 2F floor plan with 2 bedrooms, bath, hall
- Place: furniture from trench_builder GLB assets (bed, chair, table, cabinet, shelf)
- Output: BuildingGrid pieces placed at correct positions

This is the minimum viable pipeline. Everything else (apartment blocks, schools, warehouses) is scaling the same approach with different archetype templates.
