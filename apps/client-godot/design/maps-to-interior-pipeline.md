# Google Maps → Building Exterior → Interior Generation Pipeline

## What Exists

**StreetView Temporal Analysis** (`~/Projects/chronos/ingestion/streetview_scraper.py`):
- Extracts historical panoramas for any lat/lon
- Runs structural change detection (Canny → histogram correlation → roof/facade/site zones)
- Feeds GeoEvents into Chronos temporal-geographic engine
- Currently outputs: timeline JSON, panorama images, diff deltas

**BuildingGrid** (`vinc-engine/Scripts/BuildingGrid.cs`):
- Modular snap-grid placement system
- Glass-to-wall ratio for defensive exposure calculation
- Degradation modulated by window count and material type
- Currently generates geometry from *internal templates* — Foundations, Walls, Windows, Doors, Roofs, Pillars
- No external data input — every building is hand-placed through the UI

**Infrastructure Exposure Constraints** (domain-constraint-library):
- Low-density residential (detached/mansion): 30-50 windows, drywall, ~620 boards to fortify
- High-density residential (row house/tenement): 2-3 windows, concrete block, ~48 boards to fortify
- Material profiles, scavenging curves, structural degradation timelines

**Hyperpoly Terrain** (`~/Projects/hyperpoly-terrain/`):
- GPU-native material simulation
- Material-aware dual contouring that maps surface materials to structural properties

## The Gap

There is no bridge between Google Maps data and the game's procedural generation. The current BuildingGrid generates buildings from scratch (in-game placement). The StreetView pipeline extracts building *imagery* and *change detection* but has no code path that feeds exterior measurements into interior generation.

## What Must Be Built

### Layer 1: Exterior Extraction (StreetView → Building Profile)

Extract physical building parameters from Street View panoramas:

```
StreetView panorama
  → facade segmentation (OpenCV: detect window/door/roof zones)
  → window count + dimensions
  → door count + positions
  → floor count (inter-floor spacing)
  → roof type (flat vs sloped vs gabled)
  → material classification (brick vs siding vs concrete vs glass)
  → construction style (row house vs detached vs commercial vs industrial)
  → building footprint approximation (aspect ratio from facade proportions)
```

Output: `BuildingProfile { lat, lon, floors, windows, doors, material, style, roof_type, footprint }`

**New file:** `~/Projects/chronos/ingestion/building_extractor.py`
- Takes a Street View pano image
- Runs OpenCV segmentation:
  - Vertical edge detection to find floor boundaries
  - Horizontal edge clusters to find window rows
  - Contour detection to count and measure windows
  - Color/texture analysis to classify material
  - Aspect ratio analysis to classify style
- Outputs a `BuildingProfile` JSON

### Layer 2: Interior Generation (Building Profile → Floor Plan)

Given a building's exterior profile, generate a plausible interior:

```
BuildingProfile
  → footprint width × depth
  → load-bearing wall placement (structural grid based on span limits)
  → room distribution (living, kitchen, bedrooms, bathrooms, stairs, hallway)
  → window placement on exterior walls (from StreetView data)
  → door placement (interior doors between rooms)
  → stair placement (vertical circulation)
  → loot tables per room type (kitchen → food, bedroom → clothes/meds, garage → tools)
  → furniture placement (beds, tables, cabinets, shelves)
```

**Interior generation rules** (constraint-first, vinculum-based):
- `room_count / floor_area = density_ratio` (0.2 = spacious, 0.5 = cramped)
- `exterior_wall_length / window_count = spacing_ratio` (3-4m typical)
- `stair_width / floor_area = 0.08-0.12` (standard circulation)
- `load_bearing_spacing ≤ 6m` (wood frame typical span)
- `room_aspect_ratio ∈ [0.5, 2.0]` (no hallways that are 10x longer than wide)

**New files:**
- `vinc-engine/Scripts/BuildingGenerator.cs` — Takes BuildingProfile → generates interior geometry
- `vinc-engine/Scripts/FloorPlan.cs` — Room layout, wall grid, furniture placement
- `vinc-engine/Data/building_profiles/` — Cached StreetView extractions for rapid generation

### Layer 3: Game Integration (Profile → Interactive Building)

Connect the generator to the game's existing systems:

```
BuildingProfile (from StreetView)
  → BuildingGenerator.GenerateFloorPlan()
    → FloorPlan with rooms, walls, doors, windows
      → BuildingGrid.PlacePiece() for each wall/door/window
        → Existing glass-to-wall ratio, degradation, collision all apply
```

The existing glass-to-wall ratio and degradation systems will automatically apply to generated buildings — a mansion with 50 windows will have high defensive exposure regardless of whether it was built in-game or extracted from Maps.

**What changes in existing code:**
- `BuildingGrid.cs`: Add method `GenerateFromProfile(BuildingProfile profile)` that auto-places all pieces at correct positions
- `WorldStreamer.cs`: On chunk load, check if chunk has real-world coordinates → query BuildingProfile cache → generate building

### Layer 4: Real-World Coordinate Mapping

The game world needs to map game coordinates to real-world lat/lon.

**New file:** `vinc-engine/Scripts/GeoMapper.cs`
- `GameToGeo(Vector3 gamePos) → (lat, lon)`
- `GeoToGame(lat, lon) → Vector3 gamePos`
- Origin: pick a real city center (e.g., Pittsburgh: 40.44, -79.99) as game (0,0,0)
- Scale: 1 game meter = 1 real meter
- StreetView scraping at a lat/lon → building at corresponding game position

**Pipeline:** User provides a real address/coordinate → StreetView scraper gets pano → building_extractor produces profile → BuildingGenerator produces interior → BuildingGrid places it → player can walk through it in-game.

## Priority Order

| Layer | What | Difficulty | Depends On |
|-------|------|------------|------------|
| 1a | Façade segmentation (window/door/floor counting) | Medium | StreetView scraper working |
| 1b | Material classification from image | Medium | 1a |
| 1c | Building style classification | Low | 1a |
| 2a | Floor plan generation from footprint | High | 1a (footprint needed) |
| 2b | Room layout + furniture placement | High | 2a |
| 3 | BuildingGrid integration | Low | 2a, 2b |
| 4 | GeoMapper coordinate system | Low | 3 |

## First Buildable Target

A proof of concept that: Takes a StreetView panorama → extracts window count and floor count → generates a building in-game with the correct number of windows and floors.

This validates the entire pipeline without needing perfect segmentation or furniture placement. Start with Layer 1a + Layer 3 (minimal).
