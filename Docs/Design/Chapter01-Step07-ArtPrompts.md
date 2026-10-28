# 第七步图像来源与生成提示

工具模式：内置 image_gen，生成后复制到项目；Unity Sprite Editor 切片与 Point 导入。用户提供的抹茶云原图直接复制，未生成或重绘。

- `Assets/ChapterOne/Art/Step07/MatchaCloudCompact.png`：用户提供的四表情参考图，精确保留。
- `Assets/ChapterOne/Art/Step07/SheilaOriginalDraft.png`：生成的最初草稿。
- `Assets/ChapterOne/Art/Step07/MatchaPowder.png`：生成的透明原料图。
- `Assets/ChapterOne/Art/Step07/MatchaDesserts.png`：依据用户甜品参考图生成的 2×2 透明 Sprite Sheet。

## 最终提示词

### 最初草稿

Create a single in-game illustration asset for a cozy top-down pixel-art school story. A student's first pencil draft of a small house, shown on one warm ivory square page viewed straight-on. The drawing is simple, lively and imperfect: triangular roof, one door, two small windows, a short wobbly path, a few tentative pencil lines. Very limited dark muted plum and soft gray pencil marks, subtle pale mint accents, chunky clearly visible pixels, no fine high-resolution texture. Friendly modest unfinished sketch, not an architectural blueprint. Center the house with generous clean margins. No people, no family symbols, no lettering, no frame, no UI, no hands or pencil props. Entire canvas is the paper page, square composition, opaque background. This is the original sketch before repeated erasing, so the lines should feel spontaneous, with only a few construction strokes. Intended to be displayed at about 220 pixels wide inside a cream/purple dialogue interface.

### 微涩的抹茶粉

Create one isolated inventory item sprite for a cozy pixel-art game: a tiny mound of fine matcha-green powder with a few square green magical grains floating just above it, gently luminous mint accents, muted purple dark outline, compact rounded silhouette. Green powder itself, no bowl, no jar, no bag, no spoon, no cloud face, no people, no lettering, no UI, no ground plane or drop shadow. Deliberately simple 32x32-pixel-art design rendered enlarged with crisp uniform square pixel steps and a restrained 6-color palette. Centered fully visible with generous transparent padding. The item must have a true transparent background and read clearly at a very small on-screen size.

### 四款甜品图集

Create a transparent background sprite sheet of exactly FOUR separate small pixel-art matcha desserts for a cozy 2D top-down cafe game. Use the supplied image only as shape/style reference, remove all background and all other desserts. Clean 2x2 grid equal sized cells, generous empty transparent gaps. Top left: triangular slice matcha layer cake inspired by reference top row SECOND from left, pale cream and matcha sponge layers, tiny green leaf decoration, NO red fruit. Top right: round matcha macaron inspired by reference bottom row THIRD from left, two green shells and dark green filling. Bottom left: elongated matcha cream puff/eclair inspired by reference bottom row FIRST from left, short golden choux base and dark matcha cream zigzag on top. Bottom right: small round matcha tart inspired by reference bottom row LAST at right, green pastry shell and pale green cream swirl. All four predominantly green, with restrained cream/golden pastry accents. Match gentle cozy cafe pixel-art assets: discrete chunky pixel blocks, dark muted olive/plum one-pixel outline, limited harmonious palette, soft mint/pistachio/forest greens, slight top-down front 3/4 view, consistent apparent size and lighting. Each object one item, centered in its quadrant, entirely visible. No plates, table, shadows outside silhouettes, text, labels, extra food, frames or checkerboard. Actual alpha transparent background. Raster pixel edges with no blur, like sprites drawn at approximately 48x48 native pixels and enlarged with nearest neighbor.

