# CS464 Assignment 02 · Mohsin Raza Ojla · BSSE23040

- **Video:** https://drive.google.com/file/d/1wFWJrAfPcZm4NiKRbPqXCI27l8Ec0xOo/view?usp=sharing
- **Input:** The Input System package. `PlayerController` reads the project-wide actions `Move` and `Jump` from `InputSystem.actions` in `Update`. Active Input Handling is the template's default (Input System Package (New)), saved in `ProjectSettings`.
- **Ground check:** `Physics.CheckSphere` on a `Ground` layer. The sphere is a bit smaller than the capsule and the player is not on that layer, so it never finds the player's own capsule or a wall. Select the Player to see the sphere as a gizmo: green on ground, red in the air.
- **Camera:** exact smoothing with `1 − e^(−k·dt)` (k = 5) in `LateUpdate`, plus a look-ahead of 0.35 s.
- **Juice:** a squash when I land, shaped by `Ease.OutBack` and run by a coroutine. It starts on a landing after more than 0.1 s in the air.

## Tuning

| Setting | Value | Why this number |
|---|---|---|
| Speed | 5 m/s | Level05 is about 85 m long and takes about 17 s at this speed, which I measured at 16.9 s. That is quick but not rushed. It is slow enough to stay on the 1.5 m wide bridge in Level02 and to pass the 2 m doorways, and the jump carries far enough at this speed (see the jump numbers below). |
| Jump height | 1.8 m | It clears the 1.1 m low cover and the 1.2 m crates. At this speed the jump carries 5.1 m, which is a lot more than my widest gap of 2.8 m. |
| Time to apex | 0.61 s | I don't set this one. It follows from the height: v = √(2 · 9.81 · 1.8) = 5.94 m/s, and 5.94 ÷ 9.81 = 0.61 s. That is about 36 frames at 60 fps, long enough to see and steer. |
| Fall multiplier | 2.2× | The fall takes 0.41 s instead of 0.61 s, so the jump feels snappy and not floaty. The top of the jump stays at 1.8 m. |
| Coyote time | 0.1 s | At 5 m/s that is 0.5 m past the edge, which is enough to forgive a late press at the edge of the gaps in Level01 and Level04. |
| Jump buffer | 0.12 s | About 7 frames at 60 fps. I land at about 8.8 m/s, so a press up to about 0.9 m above the ground is still remembered. |
| Camera smoothing | k = 5 | The camera trails by speed ÷ k = 5 ÷ 5 = 1 m while I run, and it closes 63% of a gap in 0.2 s (1 ÷ k). It keeps up without feeling stiff. |
| Look-ahead | 0.35 s | At 5 m/s the camera aims 1.75 m ahead of me, so the next gap or doorway is in view before I get to it. |

## Four answers

1. **The jump, in numbers.** The jump height is 1.8 m and gravity is 9.81, so the launch speed is v = √(2 · 9.81 · 1.8) = 5.94 m/s. The Rigidbody weighs 1 kg, so the impulse (jumpForce) is m · v = 5.94. Going up takes 5.94 ÷ 9.81 = 0.61 s. Coming down with the fall multiplier of 2.2 takes √(2 · 1.8 ÷ (9.81 · 2.2)) = 0.41 s. The whole jump is about 1.01 s, so at 5 m/s one jump carries me about 5.1 m. My widest gap is 2.8 m (Level05), so it clears with about 2.3 m to spare. Unity's 0.02 s steps make the top about 3% lower (around 1.74 m), which does not change this.
2. **Update and FixedUpdate.** In `Update` the script reads Move and Jump, turns the move into a direction relative to the camera, clamps it to length 1, remembers when the jump was pressed and when I last stood on the ground, checks the coyote and buffer windows, queues the jump, and works out when a landing should squash. In `FixedUpdate` it does the ground check, sets `rb.linearVelocity` from the move and keeps the y speed, applies the queued jump once (it zeroes the vertical speed first, then adds the impulse) and adds the extra gravity while falling. If I swapped them, a jump press would be read in `FixedUpdate`, and a press only lasts one frame, so at 144 fps about two presses in three would be lost. Applying the jump in `Update` could add the impulse on several frames before the physics step runs, and the fall gravity would depend on the frame rate.
3. **Vector3.Dot.** In `PlayerController`: `fallSpeed = Mathf.Max(fallSpeed, Vector3.Dot(rb.linearVelocity, Vector3.down));`. The dot product with `Vector3.down` is positive while I move down and negative while I move up, so it is how fast I am falling. I use it to see how hard a landing is. A small drop like a stair step (under 3 m/s) does not squash, and a harder landing squashes more.
4. **The juice.** It is a squash on landing. The body is squashed to between 0.85 and 0.7 of its height, depending on how hard the landing was, and the width grows to keep the volume the same (at 0.7 the width is about 1.2). It then eases back to normal with `Ease.OutBack`, which goes a little past 1 and settles, so it springs back. It lasts 0.35 s, which is 21 frames at 60 fps. That is long enough to see and short enough to be over before the next jump. It starts only after more than 0.1 s in the air, a running one is stopped first, and it ends exactly at rest.

## Levels

| Level | What the controller must do there | What I changed for this jump |
|---|---|---|
| Level01 | Run through the room, the 3 m corridor and the yard around the low cover, then jump a 2.5 m gap to the goal. | Nothing. The 2.5 m gap is well under the 5.1 m the jump carries. |
| Level02 | Take the safe left corridor or the right one with a 2.5 m gap, then cross a 1.5 m wide bridge that is 12 m long. | Nothing. The bridge is wide enough for the 1 m capsule, and the gap is under the jump. |
| Level03 | Go through the 3 m corridor and a 2 m wide squeeze, then walk up a 19° ramp that is 2.4 m high. | Nothing. No jump is needed, and the controller walks up the ramp fine. |
| Level04 | Climb two flights of 0.3 m steps (3 m high each), and jump two 2.5 m gaps between platforms. | Nothing. The capsule climbs the 0.3 m steps without extra code, and both gaps are under the jump. |
| Level05 | Pass three 2 m doorways in a row, walk up a 19° ramp, and make the last jump over the widest gap, 2.8 m, onto the goal block. | Nothing. 2.8 m is the widest gap and the jump still clears it by about 2.3 m. |

## Notes

- Made and tested in Unity 6000.6.2f1. The levels are the five from Assignment 01. I only put every level piece on the `Ground` layer and added the Player and Camera prefabs to each scene.
- `Assets/Scripts/RouteAutopilot.cs` and `Assets/Editor/LevelRunner.cs` are a test driver. It presses the real Move and Jump inputs on a virtual gamepad and walks each level, so I could check that every level plays from spawn to goal (menu: CS464 > Test All Levels). It is never in a scene.
- On the first open, Unity itself printed one error from its own Search index (`SearchDatabase`). It does not come from my scripts.
