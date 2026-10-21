# Reference material

## wadsworth_map.png

Map of the stretch of Route 94 (High St) in Wadsworth, OH that the game's town is based on,
supplied by Chad on 2026-10-21. Key features: Route 94 running north-south, side streets
(Garfield, Pine, Chestnut, Water, State, Walnut, Bergey, Park), the pond to the north-west and
the industrial buildings west of 94.

### How the Phase 1 block-out uses it

`Assets/_Project/Scripts/Editor/TownBlockoutBuilder.cs` builds a compressed version (about
170 m square, not to scale). x is east, z is north.

| Real place | In the block-out |
| --- | --- |
| Route 94 | North-south road at x = 40 |
| Industrial lot west of 94 | The facility, x -30..21, z 0..45, entrance facing south |
| Water St | East-west road at z = -16, full width |
| Chestnut, Pine, Park St | East-west roads east of 94 at z = 12, 31, 50 |
| Garfield St | Road west of 94 at z = 58, north of the facility |
| Walnut St, Bergey St | East-west roads at z = -45 and z = -70 |
| Pond | Fenced water x -75..-45, z 15..52 |
| Houses | Rows east of 94 and along the State/Walnut area south of Water St |

Building out the full town, landmarks such as the two grills on 94, and real proportions are
work for later phases.
