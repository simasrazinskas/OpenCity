# isokit: OpenCity's 2.5D isometric renderer

isokit renders small 3D scenes into RCT2/TTD-style pixel sprites. It uses numpy and the
standard library only.

The output is palette-quantized RGBA with alpha 0 or 255. Pixel centres are sampled on a
z-buffer, so 2:1 edges come out as clean pixel stairs.

## Projection (fixed, from `design/iso/BRIEF.md`)
- Tiles are 64x32 px. World units: `x` and `y` in **cells**, `z` in **screen px**.
  `screen_x = (x - y) * 32` and `screen_y = (x + y) * 16 - z`.
- +X runs screen down-right and +Y runs screen down-left. The camera looks from +X+Y+Z.
- **Storey = 10 px** (`ik.STOREY`). A 1-cell cube is about 39 px tall (`ik.S = 39.2`).
  A person is about 7 px tall and a car about 16 px long.
- Light comes from the upper left. The lighting levels (0..1) are:

  | Face | Level | Ramp index |
  |---|---|---|
  | Top | 0.75 | about 8 of 11 |
  | Left face (+Y) | 0.51 | about 5.6 |
  | Right face (+X) | 0.22 | about 2.4 |

  Curved surfaces are smooth-shaded.

## Quick start
```python
import sys; sys.path.insert(0, 'mods/city/tools')
import isokit as ik

s = ik.Scene(footprint=(1, 1), seed=3)                 # footprint in cells
s.tile(0, 0, 'grass')                                   # ground decal (optional)
s.box(0.15, 0.2, 0, 0.7, 0.6, 20, 'windows_brick')      # x, y, z, dx, dy, dz(px), material
s.roof_gable(0.15, 0.2, 20, 0.7, 0.6, 12, 'roof_tiles', axis='x', gable='brick')
spr = ik.render(s)                  # ik.Sprite: .img (H,W,4 uint8), .ax/.ay anchor, .emit
spr.save('house.png')               # writes Offset/Anchor tEXt metadata too
night = ik.render(s, night=True)    # dark ambient + lit windows (emissive)
snowy = ik.render(s, season='winter')
four = ik.facings(s)                # 4 Sprites: front +Y, +X, -Y, -X
ik.save_strip('house-facings.png', four)
ik.inspect('/tmp/look.png', spr.img, k=4)   # upscaled preview on a flat background
```
The anchor `(ax, ay)` is the pixel of the footprint's ground centre, so place sprites with
`top_left = screen(cell centre) - anchor`.

## Scene primitives (all return the scene or the item)
| call | notes |
|---|---|
| `box(x, y, z, dx, dy, dz, mat, top=, left=, right=)` | `left` = +Y face, `right` = +X face |
| `extrude(poly2d, z0, z1, mat, top=)` | any (concave) 2D polygon in cells |
| `roof_gable(x, y, z, dx, dy, h, mat, axis='x', overhang=0.06, gable=)` | ridge along x or y |
| `roof_hip(x, y, z, dx, dy, h, mat)` / `pyramid(...)` | |
| `roof_shed(x, y, z, dx, dy, h, mat, high='-y', wall=)` | rises toward `high` |
| `roof_flat(x, y, z, dx, dy, mat, parapet=2, rim=)` | parapet in px |
| `roof_mansard(x, y, z, dx, dy, h, inset, mat, top=)` | also a frustum |
| `cylinder(cx, cy, z, r, h, mat, top=, axis='z', segs=20)` | axis `'x'`/`'y'`: horizontal, `h` is length in cells, `z` is axis height |
| `cone(cx, cy, z, r, h, mat)` | |
| `ellipsoid(cx, cy, cz, rx, ry, rz_px, mat, rough=0)` | analytic. `rough` 0.15-0.5 gives a leafy ragged edge |
| `sphere(cx, cy, cz, r, mat)`, `dome(cx, cy, z, r, h, mat)` | |
| `blob([(cx, cy, cz, r[, rz]), ...], mat, rough=0.3)` | tree crowns, bushes |
| `ground(poly2d, mat, z=0, layer=0)`, `tile(cx, cy, mat)` | decals; a higher layer wins |
| `poly(verts3d, mat, outward=)`, `quad(a, b, c, d, mat)`, `tri(a, b, c, mat)` | arbitrary planar faces |
| `merge(other_scene, dx, dy, dz)` | instancing |

## Materials
`ik.mat(name, **overrides)` returns a registered material. A material is a palette ramp, a
shade offset in ramp steps, and a pattern. Overrides can change `ramp`, `shade` and
`dither`, plus any pattern parameter. For example:
`ik.mat('windows_brick', ramp='sand', storey=10, win_w=5, period=9, lit=0.7)`.

Registered materials:
- **Walls:** `plain plaster plaster_white brick brick_yellow siding siding_white wood
  concrete stone metal metal_light glass glass_dark`
- **Windows over walls:** `windows_brick windows_plaster windows_siding windows_concrete
  windows_office shopfront shopfront_brick`
  - Window parameters: `base storey ground win_w win_h period sill margin glass lit`.
  - Shopfront parameters: `shop_h sign pane door_at`.
- **Roofs:** `roof_tiles roof_tiles_brown slate shingle roof_metal roof_flat roof_gravel
  roof_green`
- **Ground:** `asphalt paving concrete_ground grass grass_dry meadow dirt mud sand gravel
  rock forest_floor snow water` (water animates with `render(frame=k)`, 8 frames)
- **Vegetation:** `foliage foliage_light conifer bark bark_birch`

Your own material: `ik.Material('teal', shade=1, pattern=my_fn, **params)`. Here
`my_fn(c, m)` edits `c.tone`, `c.ramp`, `c.emit`, `c.eramp`, `c.eshade` and `c.flat`.
See the docstring in `materials.py` for the inputs, which include `u v x y z sx sy n`,
the face extents, `night`, `frame` and `season`. Use `snow=False` to keep a material
free of winter snow.

## Palette
`ik.PALETTE` has the shape `(18 ramps, 12 shades, 3)`. The ramps are:

`grey stone wood sand brick terra yellow olive grass leaf teal water glass slate purple rose
red snow`

- `ik.color('brick', 6)` returns one RGB value.
- `ik.palette_sheet()` returns an image of the whole palette.
- `ik.nearest(rgb)` quantizes any colours to the palette.

## Render options
`render(scene, facing=0, night=False, season='summer', frame=0, outline='edge',
origin=(0, 0), crop=True)`

- `outline`: `'edge'` darkens the silhouette rim by 2 shades, which is the house style.
  The alternatives are `'dark'` (a 1-px dark outline) and `None`.
- `origin`: the cell offset used to seed ground patterns. It lets neighbouring tiles join
  seamlessly.

## Sheets, manifests, compose
- `facings(scene)` returns 4 sprites. `align(sprites)`, `strip(sprites)` and
  `save_strip(path, sprites)` produce anchor-aligned frame strips.
- `grid(imgs, cols)` builds a preview sheet.
- `Manifest(root, page, section).group(title, columns, scale, note).add(file, label)`, then
  `.write()`, writes the Figma manifest. It checks that every listed file exists.
- `Compositor(w, h)` places sprites in painter's order:
  - `.ground(spr, cx, cy)` adds a flat layer item.
  - `.place(spr, cx, cy)` adds an object. The footprint comes from `spr.footprint`.
  - `.render()` returns the image.

## Added after M1

### Organic and terrain primitives
| call | notes |
|---|---|
| `limb(p0, p1, r0, r1, mat)` | Branches, poles, grass blades. Drawn as a camera-facing band with round shading, never thinner than 1 px |
| `rock(cx, cy, z, r, h, mat='rock', seed=)` | Faceted boulder |
| `heightfield(x0, y0, dx, dy, zfn, mat, n=16, steep=, steep_at=0.6)` | Terrain patch. Slopes steeper than `steep_at` use the `steep` material (banks, cliffs) |

### Trees
Anyone can use these for lots, parks and street trees.
- `ik.tree(species, season, stage, seed, state)` returns a 1x1 Scene.
- `ik.add_tree(scene, cx, cy, species, season, stage, seed, state)` adds a tree to your own scene.
- Species: `linden oak maple birch poplar willow apple cherry spruce fir pine`.
  The game trees are in `ik.GAME_TREES`: tree-1 is linden, tree-2 oak, tree-3 spruce, tree-4 birch.
- Stages: 0 sapling, 1 young, 2 mature.
- Seasons: `spring summer autumn winter`. In winter, deciduous trees are bare branches.
  Render with `season='winter'` to get snow.
- States: `alive dead burnt stump`.

### Engine export (see `design/iso/ENGINE-PLAN.md`)
- `ik.day_and_lit(scene, facing)` returns the day sprite plus a `-lit` companion. The
  companion has the same size and anchor and holds only the emissive pixels in their night
  colours.
- `ik.save_pair(path, scene)` writes `x.png` and `x-lit.png`, both even-sized.
- `ik.even(sprite)` pads a sprite to even width and height.
- `ik.footprint_overhang(sprite)` measures content outside the footprint's horizontal
  extent. Buildings get sliced into 32-px strips, so this should be `(0, 0)`.
  `ik.clip_footprint(sprite)` cuts that content off.
- Use `render(..., creases=0)` to switch off crease darkening, and `render(..., snow=0.4)`
  for patchy winter snow.

### Terrain tiles
- `ik.flat_tile(mat, origin)` and `ik.render_tile(scene, origin)` return exact 64x32
  diamond tiles with the anchor at (32, 16). Different `origin` values give non-repeating
  variants.
- `ik.shore_combos()` lists the 46 `(mask, corners)` pairs in genterrain order. The ids
  are `200 + i` and `400 + i`.
- `ik.combo_label(mask, corners)` gives a short label for a pair.
- `ik.edge_distance(u, v, mask, corners, wobble)` drives any blob transition. The wobble
  stays zero at the cell corners, so instanced tiles always join.
- Inside a pattern, `isokit.details.paint(c, mat, sel, m.shade)` renders another material
  on the selected pixels.

### Reduced scale and thumbnails
- `render(scene, tile=(32, 16))` (or `scale=0.5`) renders at a smaller tile size. It
  re-projects the geometry rather than shrinking the image.
  - Patterns are laid out in output pixels, so window grids get fewer windows that stay
    readable. A pattern "storey" stays 10 output px.
  - Use tile widths that are multiples of 4 to keep clean 2:1 stairs.
- `ik.thumbnail(scene, size=64, ground='grass', facing=0)` builds a build-menu thumbnail.
  - It puts the scene on its own footprint ground and renders it at the largest tile width
    (a multiple of 4, up to 1.5 x size) that fits.
  - The result is bottom-centred on a transparent size x size square. `.tile` reports the
    tile width used.
  - Use `size=96` for 1.5x.

### Fixes and additions after the CIVIC report
- **Per-face box overrides.** `box(..., left=, right=, back_left=, back_right=)` sets a
  material on one model face. The faces are: `left` = +Y, `right` = +X, `back_left` = -X,
  `back_right` = -Y. Each override stays on its own face as the model rotates through the
  facings, and the back faces default to `mat`.
- **Seamless ground.** Ground faces get no silhouette outline: that means `ground()`,
  `tile()`, and flat up-facing faces at z <= 0. Ground tiles and decals now join without a
  dark grid line. Pass `render(..., outline_ground=True)` to get the old behaviour.
- **Any yaw.** `render(scene, yaw=45 * k)` renders at any angle, using the same direction
  of rotation as `facing`. It is meant for 8-direction vehicles and keeps the footprint and
  anchor unchanged.
- **Tree scale.** `add_tree(..., scale=0.6)` and `tree(..., scale=)` scale every size of
  the tree.
- **Cone and cylinder seam.** `cone` and horizontal `cylinder` now close their
  wrap-around segment correctly.

### Review
`python3 tools/iso_kit_preview.py <manifest.json> out.png [group-filter]` builds a contact
sheet of a manifest, close to the Figma layout.

## Stability
After M1 the API only grows. Names and signatures are not removed or renamed.
