# 3D Chess — C# WinForms OpenGL 4

[![GitHub Sponsors](https://img.shields.io/github/sponsors/makarov-mm?style=flat&logo=github)](https://github.com/sponsors/makarov-mm)
[![License](https://img.shields.io/badge/license-MIT-blue)](LICENSE)
![C#](https://img.shields.io/badge/C%23-239120)
![OpenGL](https://img.shields.io/badge/OpenGL-5586A4?logo=opengl&logoColor=white)
![Windows](https://img.shields.io/badge/Windows-0078D6?logo=windows&logoColor=white)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-0A66C2?logo=linkedin&logoColor=white)](https://www.linkedin.com/in/makarov-mm/)
[![Threads](https://img.shields.io/badge/Threads-000000?logo=threads&logoColor=white)](https://www.threads.net/@m.m.makarov)
[![Instagram](https://img.shields.io/badge/Instagram-E4405F?logo=instagram&logoColor=white)](https://www.instagram.com/m.m.makarov/)

A small 3D chess pet project written in C# using WinForms and raw OpenGL 4.

The project renders a 3D chess board with OBJ-based chess pieces, GLSL shaders, lighting, reflections and mouse interaction. It is not based on a game engine or a 3D framework — the rendering pipeline, model loading, camera control and board interaction are implemented manually.

![Screenshot](screenshot.png)

## Features

- 3D chess board rendered with OpenGL 4 (core profile, multisampled, vertical sync)
- Chess pieces loaded from `.obj` models
- GLSL shader-based rendering: lighting, glossy materials, reflections in a glowing floor grid
- Camera rotation and zoom with the mouse; the camera starts on the player's side
- Mouse picking for selecting pieces and board squares
- Highlighting of the selected piece and its legal moves
- Complete chess rules: castling, en passant, promotion, check, checkmate, stalemate, draws by repetition, by the fifty-move rule and for lack of material
- Computer opponent: alpha-beta search with three levels; it thinks in the background, so the window stays responsive
- Per-monitor DPI awareness
- No external graphics/game engine or NuGet packages

## Controls

| Action | Control |
|---|---|
| Select piece / move | Left mouse click |
| Deselect | Click the selected piece again |
| Rotate camera | Hold and drag mouse |
| Zoom | Mouse wheel |
| New game | `N` |
| Computer opponent on / off | `C` |
| Swap sides with the computer | `S` |
| Computer level | `1`, `2`, `3` |
| Reset camera | `Home` |
| Close | `Esc` |

## Technical details

The goal of this project was not to build a strong chess engine, but to combine several low-level graphics and interaction systems in one understandable C# project:

- OpenGL context creation from WinForms through WGL (`LibraryImport` and function pointers, no wrapper library)
- GLSL shader compilation and linking
- OBJ model loading
- 3D camera and projection matrices (`System.Numerics`)
- Rendering of multiple mesh instances
- Mouse ray picking
- Board state, move generation and legality checks
- A computer player searching a few moves ahead
- Visual feedback for interaction

## Requirements

- Windows
- .NET 11 SDK
- GPU/driver with OpenGL 4.0 support
- Visual Studio or `dotnet` CLI

## Running

Open the project in Visual Studio and run it, or use the .NET CLI from the project directory:

```bash
dotnet run
```

## Project structure

```text
.
├── Models/          chess piece models (.obj)
├── Shaders/         chess.vert, chess.frag
├── Core.cs          entry point
├── ChessForm.cs     window, input, game flow, computer moves in the background
├── Renderer.cs      scene drawing: board, pieces, reflections, highlights, text overlay
├── Camera.cs        orbit camera, projection, mouse picking
├── ChessGame.cs     chess rules and the computer player
├── ChessTypes.cs    pieces and moves
├── Mesh.cs          vertex buffers, generated meshes, the pixel font
├── ObjLoader.cs     OBJ model loader
├── Gl.cs            OpenGL functions
├── Wgl.cs           OpenGL context on a window (Win32, GDI, WGL)
├── Files.cs         paths of the shaders and models
└── screenshot.png
```

## Notes

This is a graphics programming experiment, not a production chess application. The player's pawns always promote to queens, and the computer searches to a fixed depth (2 to 4 half-moves) without a quiescence search, so it plays a modest game.

The interesting part of the project is the rendering and interaction pipeline: drawing a complete 3D scene, selecting pieces with the mouse, loading models, working with shaders and keeping the board state synchronized with what is shown on screen.

## License

MIT License

Copyright (c) 2026 Mykhailo Makarov

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

## Support

If you found this project interesting or useful, you can support my work:

[![GitHub Sponsors](https://img.shields.io/github/sponsors/makarov-mm?style=flat&logo=github)](https://github.com/sponsors/makarov-mm)
